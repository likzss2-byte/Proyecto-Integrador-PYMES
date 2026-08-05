using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using InventorySystem.Infrastructure.Repositories;

namespace InventorySystem.Infrastructure.Services;

public sealed class ProductLookupService
{
    private readonly InventoryDatabase _database;
    private readonly ProductRepository _products;
    private readonly IExternalProductCatalog _externalCatalog;

    public ProductLookupService(
        InventoryDatabase database,
        ProductRepository products,
        IExternalProductCatalog externalCatalog)
    {
        _database = database;
        _products = products;
        _externalCatalog = externalCatalog;
    }

    public async Task<ProductLookupResult> LookupAsync(
        long businessId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalized = InventoryRules.NormalizeScannedCode(code);
        if (normalized.Length == 0)
        {
            throw new InventoryRuleException("Captura o escanea un código.");
        }

        var local = await _products.FindByCodeAsync(businessId, normalized, cancellationToken).ConfigureAwait(false);
        if (local is not null)
        {
            await AddRecentQueryAsync(businessId, normalized, local.Id, "Local", cancellationToken).ConfigureAwait(false);
            return new ProductLookupResult(local, null);
        }

        var suggestion = await _externalCatalog.FindAsync(normalized, cancellationToken).ConfigureAwait(false);
        await AddRecentQueryAsync(
            businessId,
            normalized,
            null,
            suggestion?.Source ?? "Sin coincidencia",
            cancellationToken).ConfigureAwait(false);
        return new ProductLookupResult(null, suggestion);
    }

    private Task AddRecentQueryAsync(
        long businessId,
        string code,
        long? productId,
        string source,
        CancellationToken cancellationToken) =>
        _database.WriteAsync(connection =>
        {
            connection.Execute(
                "INSERT INTO recent_product_queries(business_id,code,product_id,source,queried_at) VALUES(?,?,?,?,?);",
                businessId,
                code,
                productId,
                source,
                SqliteValues.Date(DateTime.UtcNow));
            connection.Execute(
                """
                DELETE FROM recent_product_queries
                WHERE business_id=? AND id NOT IN(
                    SELECT id FROM recent_product_queries WHERE business_id=? ORDER BY queried_at DESC,id DESC LIMIT 100
                );
                """,
                businessId,
                businessId);
        }, cancellationToken);
}
