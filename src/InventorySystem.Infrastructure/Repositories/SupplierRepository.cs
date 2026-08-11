using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;

namespace InventorySystem.Infrastructure.Repositories;

public sealed class SupplierRepository
{
    private readonly InventoryDatabase _database;

    public SupplierRepository(InventoryDatabase database)
    {
        _database = database;
    }

    public Task<Supplier> SaveAsync(
        long businessId,
        SupplierInput input,
        long? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        InventoryRules.ValidateSupplier(input);
        var phones = NormalizeContactValues(input.Phones, input.Phone);
        var emails = NormalizeContactValues(input.Emails, input.Email);

        return _database.WriteAsync(connection =>
        {
            var excluded = supplierId ?? 0;
            if (connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM suppliers WHERE business_id=? AND company_name=? COLLATE NOCASE AND id<>?;",
                    businessId,
                    input.CompanyName.Trim(),
                    excluded) > 0)
            {
                throw new InventoryRuleException("Ya existe un proveedor con esa empresa o razón social.");
            }

            var now = SqliteValues.Date(DateTime.UtcNow);
            long id;
            if (supplierId.HasValue)
            {
                if (GetRow(connection, businessId, supplierId.Value) is null)
                {
                    throw new InventoryRuleException("El proveedor no existe.");
                }

                connection.Execute(
                    """
                    UPDATE suppliers SET company_name=?,contact_name=?,phone=?,email=?,country=?,state=?,address=?,notes=?,active=?,updated_at=?
                    WHERE id=? AND business_id=?;
                    """,
                    input.CompanyName.Trim(),
                    ProductRepository.DbText(input.ContactName),
                    ProductRepository.DbText(phones.FirstOrDefault()),
                    ProductRepository.DbText(emails.FirstOrDefault()),
                    ProductRepository.DbText(input.Country),
                    ProductRepository.DbText(input.State),
                    ProductRepository.DbText(input.Address),
                    ProductRepository.DbText(input.Notes),
                    input.Active ? 1 : 0,
                    now,
                    supplierId.Value,
                    businessId);
                id = supplierId.Value;
            }
            else
            {
                connection.Execute(
                    """
                    INSERT INTO suppliers(
                        business_id,company_name,contact_name,phone,email,country,state,address,notes,active,created_at,updated_at)
                    VALUES(?,?,?,?,?,?,?,?,?,?,?,?);
                    """,
                    businessId,
                    input.CompanyName.Trim(),
                    ProductRepository.DbText(input.ContactName),
                    ProductRepository.DbText(phones.FirstOrDefault()),
                    ProductRepository.DbText(emails.FirstOrDefault()),
                    ProductRepository.DbText(input.Country),
                    ProductRepository.DbText(input.State),
                    ProductRepository.DbText(input.Address),
                    ProductRepository.DbText(input.Notes),
                    input.Active ? 1 : 0,
                    now,
                    now);
                id = connection.ExecuteScalar<long>("SELECT last_insert_rowid();");
            }

            ReplaceContacts(connection, "supplier_phones", "phone", id, phones, now);
            ReplaceContacts(connection, "supplier_emails", "email", id, emails, now);
            return MapSupplier(connection, GetRow(connection, businessId, id)!);
        }, cancellationToken);
    }

    public Task<Supplier?> GetAsync(
        long businessId,
        long supplierId,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync(connection =>
        {
            var row = GetRow(connection, businessId, supplierId);
            return row is null ? null : MapSupplier(connection, row);
        }, cancellationToken);

    public Task<IReadOnlyList<Supplier>> SearchAsync(
        long businessId,
        string? search = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<Supplier>>(connection =>
        {
            var conditions = new List<string> { "s.business_id=?" };
            var values = new List<object> { businessId };
            if (!includeInactive)
            {
                conditions.Add("s.active=1");
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                conditions.Add("""
                    (s.company_name LIKE ? OR s.contact_name LIKE ? OR
                     EXISTS(SELECT 1 FROM supplier_phones sp WHERE sp.supplier_id=s.id AND sp.phone LIKE ?) OR
                     EXISTS(SELECT 1 FROM supplier_emails se WHERE se.supplier_id=s.id AND se.email LIKE ?))
                    """);
                var term = $"%{search.Trim()}%";
                values.AddRange([term, term, term, term]);
            }

            var rows = connection.Query<SupplierRow>(
                $"""
                SELECT s.id Id,s.business_id BusinessId,s.company_name CompanyName,s.contact_name ContactName,
                       s.phone Phone,s.email Email,s.country Country,s.state State,s.address Address,s.notes Notes,
                       s.active Active,s.created_at CreatedAt,s.updated_at UpdatedAt
                FROM suppliers s
                WHERE {string.Join(" AND ", conditions)}
                ORDER BY s.company_name COLLATE NOCASE;
                """,
                values.ToArray());
            return rows.Select(row => MapSupplier(connection, row)).ToArray();
        }, cancellationToken);

    public Task<bool> DeleteOrArchiveAsync(
        long businessId,
        long supplierId,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var supplier = GetRow(connection, businessId, supplierId)
                ?? throw new InventoryRuleException("El proveedor no existe.");
            var hasReferences = connection.ExecuteScalar<int>(
                """
                SELECT (
                    EXISTS(SELECT 1 FROM inventory_documents WHERE supplier_id=?) OR
                    EXISTS(SELECT 1 FROM product_suppliers WHERE supplier_id=?) OR
                    EXISTS(SELECT 1 FROM inventory_lots WHERE supplier_id=?) OR
                    EXISTS(SELECT 1 FROM purchase_orders WHERE supplier_id=?) OR
                    EXISTS(SELECT 1 FROM inventory_counts WHERE supplier_id=?)
                );
                """,
                supplierId, supplierId, supplierId, supplierId, supplierId) == 1;

            if (hasReferences)
            {
                connection.Execute(
                    "UPDATE suppliers SET active=0,updated_at=? WHERE id=? AND business_id=?;",
                    SqliteValues.Date(DateTime.UtcNow),
                    supplier.Id,
                    businessId);
                return false;
            }

            connection.Execute("DELETE FROM supplier_phones WHERE supplier_id=?;", supplierId);
            connection.Execute("DELETE FROM supplier_emails WHERE supplier_id=?;", supplierId);
            connection.Execute("DELETE FROM suppliers WHERE id=? AND business_id=?;", supplierId, businessId);
            return true;
        }, cancellationToken);

    public Task<IReadOnlyList<Supplier>> GetSuppliersForProductAsync(
        long businessId,
        long productId,
        bool includeInactive = true,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<Supplier>>(connection =>
        {
            if (ProductRepository.GetRow(connection, businessId, productId) is null)
            {
                throw new InventoryRuleException("El producto no existe.");
            }

            var rows = connection.Query<SupplierRow>(
                $"""
                SELECT s.id Id,s.business_id BusinessId,s.company_name CompanyName,s.contact_name ContactName,
                       s.phone Phone,s.email Email,s.country Country,s.state State,s.address Address,s.notes Notes,
                       s.active Active,s.created_at CreatedAt,s.updated_at UpdatedAt
                FROM suppliers s
                JOIN product_suppliers ps ON ps.supplier_id=s.id
                WHERE ps.product_id=? AND ps.active=1 AND s.business_id=?
                  AND (?=1 OR s.active=1)
                ORDER BY s.company_name COLLATE NOCASE;
                """,
                productId,
                businessId,
                includeInactive ? 1 : 0);
            return rows.Select(row => MapSupplier(connection, row)).ToArray();
        }, cancellationToken);

    public Task<ProductSupplier> LinkProductAsync(
        long businessId,
        ProductSupplierInput input,
        CancellationToken cancellationToken = default)
    {
        if (input.ReferenceCost is < 0)
        {
            throw new InventoryRuleException("El costo de referencia no puede ser negativo.");
        }

        return _database.WriteAsync(connection =>
        {
            var product = ProductRepository.GetRow(connection, businessId, input.ProductId)
                ?? throw new InventoryRuleException("El producto no existe.");
            var supplier = GetRow(connection, businessId, input.SupplierId)
                ?? throw new InventoryRuleException("El proveedor no existe.");
            if (product.BusinessId != supplier.BusinessId)
            {
                throw new InventoryRuleException("El producto y el proveedor deben pertenecer al mismo negocio.");
            }

            var now = SqliteValues.Date(DateTime.UtcNow);
            connection.Execute(
                """
                INSERT INTO product_suppliers(product_id,supplier_id,supplier_sku,reference_cost_basis,active,created_at,updated_at)
                VALUES(?,?,?,?,?,?,?)
                ON CONFLICT(product_id,supplier_id) DO UPDATE SET
                    supplier_sku=excluded.supplier_sku,
                    reference_cost_basis=excluded.reference_cost_basis,
                    active=excluded.active,
                    updated_at=excluded.updated_at;
                """,
                input.ProductId,
                input.SupplierId,
                ProductRepository.DbText(input.SupplierSku),
                input.ReferenceCost.HasValue ? SqliteValues.ToMoney(input.ReferenceCost.Value) : null,
                input.Active ? 1 : 0,
                now,
                now);
            return connection.Query<ProductSupplierRow>(
                    """
                    SELECT id Id,product_id ProductId,supplier_id SupplierId,supplier_sku SupplierSku,
                           reference_cost_basis ReferenceCostBasis,active Active,created_at CreatedAt,updated_at UpdatedAt
                    FROM product_suppliers WHERE product_id=? AND supplier_id=? LIMIT 1;
                    """,
                    input.ProductId,
                    input.SupplierId)
                .Single()
                .ToDomain();
        }, cancellationToken);
    }

    public Task UnlinkProductAsync(
        long businessId,
        long productId,
        long supplierId,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            if (ProductRepository.GetRow(connection, businessId, productId) is null)
            {
                throw new InventoryRuleException("El producto no existe.");
            }

            if (GetRow(connection, businessId, supplierId) is null)
            {
                throw new InventoryRuleException("El proveedor no existe.");
            }

            connection.Execute(
                "UPDATE product_suppliers SET active=0,updated_at=? WHERE product_id=? AND supplier_id=?;",
                SqliteValues.Date(DateTime.UtcNow),
                productId,
                supplierId);
        }, cancellationToken);

    public Task<IReadOnlyList<ProductSupplier>> GetProductSuppliersAsync(
        long businessId,
        long productId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<ProductSupplier>>(connection =>
        {
            if (ProductRepository.GetRow(connection, businessId, productId) is null)
            {
                throw new InventoryRuleException("El producto no existe.");
            }

            var rows = connection.Query<ProductSupplierRow>(
                """
                SELECT id Id,product_id ProductId,supplier_id SupplierId,supplier_sku SupplierSku,
                       reference_cost_basis ReferenceCostBasis,active Active,created_at CreatedAt,updated_at UpdatedAt
                FROM product_suppliers WHERE product_id=? AND (?=1 OR active=1) ORDER BY id;
                """,
                productId,
                includeInactive ? 1 : 0);
            return rows.Select(row => row.ToDomain()).ToArray();
        }, cancellationToken);

    private static IReadOnlyList<string> NormalizeContactValues(IReadOnlyList<string>? values, string? legacyValue)
    {
        var source = values is { Count: > 0 }
            ? values
            : string.IsNullOrWhiteSpace(legacyValue) ? Array.Empty<string>() : new[] { legacyValue! };
        return source
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ReplaceContacts(
        SQLite.SQLiteConnection connection,
        string table,
        string column,
        long supplierId,
        IReadOnlyList<string> values,
        string now)
    {
        connection.Execute($"DELETE FROM {table} WHERE supplier_id=?;", supplierId);
        for (var index = 0; index < values.Count; index++)
        {
            connection.Execute(
                $"INSERT INTO {table}(supplier_id,{column},position,created_at) VALUES(?,?,?,?);",
                supplierId,
                values[index],
                index,
                now);
        }
    }

    private static Supplier MapSupplier(SQLite.SQLiteConnection connection, SupplierRow row)
    {
        var supplier = row.ToDomain();
        supplier.Phones = connection.Query<ContactValueRow>(
                "SELECT phone Value FROM supplier_phones WHERE supplier_id=? ORDER BY position,id;",
                row.Id)
            .Select(value => value.Value)
            .ToArray();
        supplier.Emails = connection.Query<ContactValueRow>(
                "SELECT email Value FROM supplier_emails WHERE supplier_id=? ORDER BY position,id;",
                row.Id)
            .Select(value => value.Value)
            .ToArray();
        supplier.Phone = supplier.Phones.FirstOrDefault();
        supplier.Email = supplier.Emails.FirstOrDefault();
        return supplier;
    }

    private sealed class ContactValueRow
    {
        public string Value { get; set; } = string.Empty;
    }

    internal static SupplierRow? GetRow(SQLite.SQLiteConnection connection, long businessId, long supplierId) =>
        connection.Query<SupplierRow>(
                $"SELECT {RepositoryRowMapper.SupplierColumns} FROM suppliers WHERE id=? AND business_id=? LIMIT 1;",
                supplierId,
                businessId)
            .FirstOrDefault();
}
