using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using InventorySystem.Infrastructure.Repositories;

namespace InventorySystem.Infrastructure.Services;

public sealed class InventoryCatalogService
{
    private readonly InventoryDatabase _database;
    private readonly ProductRepository _products;

    public InventoryCatalogService(InventoryDatabase database, ProductRepository products)
    {
        _database = database;
        _products = products;
    }

    public async Task<IReadOnlyList<Product>> GetProductsBySupplierAsync(
        long businessId,
        long supplierId,
        string? search = null,
        CancellationToken cancellationToken = default) =>
        (await GetSupplierProductCatalogAsync(
            businessId,
            supplierId,
            search,
            cancellationToken).ConfigureAwait(false))
        .Select(item => item.Product)
        .ToArray();

    public Task<IReadOnlyList<SupplierProductCatalogItem>> GetSupplierProductCatalogAsync(
        long businessId,
        long supplierId,
        string? search = null,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<SupplierProductCatalogItem>>(connection =>
        {
            if (connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM suppliers WHERE id=? AND business_id=? AND active=1;",
                    supplierId,
                    businessId) == 0)
            {
                throw new InventoryRuleException("Selecciona un proveedor activo.");
            }

            var term = $"%{(search ?? string.Empty).Trim()}%";
            var rows = connection.Query<ProductRow>(
                $"""
                SELECT DISTINCT
                    p.id Id,p.business_id BusinessId,p.sku Sku,p.barcode Barcode,p.name Name,
                    p.description Description,p.brand Brand,p.unit_of_measure UnitOfMeasure,
                    p.stock_milli StockMilli,p.minimum_stock_milli MinimumStockMilli,
                    p.sale_price_basis SalePriceBasis,ps.reference_cost_basis ReferenceCostBasis,
                    p.expiration_mode ExpirationMode,
                    (SELECT MIN(l.expiration_date) FROM inventory_lots l
                     WHERE l.product_id=p.id AND l.quantity_milli>0 AND l.expiration_date IS NOT NULL) NearestExpirationDate,
                    COALESCE((SELECT SUM(l.quantity_milli) FROM inventory_lots l
                     WHERE l.product_id=p.id AND l.quantity_milli>0 AND l.expiration_date IS NULL),0) UndatedStockMilli,
                    p.active Active,p.created_at CreatedAt,p.updated_at UpdatedAt
                FROM products p
                JOIN product_suppliers ps ON ps.product_id=p.id AND ps.active=1
                JOIN suppliers s ON s.id=ps.supplier_id AND s.active=1
                WHERE p.business_id=? AND p.active=1 AND s.id=?
                  AND (?='' OR p.name LIKE ? OR p.barcode LIKE ? OR p.brand LIKE ?)
                ORDER BY p.name COLLATE NOCASE,p.id;
                """,
                businessId,
                supplierId,
                (search ?? string.Empty).Trim(),
                term,
                term,
                term);
            return rows
                .Select(row => new SupplierProductCatalogItem(
                    row.ToDomain(),
                    row.ReferenceCostBasis.HasValue
                        ? SqliteValues.FromMoney(row.ReferenceCostBasis.Value)
                        : null))
                .ToArray();
        }, cancellationToken);

    public Task<IReadOnlyList<string>> GetBrandsAsync(
        long businessId,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<string>>(connection =>
        {
            var values = connection.Query<BrandRow>(
                """
                SELECT brand Brand FROM products
                WHERE business_id=? AND active=1 AND brand IS NOT NULL AND trim(brand)<>''
                ORDER BY brand COLLATE NOCASE;
                """,
                businessId);
            return values
                .Select(row => NormalizeBrandDisplay(row.Brand))
                .Where(value => value.Length > 0)
                .GroupBy(NormalizeBrandKey, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetProductsByBrandAsync(
        long businessId,
        string brand,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeBrandKey(brand);
        if (key.Length == 0)
        {
            throw new InventoryRuleException("Selecciona una marca.");
        }

        var products = await _products.SearchAsync(
            businessId,
            search,
            orderBy: "name",
            descending: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return products
            .Where(product => NormalizeBrandKey(product.Brand) == key)
            .ToArray();
    }

    public async Task<IReadOnlyList<Product>> SearchForFreeInventoryAsync(
        long businessId,
        string? search,
        CancellationToken cancellationToken = default) =>
        await _products.SearchAsync(
            businessId,
            search,
            orderBy: "name",
            descending: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public async Task<Product?> FindByCodeAsync(
        long businessId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var product = await _products.FindByCodeAsync(businessId, code, cancellationToken).ConfigureAwait(false);
        return product is { Active: true } ? product : null;
    }

    public static string NormalizeBrandDisplay(string? brand) =>
        string.Join(' ', (brand ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string NormalizeBrandKey(string? brand) =>
        NormalizeBrandDisplay(brand).ToUpperInvariant();

    private sealed class BrandRow
    {
        public string? Brand { get; set; }
    }
}
