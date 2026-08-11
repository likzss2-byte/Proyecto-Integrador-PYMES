using System.Globalization;
using System.Text.RegularExpressions;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using SQLite;

namespace InventorySystem.Infrastructure.Repositories;

public sealed partial class ProductRepository
{
    private readonly InventoryDatabase _database;

    public ProductRepository(InventoryDatabase database)
    {
        _database = database;
    }

    public Task<Product> SaveAsync(
        long businessId,
        ProductInput input,
        decimal initialStock = 0m,
        long? productId = null,
        CancellationToken cancellationToken = default)
    {
        InventoryRules.ValidateProduct(input);
        var sku = DbText(input.Sku) as string;
        var barcode = InventoryRules.NormalizeBarcode(input.Barcode);
        initialStock = InventoryRules.NormalizeQuantity(initialStock);
        if (initialStock < 0)
        {
            throw new InventoryRuleException("El inventario inicial no puede ser negativo.");
        }

        if (initialStock > 0)
        {
            InventoryRules.ValidateQuantity(initialStock, input.UnitOfMeasure, "El inventario inicial");
            if (input.ExpirationMode == ExpirationMode.Tracked && input.InitialExpirationDate is null)
            {
                throw new InventoryRuleException("La fecha de caducidad del inventario inicial es obligatoria.");
            }
        }

        return _database.WriteAsync(connection =>
        {
            EnsureBusinessExists(connection, businessId);
            EnsureUniqueBarcode(connection, businessId, barcode, productId);
            var now = SqliteValues.Date(DateTime.UtcNow);
            long id;
            if (productId.HasValue)
            {
                var existing = GetRow(connection, businessId, productId.Value)
                    ?? throw new InventoryRuleException("El producto no existe.");
                connection.Execute(
                    """
                    UPDATE products SET sku=?,barcode=?,name=?,description=?,brand=?,unit_of_measure=?,
                        minimum_stock_milli=?,sale_price_basis=?,expiration_mode=?,active=?,
                        archived_by_delete=CASE WHEN ?=1 THEN 0 ELSE archived_by_delete END,updated_at=?
                    WHERE id=? AND business_id=?;
                    """,
                    sku,
                    barcode,
                    input.Name.Trim(),
                    DbText(input.Description),
                    DbText(input.Brand),
                    (int)input.UnitOfMeasure,
                    SqliteValues.ToMilli(input.MinimumStock),
                    SqliteValues.ToMoney(input.SalePrice),
                    (int)input.ExpirationMode,
                    input.Active ? 1 : 0,
                    input.Active ? 1 : 0,
                    now,
                    existing.Id,
                    businessId);
                id = existing.Id;
            }
            else
            {
                connection.Execute(
                    """
                    INSERT INTO products(
                        business_id,sku,barcode,name,description,brand,unit_of_measure,stock_milli,
                        minimum_stock_milli,sale_price_basis,expiration_mode,active,created_at,updated_at)
                    VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?);
                    """,
                    businessId,
                    sku,
                    barcode,
                    input.Name.Trim(),
                    DbText(input.Description),
                    DbText(input.Brand),
                    (int)input.UnitOfMeasure,
                    SqliteValues.ToMilli(initialStock),
                    SqliteValues.ToMilli(input.MinimumStock),
                    SqliteValues.ToMoney(input.SalePrice),
                    (int)input.ExpirationMode,
                    input.Active ? 1 : 0,
                    now,
                    now);
                id = connection.ExecuteScalar<long>("SELECT last_insert_rowid();");
                if (initialStock != 0)
                {
                    var lotId = InventoryLotPersistence.Add(
                        connection,
                        id,
                        initialStock,
                        input.ExpirationMode == ExpirationMode.Tracked ? input.InitialExpirationDate : null,
                        string.IsNullOrWhiteSpace(input.InitialLotCode) ? "EXISTENCIA-INICIAL" : input.InitialLotCode,
                        now);
                    var movementId = InsertMovement(
                        connection,
                        businessId,
                        id,
                        InventoryMovementType.InitialInventory,
                        initialStock,
                        0m,
                        initialStock,
                        $"INICIAL-{id}",
                        "Inventario inicial",
                        now);
                    InventoryLotPersistence.RecordMovementAllocations(
                        connection,
                        movementId,
                        [new LotAllocation(lotId, initialStock)]);
                }
            }

            return GetRow(connection, businessId, id)!.ToDomain();
        }, cancellationToken);
    }

    public Task<Product?> GetAsync(
        long businessId,
        long productId,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync(
            connection => GetRow(connection, businessId, productId)?.ToDomain(),
            cancellationToken);

    public Task<Product?> FindByCodeAsync(
        long businessId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalized = InventoryRules.NormalizeScannedCode(code);
        if (normalized.Length == 0)
        {
            return Task.FromResult<Product?>(null);
        }

        return _database.ReadAsync(connection =>
            connection.Query<ProductRow>(
                    $"SELECT {RepositoryRowMapper.ProductColumns} FROM products WHERE business_id=? AND barcode=? COLLATE NOCASE LIMIT 1;",
                    businessId,
                    normalized)
                .FirstOrDefault()?.ToDomain(),
            cancellationToken);
    }

    public Task<IReadOnlyList<Product>> SearchAsync(
        long businessId,
        string? search = null,
        bool includeInactive = false,
        string orderBy = "recent",
        bool descending = true,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<Product>>(connection =>
        {
            var conditions = new List<string> { "business_id=?" };
            var values = new List<object> { businessId };
            if (!includeInactive)
            {
                conditions.Add("active=1");
            }

            var queryCondition = BuildSearchCondition(search, values);
            if (!string.IsNullOrWhiteSpace(queryCondition))
            {
                conditions.Add(queryCondition);
            }

            var orderColumn = orderBy.ToLowerInvariant() switch
            {
                "name" or "alphabetical" => "name COLLATE NOCASE",
                "price" => "sale_price_basis",
                _ => "updated_at"
            };
            var direction = descending ? "DESC" : "ASC";
            var rows = connection.Query<ProductRow>(
                $"SELECT {RepositoryRowMapper.ProductColumns} FROM products WHERE {string.Join(" AND ", conditions)} ORDER BY {orderColumn} {direction},id {direction};",
                values.ToArray());
            return rows.Select(row => row.ToDomain()).ToArray();
        }, cancellationToken);

    private static string? BuildSearchCondition(string? rawSearch, List<object> values)
    {
        if (string.IsNullOrWhiteSpace(rawSearch))
        {
            return null;
        }

        var groups = rawSearch
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(group => group
                .Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToArray())
            .Where(group => group.Length > 0)
            .ToArray();
        if (groups.Length == 0)
        {
            return null;
        }

        var regularGroups = new List<string>(groups.Length);
        var exclusionOnlyGroups = new List<string>();
        foreach (var group in groups)
        {
            var terms = new List<string>(group.Length);
            var hasPositiveTerm = false;
            foreach (var rawTerm in group)
            {
                var term = rawTerm.Trim();
                var negated = term.StartsWith('!');
                if (negated)
                {
                    term = term[1..].Trim();
                }
                else
                {
                    hasPositiveTerm = true;
                }

                if (term.Length == 0)
                {
                    continue;
                }

                var condition = TryBuildNumericCondition(term, values) ?? BuildTextCondition(term, values);
                terms.Add(negated ? $"NOT ({condition})" : condition);
            }

            if (terms.Count == 0)
            {
                continue;
            }

            var groupCondition = $"({string.Join(" AND ", terms)})";
            if (hasPositiveTerm)
            {
                regularGroups.Add(groupCondition);
            }
            else
            {
                exclusionOnlyGroups.Add(groupCondition);
            }
        }

        var orGroups = new List<string>(regularGroups.Count + 1);
        if (exclusionOnlyGroups.Count > 0)
        {
            orGroups.Add($"({string.Join(" AND ", exclusionOnlyGroups)})");
        }
        orGroups.AddRange(regularGroups);

        return orGroups.Count == 0 ? null : $"({string.Join(" OR ", orGroups)})";
    }

    private static string BuildTextCondition(string term, List<object> values)
    {
        var partial = $"%{EscapeLike(term)}%";
        var exact = term.Trim();
        values.Add(partial);
        values.Add(partial);
        values.Add(partial);
        values.Add(exact);
        return "(name LIKE ? ESCAPE '\\' COLLATE NOCASE OR description LIKE ? ESCAPE '\\' COLLATE NOCASE OR brand LIKE ? ESCAPE '\\' COLLATE NOCASE OR barcode=? COLLATE NOCASE)";
    }

    private static string? TryBuildNumericCondition(string term, List<object> values)
    {
        var match = NumericSearchRegex().Match(term);
        if (!match.Success)
        {
            return null;
        }

        var field = match.Groups["field"].Value.ToLowerInvariant();
        var first = ParseSearchDecimal(match.Groups["first"].Value);
        var secondGroup = match.Groups["second"];
        var column = field == "stock" ? "stock_milli" : "sale_price_basis";
        Func<decimal, long> convert = field == "stock" ? SqliteValues.ToMilli : SqliteValues.ToMoney;

        if (secondGroup.Success)
        {
            var second = ParseSearchDecimal(secondGroup.Value);
            var minimum = Math.Min(first, second);
            var maximum = Math.Max(first, second);
            values.Add(convert(minimum));
            values.Add(convert(maximum));
            return $"({column} BETWEEN ? AND ?)";
        }

        var op = match.Groups["op"].Success ? match.Groups["op"].Value : "=";
        values.Add(convert(first));
        return $"({column} {op} ?)";
    }

    private static decimal ParseSearchDecimal(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InventoryRuleException($"El valor numérico '{value}' no es válido en la búsqueda.");
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);

    [GeneratedRegex(@"^(?<field>stock|precio|price)\s*(?:(?<op>>=|<=|>|<|=)\s*)?(?<first>\d+(?:\.\d+)?)\s*(?:-\s*(?<second>\d+(?:\.\d+)?))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumericSearchRegex();

    internal static ProductRow? GetRow(SQLiteConnection connection, long businessId, long productId) =>
        connection.Query<ProductRow>(
                $"SELECT {RepositoryRowMapper.ProductColumns} FROM products WHERE id=? AND business_id=? LIMIT 1;",
                productId,
                businessId)
            .FirstOrDefault();

    internal static long InsertMovement(
        SQLiteConnection connection,
        long businessId,
        long productId,
        InventoryMovementType type,
        decimal quantity,
        decimal previousStock,
        decimal resultingStock,
        string reference,
        string? reason,
        string occurredAt,
        long? inventoryCountId = null)
    {
        connection.Execute(
            """
            INSERT INTO inventory_movements(
                business_id,product_id,movement_type,quantity_milli,previous_stock_milli,
                resulting_stock_milli,reference,reason,occurred_at,inventory_count_id)
            VALUES(?,?,?,?,?,?,?,?,?,?);
            """,
            businessId,
            productId,
            (int)type,
            SqliteValues.ToMilli(quantity),
            SqliteValues.ToMilli(previousStock),
            SqliteValues.ToMilli(resultingStock),
            reference,
            DbText(reason),
            occurredAt,
            inventoryCountId);
        return connection.ExecuteScalar<long>("SELECT last_insert_rowid();");
    }

    internal static object? DbText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureBusinessExists(SQLiteConnection connection, long businessId)
    {
        if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM businesses WHERE id=?;", businessId) == 0)
        {
            throw new InventoryRuleException("El negocio no existe.");
        }
    }

    private static void EnsureUniqueBarcode(
        SQLiteConnection connection,
        long businessId,
        string? barcode,
        long? productId)
    {
        var excluded = productId ?? 0;
        if (barcode is not null && connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM products WHERE business_id=? AND barcode=? COLLATE NOCASE AND id<>?;",
                businessId,
                barcode,
                excluded) > 0)
        {
            throw new InventoryRuleException("El código de barras ya pertenece a otro producto.");
        }
    }

    public Task<bool> DeleteOrArchiveAsync(
        long businessId,
        long productId,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var product = GetRow(connection, businessId, productId)
                ?? throw new InventoryRuleException("El producto no existe.");

            var hasReferences = connection.ExecuteScalar<int>(
                """
                SELECT (
                    EXISTS(SELECT 1 FROM inventory_lots WHERE product_id=?) OR
                    EXISTS(SELECT 1 FROM inventory_document_lines WHERE product_id=?) OR
                    EXISTS(SELECT 1 FROM inventory_movements WHERE product_id=?) OR
                    EXISTS(SELECT 1 FROM inventory_count_lines WHERE product_id=?) OR
                    EXISTS(SELECT 1 FROM product_suppliers WHERE product_id=?) OR
                    EXISTS(SELECT 1 FROM recent_product_queries WHERE product_id=?) OR
                    EXISTS(SELECT 1 FROM purchase_order_lines WHERE product_id=?)
                );
                """,
                productId, productId, productId, productId, productId, productId, productId) == 1;

            if (hasReferences)
            {
                connection.Execute(
                    "UPDATE products SET active=0,archived_by_delete=1,updated_at=? WHERE id=? AND business_id=?;",
                    SqliteValues.Date(DateTime.UtcNow),
                    product.Id,
                    businessId);
                return false;
            }

            connection.Execute("DELETE FROM products WHERE id=? AND business_id=?;", product.Id, businessId);
            return true;
        }, cancellationToken);
}
