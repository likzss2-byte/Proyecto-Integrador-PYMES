using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using SQLite;

namespace InventorySystem.Infrastructure.Repositories;

public sealed class ProductRepository
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
        var sku = InventoryRules.NormalizeSku(input.Sku);
        var barcode = InventoryRules.NormalizeBarcode(input.Barcode);
        initialStock = InventoryRules.NormalizeQuantity(initialStock);
        if (initialStock < 0)
        {
            throw new InventoryRuleException("El inventario inicial no puede ser negativo.");
        }

        if (initialStock > 0)
        {
            InventoryRules.ValidateQuantity(initialStock, input.UnitOfMeasure, "El inventario inicial");
        }

        return _database.WriteAsync(connection =>
        {
            EnsureBusinessExists(connection, businessId);
            EnsureUniqueCodes(connection, businessId, sku, barcode, productId);
            var now = SqliteValues.Date(DateTime.UtcNow);
            long id;
            if (productId.HasValue)
            {
                var existing = GetRow(connection, businessId, productId.Value)
                    ?? throw new InventoryRuleException("El producto no existe.");
                connection.Execute(
                    """
                    UPDATE products SET sku=?,barcode=?,name=?,description=?,brand=?,unit_of_measure=?,
                        minimum_stock_milli=?,sale_price_basis=?,active=?,updated_at=?
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
                        minimum_stock_milli,sale_price_basis,active,created_at,updated_at)
                    VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?);
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
                    input.Active ? 1 : 0,
                    now,
                    now);
                id = connection.ExecuteScalar<long>("SELECT last_insert_rowid();");
                if (initialStock != 0)
                {
                    InsertMovement(
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
        {
            var byBarcode = connection.Query<ProductRow>(
                    $"SELECT {RepositoryRowMapper.ProductColumns} FROM products WHERE business_id=? AND barcode=? COLLATE NOCASE LIMIT 1;",
                    businessId,
                    normalized)
                .FirstOrDefault();
            if (byBarcode is not null)
            {
                return byBarcode.ToDomain();
            }

            var bySku = connection.Query<ProductRow>(
                    $"SELECT {RepositoryRowMapper.ProductColumns} FROM products WHERE business_id=? AND sku=? COLLATE NOCASE LIMIT 1;",
                    businessId,
                    InventoryRules.NormalizeSku(normalized))
                .FirstOrDefault();
            return bySku?.ToDomain();
        }, cancellationToken);
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

            if (!string.IsNullOrWhiteSpace(search))
            {
                conditions.Add("(name LIKE ? OR brand LIKE ? OR sku LIKE ? OR barcode LIKE ?)");
                var term = $"%{search.Trim()}%";
                values.AddRange([term, term, term, term]);
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

    internal static ProductRow? GetRow(SQLiteConnection connection, long businessId, long productId) =>
        connection.Query<ProductRow>(
                $"SELECT {RepositoryRowMapper.ProductColumns} FROM products WHERE id=? AND business_id=? LIMIT 1;",
                productId,
                businessId)
            .FirstOrDefault();

    internal static void InsertMovement(
        SQLiteConnection connection,
        long businessId,
        long productId,
        InventoryMovementType type,
        decimal quantity,
        decimal previousStock,
        decimal resultingStock,
        string reference,
        string? reason,
        string occurredAt)
    {
        connection.Execute(
            """
            INSERT INTO inventory_movements(
                business_id,product_id,movement_type,quantity_milli,previous_stock_milli,
                resulting_stock_milli,reference,reason,occurred_at)
            VALUES(?,?,?,?,?,?,?,?,?);
            """,
            businessId,
            productId,
            (int)type,
            SqliteValues.ToMilli(quantity),
            SqliteValues.ToMilli(previousStock),
            SqliteValues.ToMilli(resultingStock),
            reference,
            DbText(reason),
            occurredAt);
    }

    internal static object? DbText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureBusinessExists(SQLiteConnection connection, long businessId)
    {
        if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM businesses WHERE id=?;", businessId) == 0)
        {
            throw new InventoryRuleException("El negocio no existe.");
        }
    }

    private static void EnsureUniqueCodes(
        SQLiteConnection connection,
        long businessId,
        string sku,
        string? barcode,
        long? productId)
    {
        var excluded = productId ?? 0;
        if (connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM products WHERE business_id=? AND sku=? COLLATE NOCASE AND id<>?;",
                businessId,
                sku,
                excluded) > 0)
        {
            throw new InventoryRuleException("El SKU ya pertenece a otro producto.");
        }

        if (barcode is not null && connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM products WHERE business_id=? AND barcode=? COLLATE NOCASE AND id<>?;",
                businessId,
                barcode,
                excluded) > 0)
        {
            throw new InventoryRuleException("El código de barras ya pertenece a otro producto.");
        }
    }
}
