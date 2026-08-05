using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using InventorySystem.Infrastructure.Repositories;

namespace InventorySystem.Infrastructure.Services;

public sealed class InventoryLotService
{
    private readonly InventoryDatabase _database;

    public InventoryLotService(InventoryDatabase database)
    {
        _database = database;
    }

    public Task<Product> ReceiveAsync(
        long businessId,
        long productId,
        decimal quantity,
        ExpirationMode expirationMode,
        DateOnly? expirationDate = null,
        string? lotCode = null,
        CancellationToken cancellationToken = default)
    {
        quantity = InventoryRules.NormalizeQuantity(quantity);
        if (expirationMode == ExpirationMode.Unknown)
        {
            throw new InventoryRuleException("Indica si el producto maneja fecha de caducidad.");
        }

        if (expirationMode == ExpirationMode.Tracked && expirationDate is null)
        {
            throw new InventoryRuleException("La fecha de caducidad del lote es obligatoria.");
        }

        return _database.WriteAsync(connection =>
        {
            var row = ProductRepository.GetRow(connection, businessId, productId)
                ?? throw new InventoryRuleException("El producto no existe.");
            if (row.Active != 1)
            {
                throw new InventoryRuleException("No se puede recibir stock de un producto inactivo.");
            }

            InventoryRules.ValidateQuantity(quantity, (UnitOfMeasure)row.UnitOfMeasure);
            var product = row.ToDomain();
            var resulting = InventoryRules.NormalizeQuantity(product.Stock + quantity);
            var now = SqliteValues.Date(DateTime.UtcNow);
            var reference = $"LOT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
            InventoryLotPersistence.Add(
                connection,
                productId,
                quantity,
                expirationMode == ExpirationMode.Tracked ? expirationDate : null,
                lotCode,
                now);
            connection.Execute(
                "UPDATE products SET stock_milli=?,expiration_mode=?,updated_at=? WHERE id=?;",
                SqliteValues.ToMilli(resulting),
                (int)expirationMode,
                now,
                productId);
            ProductRepository.InsertMovement(
                connection,
                businessId,
                productId,
                InventoryMovementType.Entry,
                quantity,
                product.Stock,
                resulting,
                reference,
                string.IsNullOrWhiteSpace(lotCode) ? "Entrada de mercancía" : $"Entrada de lote {lotCode.Trim()}",
                now);
            return ProductRepository.GetRow(connection, businessId, productId)!.ToDomain();
        }, cancellationToken);
    }

    public Task<Product> ClassifyUndatedStockAsync(
        long businessId,
        long productId,
        ExpirationMode expirationMode,
        DateOnly? expirationDate = null,
        string? lotCode = null,
        CancellationToken cancellationToken = default)
    {
        if (expirationMode == ExpirationMode.Unknown)
        {
            throw new InventoryRuleException("Indica si el producto maneja fecha de caducidad.");
        }

        if (expirationMode == ExpirationMode.Tracked && expirationDate is null)
        {
            throw new InventoryRuleException("La fecha de caducidad es obligatoria.");
        }

        return _database.WriteAsync(connection =>
        {
            _ = ProductRepository.GetRow(connection, businessId, productId)
                ?? throw new InventoryRuleException("El producto no existe.");
            var now = SqliteValues.Date(DateTime.UtcNow);
            connection.Execute(
                """
                UPDATE inventory_lots SET expiration_date=?,lot_code=COALESCE(?,lot_code),updated_at=?
                WHERE product_id=? AND quantity_milli>0 AND expiration_date IS NULL;
                """,
                expirationMode == ExpirationMode.Tracked
                    ? expirationDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : null,
                ProductRepository.DbText(lotCode),
                now,
                productId);
            connection.Execute(
                "UPDATE products SET expiration_mode=?,updated_at=? WHERE id=?;",
                (int)expirationMode,
                now,
                productId);
            return ProductRepository.GetRow(connection, businessId, productId)!.ToDomain();
        }, cancellationToken);
    }

    public Task<IReadOnlyList<InventoryLot>> GetLotsAsync(
        long businessId,
        long productId,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<InventoryLot>>(connection =>
        {
            if (ProductRepository.GetRow(connection, businessId, productId) is null)
            {
                throw new InventoryRuleException("El producto no existe.");
            }

            var rows = connection.Query<LotRow>(
                """
                SELECT id Id,product_id ProductId,lot_code LotCode,quantity_milli QuantityMilli,
                       expiration_date ExpirationDate,received_at ReceivedAt
                FROM inventory_lots WHERE product_id=? AND quantity_milli>0
                ORDER BY CASE WHEN expiration_date IS NULL THEN 1 ELSE 0 END,expiration_date,received_at,id;
                """,
                productId);
            return rows.Select(MapLot).ToArray();
        }, cancellationToken);

    public Task<IReadOnlyList<ExpirationAlert>> GetAlertsAsync(
        long businessId,
        int days = 30,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (days < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days));
        }

        return _database.ReadAsync<IReadOnlyList<ExpirationAlert>>(connection =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var rows = connection.Query<AlertRow>(
                """
                SELECT p.id ProductId,p.name ProductName,COALESCE(p.barcode,p.sku) Code,l.lot_code LotCode,
                       l.quantity_milli QuantityMilli,p.unit_of_measure UnitOfMeasure,l.expiration_date ExpirationDate
                FROM inventory_lots l JOIN products p ON p.id=l.product_id
                WHERE p.business_id=? AND p.active=1 AND p.expiration_mode=? AND l.quantity_milli>0
                  AND l.expiration_date IS NOT NULL AND date(l.expiration_date)<=date(?)
                ORDER BY date(l.expiration_date),p.name COLLATE NOCASE LIMIT ?;
                """,
                businessId,
                (int)ExpirationMode.Tracked,
                today.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                limit);
            return rows.Select(row => new ExpirationAlert(
                row.ProductId,
                row.ProductName,
                row.Code,
                row.LotCode,
                SqliteValues.FromMilli(row.QuantityMilli),
                (UnitOfMeasure)row.UnitOfMeasure,
                DateOnly.ParseExact(row.ExpirationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture))).ToArray();
        }, cancellationToken);
    }

    public Task<ExpirationSummary> GetSummaryAsync(
        long businessId,
        int days = 30,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync(connection =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var row = connection.Query<SummaryRow>(
                """
                SELECT
                  COUNT(DISTINCT CASE WHEN p.expiration_mode=1 AND l.quantity_milli>0 AND l.expiration_date IS NOT NULL AND date(l.expiration_date)<date(?) THEN p.id END) ExpiredProducts,
                  COUNT(DISTINCT CASE WHEN p.expiration_mode=1 AND l.quantity_milli>0 AND l.expiration_date IS NOT NULL AND date(l.expiration_date) BETWEEN date(?) AND date(?) THEN p.id END) ExpiringProducts,
                  COUNT(DISTINCT CASE WHEN p.expiration_mode=1 AND l.quantity_milli>0 AND l.expiration_date IS NULL THEN p.id END) MissingDateProducts,
                  COUNT(DISTINCT CASE WHEN p.expiration_mode=0 AND p.stock_milli>0 THEN p.id END) NeedsSetupProducts
                FROM products p LEFT JOIN inventory_lots l ON l.product_id=p.id
                WHERE p.business_id=? AND p.active=1;
                """,
                today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                today.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                businessId).Single();
            return new ExpirationSummary(
                row.ExpiredProducts,
                row.ExpiringProducts,
                row.MissingDateProducts,
                row.NeedsSetupProducts);
        }, cancellationToken);

    private static InventoryLot MapLot(LotRow row) => new()
    {
        Id = row.Id,
        ProductId = row.ProductId,
        LotCode = row.LotCode,
        Quantity = SqliteValues.FromMilli(row.QuantityMilli),
        ExpirationDate = row.ExpirationDate is null
            ? null
            : DateOnly.ParseExact(row.ExpirationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        ReceivedAt = SqliteValues.ParseDate(row.ReceivedAt)
    };

    private sealed class LotRow
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public string? LotCode { get; set; }
        public long QuantityMilli { get; set; }
        public string? ExpirationDate { get; set; }
        public string ReceivedAt { get; set; } = string.Empty;
    }

    private sealed class AlertRow
    {
        public long ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? LotCode { get; set; }
        public long QuantityMilli { get; set; }
        public int UnitOfMeasure { get; set; }
        public string ExpirationDate { get; set; } = string.Empty;
    }

    private sealed class SummaryRow
    {
        public int ExpiredProducts { get; set; }
        public int ExpiringProducts { get; set; }
        public int MissingDateProducts { get; set; }
        public int NeedsSetupProducts { get; set; }
    }
}
