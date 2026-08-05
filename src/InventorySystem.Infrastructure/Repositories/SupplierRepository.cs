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
                    ProductRepository.DbText(input.Phone),
                    ProductRepository.DbText(input.Email),
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
                    ProductRepository.DbText(input.Phone),
                    ProductRepository.DbText(input.Email),
                    ProductRepository.DbText(input.Country),
                    ProductRepository.DbText(input.State),
                    ProductRepository.DbText(input.Address),
                    ProductRepository.DbText(input.Notes),
                    input.Active ? 1 : 0,
                    now,
                    now);
                id = connection.ExecuteScalar<long>("SELECT last_insert_rowid();");
            }

            return GetRow(connection, businessId, id)!.ToDomain();
        }, cancellationToken);
    }

    public Task<IReadOnlyList<Supplier>> SearchAsync(
        long businessId,
        string? search = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<Supplier>>(connection =>
        {
            var conditions = new List<string> { "business_id=?" };
            var values = new List<object> { businessId };
            if (!includeInactive)
            {
                conditions.Add("active=1");
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                conditions.Add("(company_name LIKE ? OR contact_name LIKE ? OR phone LIKE ? OR email LIKE ?)");
                var term = $"%{search.Trim()}%";
                values.AddRange([term, term, term, term]);
            }

            var rows = connection.Query<SupplierRow>(
                $"SELECT {RepositoryRowMapper.SupplierColumns} FROM suppliers WHERE {string.Join(" AND ", conditions)} ORDER BY company_name COLLATE NOCASE;",
                values.ToArray());
            return rows.Select(row => row.ToDomain()).ToArray();
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

    internal static SupplierRow? GetRow(SQLite.SQLiteConnection connection, long businessId, long supplierId) =>
        connection.Query<SupplierRow>(
                $"SELECT {RepositoryRowMapper.SupplierColumns} FROM suppliers WHERE id=? AND business_id=? LIMIT 1;",
                supplierId,
                businessId)
            .FirstOrDefault();
}
