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
        CancellationToken cancellationToken = default) =>
        ReceiveAsync(
            businessId,
            new InventoryLotReceiptInput(productId, quantity, expirationMode, expirationDate, lotCode),
            cancellationToken);

    public Task<Product> ReceiveAsync(
        long businessId,
        InventoryLotReceiptInput input,
        CancellationToken cancellationToken = default)
    {
        var quantity = InventoryRules.NormalizeQuantity(input.Quantity);
        ValidateLotDates(input.ExpirationMode, input.ManufacturingDate, input.ExpirationDate);
        if (input.UnitCost < 0)
        {
            throw new InventoryRuleException("El costo unitario no puede ser negativo.");
        }

        return _database.WriteAsync(connection =>
        {
            var row = ProductRepository.GetRow(connection, businessId, input.ProductId)
                ?? throw new InventoryRuleException("El producto no existe.");
            if (row.Active != 1)
            {
                throw new InventoryRuleException("No se puede recibir stock de un producto inactivo.");
            }

            if (input.SupplierId.HasValue &&
                SupplierRepository.GetRow(connection, businessId, input.SupplierId.Value) is not { Active: 1 })
            {
                throw new InventoryRuleException("El proveedor no existe o está inactivo.");
            }

            InventoryRules.ValidateQuantity(quantity, (UnitOfMeasure)row.UnitOfMeasure);
            var product = row.ToDomain();
            var resulting = InventoryRules.NormalizeQuantity(product.Stock + quantity);
            var now = SqliteValues.Date(DateTime.UtcNow);
            var reference = $"LOT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
            var lotId = InventoryLotPersistence.Add(
                connection,
                input.ProductId,
                quantity,
                input.ExpirationMode == ExpirationMode.Tracked ? input.ExpirationDate : null,
                input.LotCode,
                now,
                input.SupplierId,
                input.ManufacturingDate,
                input.UnitCost,
                input.PurchaseOrderId,
                input.ReceiptId);
            connection.Execute(
                "UPDATE products SET stock_milli=?,expiration_mode=?,updated_at=? WHERE id=?;",
                SqliteValues.ToMilli(resulting),
                (int)input.ExpirationMode,
                now,
                input.ProductId);
            var movementId = ProductRepository.InsertMovement(
                connection,
                businessId,
                input.ProductId,
                InventoryMovementType.Entry,
                quantity,
                product.Stock,
                resulting,
                reference,
                string.IsNullOrWhiteSpace(input.LotCode)
                    ? "Entrada de mercancía"
                    : $"Entrada de lote {input.LotCode.Trim()}",
                now);
            InventoryLotPersistence.RecordMovementAllocations(
                connection,
                movementId,
                [new LotAllocation(lotId, quantity)]);
            return ProductRepository.GetRow(connection, businessId, input.ProductId)!.ToDomain();
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
        ValidateLotDates(expirationMode, null, expirationDate);
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
                SELECT l.id Id,l.product_id ProductId,l.supplier_id SupplierId,s.company_name SupplierName,
                       l.lot_code LotCode,l.manufacturing_date ManufacturingDate,l.quantity_milli QuantityMilli,
                       l.initial_quantity_milli InitialQuantityMilli,l.unit_cost_basis UnitCostBasis,
                       l.expiration_date ExpirationDate,l.received_at ReceivedAt,l.status Status,
                       l.purchase_order_id PurchaseOrderId,l.receipt_id ReceiptId,l.created_at CreatedAt,l.updated_at UpdatedAt
                FROM inventory_lots l LEFT JOIN suppliers s ON s.id=l.supplier_id
                WHERE l.product_id=?
                ORDER BY CASE WHEN l.expiration_date IS NULL THEN 1 ELSE 0 END,l.expiration_date,l.received_at,l.id;
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

        var today = DateOnly.FromDateTime(DateTime.Today);
        return QueryAlertsAsync(businessId, null, today.AddDays(days), limit, cancellationToken);
    }

    public Task<IReadOnlyList<ExpirationAlert>> GetExpiringAsync(
        long businessId,
        int days = 7,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (days < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days));
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        return QueryAlertsAsync(businessId, today, today.AddDays(days), limit, cancellationToken);
    }

    public Task<IReadOnlyList<ExpirationAlert>> GetExpiredAsync(
        long businessId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var yesterday = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        return QueryAlertsAsync(businessId, null, yesterday, limit, cancellationToken);
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

    private Task<IReadOnlyList<ExpirationAlert>> QueryAlertsAsync(
        long businessId,
        DateOnly? from,
        DateOnly through,
        int limit,
        CancellationToken cancellationToken) =>
        _database.ReadAsync<IReadOnlyList<ExpirationAlert>>(connection =>
        {
            var rows = connection.Query<AlertRow>(
                """
                SELECT p.id ProductId,p.name ProductName,COALESCE(p.barcode,p.sku) Code,l.lot_code LotCode,
                       l.quantity_milli QuantityMilli,p.unit_of_measure UnitOfMeasure,l.expiration_date ExpirationDate,
                       s.company_name SupplierName
                FROM inventory_lots l
                JOIN products p ON p.id=l.product_id
                LEFT JOIN suppliers s ON s.id=l.supplier_id
                WHERE p.business_id=? AND p.active=1 AND p.expiration_mode=? AND l.quantity_milli>0
                  AND l.expiration_date IS NOT NULL
                  AND (? IS NULL OR date(l.expiration_date)>=date(?))
                  AND date(l.expiration_date)<=date(?)
                ORDER BY date(l.expiration_date),p.name COLLATE NOCASE LIMIT ?;
                """,
                businessId,
                (int)ExpirationMode.Tracked,
                from?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                from?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                through.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                limit);
            return rows.Select(row => new ExpirationAlert(
                row.ProductId,
                row.ProductName,
                row.Code,
                row.LotCode,
                SqliteValues.FromMilli(row.QuantityMilli),
                (UnitOfMeasure)row.UnitOfMeasure,
                DateOnly.ParseExact(row.ExpirationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.SupplierName)).ToArray();
        }, cancellationToken);

    private static InventoryLot MapLot(LotRow row) => new()
    {
        Id = row.Id,
        ProductId = row.ProductId,
        SupplierId = row.SupplierId,
        SupplierName = row.SupplierName,
        LotCode = row.LotCode,
        ManufacturingDate = ParseDateOnly(row.ManufacturingDate),
        Quantity = SqliteValues.FromMilli(row.QuantityMilli),
        InitialQuantity = SqliteValues.FromMilli(row.InitialQuantityMilli),
        UnitCost = row.UnitCostBasis.HasValue ? SqliteValues.FromMoney(row.UnitCostBasis.Value) : null,
        ExpirationDate = ParseDateOnly(row.ExpirationDate),
        ReceivedAt = SqliteValues.ParseDate(row.ReceivedAt),
        Status = (InventoryLotStatus)row.Status,
        PurchaseOrderId = row.PurchaseOrderId,
        ReceiptId = row.ReceiptId,
        CreatedAt = SqliteValues.ParseDate(row.CreatedAt),
        UpdatedAt = SqliteValues.ParseDate(row.UpdatedAt)
    };

    private static DateOnly? ParseDateOnly(string? value) => value is null
        ? null
        : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static void ValidateLotDates(
        ExpirationMode expirationMode,
        DateOnly? manufacturingDate,
        DateOnly? expirationDate)
    {
        if (expirationMode == ExpirationMode.Unknown)
        {
            throw new InventoryRuleException("Indica si el producto maneja fecha de caducidad.");
        }

        if (expirationMode == ExpirationMode.Tracked && expirationDate is null)
        {
            throw new InventoryRuleException("La fecha de caducidad del lote es obligatoria.");
        }

        if (manufacturingDate > DateOnly.FromDateTime(DateTime.Today))
        {
            throw new InventoryRuleException("La fecha de fabricación no puede estar en el futuro.");
        }

        if (manufacturingDate.HasValue && expirationDate.HasValue && expirationDate < manufacturingDate)
        {
            throw new InventoryRuleException("La caducidad no puede ser anterior a la fabricación.");
        }
    }

    private sealed class LotRow
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public long? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? LotCode { get; set; }
        public string? ManufacturingDate { get; set; }
        public long QuantityMilli { get; set; }
        public long InitialQuantityMilli { get; set; }
        public long? UnitCostBasis { get; set; }
        public string? ExpirationDate { get; set; }
        public string ReceivedAt { get; set; } = string.Empty;
        public int Status { get; set; }
        public long? PurchaseOrderId { get; set; }
        public long? ReceiptId { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
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
        public string? SupplierName { get; set; }
    }

    private sealed class SummaryRow
    {
        public int ExpiredProducts { get; set; }
        public int ExpiringProducts { get; set; }
        public int MissingDateProducts { get; set; }
        public int NeedsSetupProducts { get; set; }
    }
}
