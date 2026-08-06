using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using InventorySystem.Infrastructure.Repositories;

namespace InventorySystem.Infrastructure.Services;

public sealed class DashboardService
{
    private readonly InventoryDatabase _database;
    private readonly InventoryLotService _lots;

    public DashboardService(InventoryDatabase database, InventoryLotService lots)
    {
        _database = database;
        _lots = lots;
    }

    public Task<IReadOnlyList<MinimumStockAlert>> GetMinimumStockAsync(
        long businessId,
        int limit = 100,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<MinimumStockAlert>>(connection =>
        {
            var rows = connection.Query<MinimumStockRow>(
                """
                SELECT id ProductId,name ProductName,COALESCE(barcode,sku) Code,stock_milli StockMilli,
                       minimum_stock_milli MinimumStockMilli,unit_of_measure UnitOfMeasure
                FROM products
                WHERE business_id=? AND active=1 AND stock_milli<=minimum_stock_milli
                ORDER BY stock_milli ASC,minimum_stock_milli DESC,name COLLATE NOCASE
                LIMIT ?;
                """,
                businessId,
                limit);
            return rows.Select(row =>
            {
                var stock = SqliteValues.FromMilli(row.StockMilli);
                return new MinimumStockAlert(
                    row.ProductId,
                    row.ProductName,
                    row.Code,
                    stock,
                    SqliteValues.FromMilli(row.MinimumStockMilli),
                    (UnitOfMeasure)row.UnitOfMeasure,
                    stock <= 0 ? "Agotado" : "Stock mínimo");
            }).ToArray();
        }, cancellationToken);

    public async Task<InventoryDashboard> GetAsync(
        long businessId,
        CancellationToken cancellationToken = default)
    {
        var minimumStock = await GetMinimumStockAsync(businessId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var expiring = await _lots.GetExpiringAsync(businessId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var expired = await _lots.GetExpiredAsync(businessId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var counts = await _database.ReadAsync(connection =>
        {
            var pending = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM purchase_orders WHERE business_id=? AND status IN (?,?);",
                businessId,
                (int)PurchaseOrderStatus.Pending,
                (int)PurchaseOrderStatus.Confirmed);
            var partial = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM purchase_orders WHERE business_id=? AND status=?;",
                businessId,
                (int)PurchaseOrderStatus.PartiallyReceived);
            return (pending, partial);
        }, cancellationToken).ConfigureAwait(false);

        return new InventoryDashboard(
            new DashboardSummary(minimumStock.Count, expiring.Count, expired.Count, counts.pending, counts.partial),
            minimumStock,
            expiring,
            expired);
    }

    private sealed class MinimumStockRow
    {
        public long ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public long StockMilli { get; set; }
        public long MinimumStockMilli { get; set; }
        public int UnitOfMeasure { get; set; }
    }
}
