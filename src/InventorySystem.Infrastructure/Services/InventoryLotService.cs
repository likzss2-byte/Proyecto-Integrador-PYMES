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

            var productExpirationMode = (ExpirationMode)row.ExpirationMode;
            if (input.ExpirationMode != productExpirationMode)
            {
                throw new InventoryRuleException(
                    "El modo de caducidad pertenece al producto. Modifícalo desde la ficha del producto antes de registrar el lote.");
            }
            ValidateLotDates(productExpirationMode, input.ManufacturingDate, input.ExpirationDate);
            InventoryRules.ValidateQuantity(quantity, (UnitOfMeasure)row.UnitOfMeasure);
            var product = row.ToDomain();
            var resulting = InventoryRules.NormalizeQuantity(product.Stock + quantity);
            var now = SqliteValues.Date(DateTime.UtcNow);
            var reference = $"LOT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
            var lotId = InventoryLotPersistence.Add(
                connection,
                input.ProductId,
                quantity,
                productExpirationMode == ExpirationMode.Tracked ? input.ExpirationDate : null,
                input.LotCode,
                now,
                input.SupplierId,
                input.ManufacturingDate,
                input.UnitCost,
                input.PurchaseOrderId,
                input.ReceiptId);
            connection.Execute(
                "UPDATE products SET stock_milli=?,updated_at=? WHERE id=?;",
                SqliteValues.ToMilli(resulting),
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
                SELECT l.id Id,l.product_id ProductId,p.name ProductName,COALESCE(p.barcode,'ID ' || p.id) ProductCode,p.expiration_mode ProductExpirationMode,
                       l.supplier_id SupplierId,s.company_name SupplierName,
                       l.lot_code LotCode,l.manufacturing_date ManufacturingDate,l.quantity_milli QuantityMilli,
                       l.initial_quantity_milli InitialQuantityMilli,l.unit_cost_basis UnitCostBasis,
                       l.expiration_date ExpirationDate,l.received_at ReceivedAt,l.status Status,
                       l.purchase_order_id PurchaseOrderId,l.receipt_id ReceiptId,l.created_at CreatedAt,l.updated_at UpdatedAt
                FROM inventory_lots l
                JOIN products p ON p.id=l.product_id
                LEFT JOIN suppliers s ON s.id=l.supplier_id
                WHERE l.product_id=? AND p.business_id=?
                ORDER BY CASE WHEN l.expiration_date IS NULL THEN 1 ELSE 0 END,l.expiration_date,l.received_at,l.id;
                """,
                productId,
                businessId);
            return rows.Select(MapLot).ToArray();
        }, cancellationToken);

    public Task<InventoryLot?> GetAsync(
        long businessId,
        long lotId,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync(connection =>
        {
            var row = QueryLot(connection, businessId, lotId);
            return row is null ? null : MapLot(row);
        }, cancellationToken);

    public Task<IReadOnlyList<InventoryLot>> GetAllAsync(
        long businessId,
        string? search = null,
        int limit = 300,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<InventoryLot>>(connection =>
        {
            var normalized = (search ?? string.Empty).Trim();
            var term = $"%{normalized}%";
            var rows = connection.Query<LotRow>(
                """
                SELECT l.id Id,l.product_id ProductId,p.name ProductName,COALESCE(p.barcode,'ID ' || p.id) ProductCode,p.expiration_mode ProductExpirationMode,
                       l.supplier_id SupplierId,s.company_name SupplierName,
                       l.lot_code LotCode,l.manufacturing_date ManufacturingDate,l.quantity_milli QuantityMilli,
                       l.initial_quantity_milli InitialQuantityMilli,l.unit_cost_basis UnitCostBasis,
                       l.expiration_date ExpirationDate,l.received_at ReceivedAt,l.status Status,
                       l.purchase_order_id PurchaseOrderId,l.receipt_id ReceiptId,l.created_at CreatedAt,l.updated_at UpdatedAt
                FROM inventory_lots l
                JOIN products p ON p.id=l.product_id
                LEFT JOIN suppliers s ON s.id=l.supplier_id
                WHERE p.business_id=?
                  AND (?='' OR p.name LIKE ? OR p.barcode LIKE ? OR l.lot_code LIKE ? OR s.company_name LIKE ?)
                ORDER BY l.received_at DESC,l.id DESC
                LIMIT ?;
                """,
                businessId,
                normalized,
                term,
                term,
                term,
                term,
                limit);
            return rows.Select(MapLot).ToArray();
        }, cancellationToken);

    public Task<InventoryLot> UpdateAsync(
        long businessId,
        long lotId,
        InventoryLotUpdateInput input,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var existing = QueryLot(connection, businessId, lotId)
                ?? throw new InventoryRuleException("El lote no existe.");
            var product = ProductRepository.GetRow(connection, businessId, existing.ProductId)
                ?? throw new InventoryRuleException("El producto del lote ya no existe.");
            var expirationMode = (ExpirationMode)product.ExpirationMode;
            ValidateLotDates(expirationMode, input.ManufacturingDate, input.ExpirationDate);
            var expiration = expirationMode == ExpirationMode.Tracked ? input.ExpirationDate : null;
            if (input.UnitCost < 0)
            {
                throw new InventoryRuleException("El costo unitario no puede ser negativo.");
            }

            if (input.SupplierId.HasValue)
            {
                var supplier = SupplierRepository.GetRow(connection, businessId, input.SupplierId.Value)
                    ?? throw new InventoryRuleException("El proveedor no existe.");
                if (supplier.Active != 1 && input.SupplierId != existing.SupplierId)
                {
                    throw new InventoryRuleException("No se puede asignar un proveedor archivado a un lote nuevo.");
                }
            }

            var lotCode = ProductRepository.DbText(input.LotCode) as string;
            if (lotCode is not null && connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM inventory_lots WHERE product_id=? AND lot_code=? COLLATE NOCASE AND id<>?;",
                    existing.ProductId,
                    lotCode,
                    lotId) > 0)
            {
                throw new InventoryRuleException("Ya existe otro lote de este producto con el mismo código.");
            }

            var now = SqliteValues.Date(DateTime.UtcNow);
            connection.Execute(
                """
                UPDATE inventory_lots
                SET lot_code=?,supplier_id=?,manufacturing_date=?,expiration_date=?,unit_cost_basis=?,updated_at=?
                WHERE id=?;
                """,
                lotCode,
                input.SupplierId,
                input.ManufacturingDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                expiration?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                input.UnitCost.HasValue ? SqliteValues.ToMoney(input.UnitCost.Value) : null,
                now,
                lotId);
            InventoryLotPersistence.LinkSupplier(
                connection,
                existing.ProductId,
                input.SupplierId,
                input.UnitCost,
                now);
            return MapLot(QueryLot(connection, businessId, lotId)!);
        }, cancellationToken);

    public Task<InventoryLot> AdjustQuantityAsync(
        long businessId,
        InventoryLotAdjustmentInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new InventoryRuleException("El motivo del ajuste es obligatorio.");
        }

        var change = InventoryRules.NormalizeQuantity(input.QuantityChange);
        if (change == 0)
        {
            throw new InventoryRuleException("El ajuste debe ser diferente de cero.");
        }

        return _database.WriteAsync(connection =>
        {
            var lot = QueryLot(connection, businessId, input.LotId)
                ?? throw new InventoryRuleException("El lote no existe.");
            var productRow = ProductRepository.GetRow(connection, businessId, lot.ProductId)
                ?? throw new InventoryRuleException("El producto del lote ya no existe.");
            InventoryRules.ValidateQuantity(decimal.Abs(change), (UnitOfMeasure)productRow.UnitOfMeasure, "El ajuste");
            var currentLotQuantity = SqliteValues.FromMilli(lot.QuantityMilli);
            var resultingLot = InventoryRules.NormalizeQuantity(currentLotQuantity + change);
            if (resultingLot < 0)
            {
                throw new InventoryRuleException($"El lote solo tiene {currentLotQuantity:0.###} disponibles.");
            }

            var product = productRow.ToDomain();
            var resultingStock = InventoryRules.NormalizeQuantity(product.Stock + change);
            if (resultingStock < 0)
            {
                throw new InventoryRuleException("El ajuste dejaría el inventario total en negativo.");
            }

            var now = SqliteValues.Date(DateTime.UtcNow);
            connection.Execute(
                """
                UPDATE inventory_lots
                SET quantity_milli=?,status=CASE WHEN ?=0 THEN 1 ELSE 0 END,updated_at=?
                WHERE id=?;
                """,
                SqliteValues.ToMilli(resultingLot),
                SqliteValues.ToMilli(resultingLot),
                now,
                input.LotId);
            connection.Execute(
                "UPDATE products SET stock_milli=?,updated_at=? WHERE id=?;",
                SqliteValues.ToMilli(resultingStock),
                now,
                product.Id);
            var movementId = ProductRepository.InsertMovement(
                connection,
                businessId,
                product.Id,
                change > 0 ? InventoryMovementType.PositiveAdjustment : InventoryMovementType.NegativeAdjustment,
                change,
                product.Stock,
                resultingStock,
                $"AJUSTE-LOTE-{input.LotId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                input.Reason.Trim(),
                now);
            InventoryLotPersistence.RecordMovementAllocations(
                connection,
                movementId,
                [new LotAllocation(input.LotId, decimal.Abs(change))]);
            return MapLot(QueryLot(connection, businessId, input.LotId)!);
        }, cancellationToken);
    }

    private static LotRow? QueryLot(SQLite.SQLiteConnection connection, long businessId, long lotId) =>
        connection.Query<LotRow>(
                """
                SELECT l.id Id,l.product_id ProductId,p.name ProductName,COALESCE(p.barcode,'ID ' || p.id) ProductCode,p.expiration_mode ProductExpirationMode,
                       l.supplier_id SupplierId,s.company_name SupplierName,
                       l.lot_code LotCode,l.manufacturing_date ManufacturingDate,l.quantity_milli QuantityMilli,
                       l.initial_quantity_milli InitialQuantityMilli,l.unit_cost_basis UnitCostBasis,
                       l.expiration_date ExpirationDate,l.received_at ReceivedAt,l.status Status,
                       l.purchase_order_id PurchaseOrderId,l.receipt_id ReceiptId,l.created_at CreatedAt,l.updated_at UpdatedAt
                FROM inventory_lots l
                JOIN products p ON p.id=l.product_id
                LEFT JOIN suppliers s ON s.id=l.supplier_id
                WHERE l.id=? AND p.business_id=? LIMIT 1;
                """,
                lotId,
                businessId)
            .FirstOrDefault();

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
                SELECT p.id ProductId,p.name ProductName,COALESCE(p.barcode,'ID ' || p.id) Code,l.lot_code LotCode,
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
        ProductName = row.ProductName,
        ProductCode = row.ProductCode,
        ProductExpirationMode = (ExpirationMode)row.ProductExpirationMode,
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
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public int ProductExpirationMode { get; set; }
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
