using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using InventorySystem.Infrastructure.Repositories;
using SQLite;

namespace InventorySystem.Infrastructure.Services;

public sealed class PurchaseOrderService
{
    private readonly InventoryDatabase _database;

    public PurchaseOrderService(InventoryDatabase database)
    {
        _database = database;
    }

    public Task<PurchaseOrder> CreateAsync(
        long businessId,
        PurchaseOrderInput input,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection => SaveInTransaction(connection, businessId, null, input), cancellationToken);

    public Task<PurchaseOrder> UpdateAsync(
        long businessId,
        long orderId,
        PurchaseOrderInput input,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection => SaveInTransaction(connection, businessId, orderId, input), cancellationToken);

    public Task<PurchaseOrder> ConfirmAsync(
        long businessId,
        long orderId,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var order = GetOrder(connection, businessId, orderId)
                ?? throw new InventoryRuleException("El pedido no existe.");
            if (order.Status is PurchaseOrderStatus.Confirmed or PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Received)
            {
                throw new InventoryRuleException("El pedido ya fue confirmado.");
            }

            if (order.Status == PurchaseOrderStatus.Cancelled)
            {
                throw new InventoryRuleException("Un pedido cancelado no se puede confirmar.");
            }

            var now = SqliteValues.Date(DateTime.UtcNow);
            var changed = connection.Execute(
                """
                UPDATE purchase_orders SET status=?,confirmed_at=?,updated_at=?
                WHERE id=? AND business_id=? AND status IN (?,?);
                """,
                (int)PurchaseOrderStatus.Confirmed,
                now,
                now,
                orderId,
                businessId,
                (int)PurchaseOrderStatus.Draft,
                (int)PurchaseOrderStatus.Pending);
            if (changed != 1)
            {
                throw new InventoryRuleException("El pedido cambió de estado y no pudo confirmarse.");
            }

            return GetOrder(connection, businessId, orderId)!;
        }, cancellationToken);

    public Task<PurchaseOrder> CancelAsync(
        long businessId,
        long orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InventoryRuleException("El motivo de cancelación es obligatorio.");
        }

        return _database.WriteAsync(connection =>
        {
            var order = GetOrder(connection, businessId, orderId)
                ?? throw new InventoryRuleException("El pedido no existe.");
            if (order.Status == PurchaseOrderStatus.Cancelled)
            {
                throw new InventoryRuleException("El pedido ya fue cancelado.");
            }

            if (order.Status is PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Received)
            {
                throw new InventoryRuleException("Un pedido con recepciones no puede cancelarse destruyendo su historial.");
            }

            var now = SqliteValues.Date(DateTime.UtcNow);
            connection.Execute(
                """
                UPDATE purchase_orders
                SET status=?,cancelled_at=?,updated_at=?,notes=trim(COALESCE(notes,'') || ?)
                WHERE id=? AND business_id=?;
                """,
                (int)PurchaseOrderStatus.Cancelled,
                now,
                now,
                $"\nCancelación: {reason.Trim()}",
                orderId,
                businessId);
            return GetOrder(connection, businessId, orderId)!;
        }, cancellationToken);
    }

    public Task<PurchaseReceipt> ReceiveAsync(
        long businessId,
        long orderId,
        IEnumerable<PurchaseReceiptInput> lines,
        string operationKey,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var inputs = lines?.ToList() ?? [];
        if (inputs.Count == 0)
        {
            throw new InventoryRuleException("La recepción debe incluir al menos un producto.");
        }

        if (inputs.GroupBy(line => line.OrderLineId).Any(group => group.Count() > 1))
        {
            throw new InventoryRuleException("La recepción contiene detalles repetidos.");
        }

        var normalizedOperationKey = (operationKey ?? string.Empty).Trim();
        if (normalizedOperationKey.Length == 0)
        {
            throw new InventoryRuleException("La clave de la recepción es obligatoria.");
        }

        return _database.WriteAsync(connection =>
        {
            var existingReceipt = connection.Query<ReceiptKeyRow>(
                    """
                    SELECT id Id,order_id OrderId FROM purchase_receipts
                    WHERE business_id=? AND operation_key=? COLLATE NOCASE LIMIT 1;
                    """,
                    businessId,
                    normalizedOperationKey)
                .FirstOrDefault();
            if (existingReceipt is not null)
            {
                if (existingReceipt.OrderId != orderId)
                {
                    throw new InventoryRuleException("La clave de recepción ya pertenece a otro pedido.");
                }

                return GetReceipt(connection, businessId, existingReceipt.Id)!;
            }

            var order = GetOrder(connection, businessId, orderId)
                ?? throw new InventoryRuleException("El pedido no existe.");
            if (order.Status == PurchaseOrderStatus.Cancelled)
            {
                throw new InventoryRuleException("Un pedido cancelado no puede recibirse.");
            }

            if (order.Status == PurchaseOrderStatus.Received)
            {
                throw new InventoryRuleException("El pedido ya fue recibido por completo.");
            }

            if (order.Status == PurchaseOrderStatus.Draft)
            {
                throw new InventoryRuleException("Confirma o deja pendiente el pedido antes de recibir mercancía.");
            }

            var nowUtc = DateTime.UtcNow;
            var now = SqliteValues.Date(nowUtc);
            var reference = $"REC-{nowUtc:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            connection.Execute(
                """
                INSERT INTO purchase_receipts(business_id,order_id,reference,operation_key,notes,received_at,created_at)
                VALUES(?,?,?,?,?,?,?);
                """,
                businessId,
                orderId,
                reference,
                normalizedOperationKey,
                ProductRepository.DbText(notes),
                now,
                now);
            var receiptId = connection.ExecuteScalar<long>("SELECT last_insert_rowid();");

            foreach (var input in inputs)
            {
                var orderLine = order.Lines.SingleOrDefault(line => line.Id == input.OrderLineId)
                    ?? throw new InventoryRuleException("Uno de los detalles no pertenece al pedido.");
                var quantity = InventoryRules.NormalizeQuantity(input.Quantity);
                InventoryRules.ValidateQuantity(quantity, orderLine.UnitOfMeasure, "La cantidad recibida");
                if (quantity > orderLine.PendingQuantity)
                {
                    throw new InventoryRuleException(
                        $"La cantidad recibida de {orderLine.Description} excede la pendiente ({orderLine.PendingQuantity:0.###}).");
                }

                var productRow = ProductRepository.GetRow(connection, businessId, input.ProductId)
                    ?? throw new InventoryRuleException("Vincula cada concepto recibido con un producto existente.");
                if (productRow.Active != 1)
                {
                    throw new InventoryRuleException($"El producto {productRow.Name} está inactivo.");
                }

                if (orderLine.ProductId.HasValue && orderLine.ProductId.Value != input.ProductId)
                {
                    throw new InventoryRuleException("El producto recibido no coincide con el detalle del pedido.");
                }

                if ((UnitOfMeasure)productRow.UnitOfMeasure != orderLine.UnitOfMeasure)
                {
                    throw new InventoryRuleException("La unidad del producto no coincide con la unidad solicitada.");
                }

                var expirationMode = (ExpirationMode)productRow.ExpirationMode;
                ValidateReceiptDates(expirationMode, input.ManufacturingDate, input.ExpirationDate);
                if (input.UnitCost < 0)
                {
                    throw new InventoryRuleException("El costo unitario recibido no puede ser negativo.");
                }

                var previousStock = SqliteValues.FromMilli(productRow.StockMilli);
                var resultingStock = InventoryRules.NormalizeQuantity(previousStock + quantity);
                var lotId = InventoryLotPersistence.Add(
                    connection,
                    input.ProductId,
                    quantity,
                    expirationMode == ExpirationMode.Tracked ? input.ExpirationDate : null,
                    input.LotCode,
                    now,
                    order.SupplierId,
                    input.ManufacturingDate,
                    input.UnitCost ?? orderLine.EstimatedUnitCost,
                    orderId,
                    receiptId);
                connection.Execute(
                    "UPDATE products SET stock_milli=?,updated_at=? WHERE id=?;",
                    SqliteValues.ToMilli(resultingStock),
                    now,
                    input.ProductId);
                var movementId = ProductRepository.InsertMovement(
                    connection,
                    businessId,
                    input.ProductId,
                    InventoryMovementType.PurchaseReceipt,
                    quantity,
                    previousStock,
                    resultingStock,
                    reference,
                    $"Recepción del pedido {order.Folio}",
                    now);
                InventoryLotPersistence.RecordMovementAllocations(
                    connection,
                    movementId,
                    [new LotAllocation(lotId, quantity)]);

                connection.Execute(
                    """
                    INSERT INTO purchase_receipt_lines(
                        receipt_id,order_line_id,product_id,lot_id,quantity_milli,unit_cost_basis)
                    VALUES(?,?,?,?,?,?);
                    """,
                    receiptId,
                    orderLine.Id,
                    input.ProductId,
                    lotId,
                    SqliteValues.ToMilli(quantity),
                    input.UnitCost.HasValue ? SqliteValues.ToMoney(input.UnitCost.Value) : null);
                connection.Execute(
                    """
                    UPDATE purchase_order_lines
                    SET product_id=COALESCE(product_id,?),received_milli=received_milli+?,updated_at=?
                    WHERE id=? AND order_id=? AND received_milli+?<=requested_milli;
                    """,
                    input.ProductId,
                    SqliteValues.ToMilli(quantity),
                    now,
                    orderLine.Id,
                    orderId,
                    SqliteValues.ToMilli(quantity));
            }

            var pendingLines = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM purchase_order_lines WHERE order_id=? AND received_milli<requested_milli;",
                orderId);
            var receivedLines = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM purchase_order_lines WHERE order_id=? AND received_milli>0;",
                orderId);
            var newStatus = pendingLines == 0
                ? PurchaseOrderStatus.Received
                : receivedLines > 0
                    ? PurchaseOrderStatus.PartiallyReceived
                    : PurchaseOrderStatus.Confirmed;
            connection.Execute(
                "UPDATE purchase_orders SET status=?,updated_at=? WHERE id=? AND business_id=?;",
                (int)newStatus,
                now,
                orderId,
                businessId);

            return GetReceipt(connection, businessId, receiptId)!;
        }, cancellationToken);
    }

    public Task<PurchaseOrder?> GetAsync(
        long businessId,
        long orderId,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync(connection => GetOrder(connection, businessId, orderId), cancellationToken);

    public Task<IReadOnlyList<PurchaseOrder>> GetOrdersAsync(
        long businessId,
        bool includeCompleted = true,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<PurchaseOrder>>(connection =>
        {
            var rows = includeCompleted
                ? connection.Query<OrderRow>(OrderSelect + " WHERE o.business_id=? ORDER BY o.order_date DESC,o.id DESC;", businessId)
                : connection.Query<OrderRow>(
                    OrderSelect + " WHERE o.business_id=? AND o.status IN (1,2,3) ORDER BY o.order_date,o.id;",
                    businessId);
            return rows.Select(row => MapOrder(connection, row)).ToArray();
        }, cancellationToken);

    private static PurchaseOrder SaveInTransaction(
        SQLiteConnection connection,
        long businessId,
        long? orderId,
        PurchaseOrderInput input)
    {
        var requestedLines = input.Lines?.ToList() ?? [];
        if (requestedLines.Count == 0)
        {
            throw new InventoryRuleException("El pedido debe incluir al menos un concepto.");
        }

        if (input.InitialStatus is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Pending))
        {
            throw new InventoryRuleException("Un pedido nuevo solo puede guardarse como borrador o pendiente.");
        }

        if (input.EstimatedDate.HasValue && input.EstimatedDate < input.OrderDate)
        {
            throw new InventoryRuleException("La fecha estimada no puede ser anterior a la fecha del pedido.");
        }

        if (!input.SupplierId.HasValue && string.IsNullOrWhiteSpace(input.ManualSupplierName))
        {
            throw new InventoryRuleException("Selecciona un proveedor o escribe una referencia manual.");
        }

        if (input.SupplierId.HasValue &&
            SupplierRepository.GetRow(connection, businessId, input.SupplierId.Value) is not { Active: 1 })
        {
            throw new InventoryRuleException("El proveedor no existe o está inactivo.");
        }

        if (orderId.HasValue)
        {
            var existing = GetOrder(connection, businessId, orderId.Value)
                ?? throw new InventoryRuleException("El pedido no existe.");
            if (existing.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Pending))
            {
                throw new InventoryRuleException("Solo se pueden editar pedidos en borrador o pendientes sin recepciones.");
            }
        }

        var normalizedLines = new List<NormalizedOrderLine>(requestedLines.Count);
        foreach (var line in requestedLines)
        {
            var quantity = InventoryRules.NormalizeQuantity(line.Quantity);
            ProductRow? product = null;
            if (line.ProductId.HasValue)
            {
                product = ProductRepository.GetRow(connection, businessId, line.ProductId.Value)
                    ?? throw new InventoryRuleException("Uno de los productos del pedido no existe.");
                if (product.Active != 1)
                {
                    throw new InventoryRuleException($"El producto {product.Name} está inactivo.");
                }

                if ((UnitOfMeasure)product.UnitOfMeasure != line.UnitOfMeasure)
                {
                    throw new InventoryRuleException($"La unidad de {product.Name} no coincide con el pedido.");
                }
            }

            InventoryRules.ValidateQuantity(quantity, line.UnitOfMeasure, "La cantidad solicitada");
            var description = string.IsNullOrWhiteSpace(line.Description) ? product?.Name : line.Description.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new InventoryRuleException("La descripción manual es obligatoria cuando no hay un producto registrado.");
            }

            if (line.EstimatedUnitCost < 0)
            {
                throw new InventoryRuleException("El costo estimado no puede ser negativo.");
            }

            normalizedLines.Add(new NormalizedOrderLine(
                product?.Id,
                description,
                InventoryRules.NormalizeBarcode(line.Barcode) ?? product?.Barcode,
                string.IsNullOrWhiteSpace(line.Sku) ? product?.Sku : InventoryRules.NormalizeSku(line.Sku),
                quantity,
                line.UnitOfMeasure,
                line.EstimatedUnitCost.HasValue
                    ? decimal.Round(line.EstimatedUnitCost.Value, 4, MidpointRounding.AwayFromZero)
                    : null,
                string.IsNullOrWhiteSpace(line.Notes) ? null : line.Notes.Trim()));
        }

        var nowUtc = DateTime.UtcNow;
        var now = SqliteValues.Date(nowUtc);
        var folio = string.IsNullOrWhiteSpace(input.Folio)
            ? $"PED-{nowUtc:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}"
            : input.Folio.Trim();
        var total = normalizedLines.Sum(line => line.Quantity * (line.EstimatedUnitCost ?? 0m));
        long id;
        if (orderId.HasValue)
        {
            id = orderId.Value;
            connection.Execute(
                """
                UPDATE purchase_orders SET folio=?,supplier_id=?,manual_supplier_name=?,order_date=?,estimated_date=?,
                    status=?,notes=?,total_basis=?,updated_at=? WHERE id=? AND business_id=?;
                """,
                folio,
                input.SupplierId,
                ProductRepository.DbText(input.ManualSupplierName),
                input.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                input.EstimatedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                (int)input.InitialStatus,
                ProductRepository.DbText(input.Notes),
                SqliteValues.ToMoney(total),
                now,
                id,
                businessId);
            connection.Execute("DELETE FROM purchase_order_lines WHERE order_id=?;", id);
        }
        else
        {
            connection.Execute(
                """
                INSERT INTO purchase_orders(
                    business_id,folio,supplier_id,manual_supplier_name,order_date,estimated_date,status,notes,
                    total_basis,created_at,updated_at,confirmed_at,cancelled_at)
                VALUES(?,?,?,?,?,?,?,?,?,?,?,NULL,NULL);
                """,
                businessId,
                folio,
                input.SupplierId,
                ProductRepository.DbText(input.ManualSupplierName),
                input.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                input.EstimatedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                (int)input.InitialStatus,
                ProductRepository.DbText(input.Notes),
                SqliteValues.ToMoney(total),
                now,
                now);
            id = connection.ExecuteScalar<long>("SELECT last_insert_rowid();");
        }

        foreach (var line in normalizedLines)
        {
            connection.Execute(
                """
                INSERT INTO purchase_order_lines(
                    order_id,product_id,manual_description,barcode,sku,requested_milli,received_milli,
                    unit_of_measure,estimated_cost_basis,notes,created_at,updated_at)
                VALUES(?,?,?,?,?,?,0,?,?,?,?,?);
                """,
                id,
                line.ProductId,
                line.Description,
                ProductRepository.DbText(line.Barcode),
                ProductRepository.DbText(line.Sku),
                SqliteValues.ToMilli(line.Quantity),
                (int)line.UnitOfMeasure,
                line.EstimatedUnitCost.HasValue ? SqliteValues.ToMoney(line.EstimatedUnitCost.Value) : null,
                ProductRepository.DbText(line.Notes),
                now,
                now);
        }

        return GetOrder(connection, businessId, id)!;
    }

    private static PurchaseOrder? GetOrder(SQLiteConnection connection, long businessId, long orderId)
    {
        var row = connection.Query<OrderRow>(
            OrderSelect + " WHERE o.id=? AND o.business_id=? LIMIT 1;",
            orderId,
            businessId).FirstOrDefault();
        return row is null ? null : MapOrder(connection, row);
    }

    private static PurchaseOrder MapOrder(SQLiteConnection connection, OrderRow row)
    {
        var lines = connection.Query<OrderLineRow>(
            """
            SELECT l.id Id,l.order_id OrderId,l.product_id ProductId,
                   COALESCE(p.name,l.manual_description) Description,l.barcode Barcode,l.sku Sku,
                   l.requested_milli RequestedMilli,l.received_milli ReceivedMilli,
                   l.unit_of_measure UnitOfMeasure,l.estimated_cost_basis EstimatedCostBasis,l.notes Notes
            FROM purchase_order_lines l LEFT JOIN products p ON p.id=l.product_id
            WHERE l.order_id=? ORDER BY l.id;
            """,
            row.Id);
        return new PurchaseOrder
        {
            Id = row.Id,
            BusinessId = row.BusinessId,
            Folio = row.Folio,
            SupplierId = row.SupplierId,
            SupplierName = row.SupplierName,
            ManualSupplierName = row.ManualSupplierName,
            OrderDate = DateOnly.ParseExact(row.OrderDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            EstimatedDate = row.EstimatedDate is null
                ? null
                : DateOnly.ParseExact(row.EstimatedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Status = (PurchaseOrderStatus)row.Status,
            Notes = row.Notes,
            EstimatedTotal = SqliteValues.FromMoney(row.TotalBasis),
            CreatedAt = SqliteValues.ParseDate(row.CreatedAt),
            UpdatedAt = SqliteValues.ParseDate(row.UpdatedAt),
            ConfirmedAt = row.ConfirmedAt is null ? null : SqliteValues.ParseDate(row.ConfirmedAt),
            CancelledAt = row.CancelledAt is null ? null : SqliteValues.ParseDate(row.CancelledAt),
            Lines = lines.Select(line => new PurchaseOrderLine
            {
                Id = line.Id,
                OrderId = line.OrderId,
                ProductId = line.ProductId,
                Description = line.Description,
                Barcode = line.Barcode,
                Sku = line.Sku,
                RequestedQuantity = SqliteValues.FromMilli(line.RequestedMilli),
                ReceivedQuantity = SqliteValues.FromMilli(line.ReceivedMilli),
                UnitOfMeasure = (UnitOfMeasure)line.UnitOfMeasure,
                EstimatedUnitCost = line.EstimatedCostBasis.HasValue
                    ? SqliteValues.FromMoney(line.EstimatedCostBasis.Value)
                    : null,
                Notes = line.Notes
            }).ToList()
        };
    }

    private static PurchaseReceipt? GetReceipt(SQLiteConnection connection, long businessId, long receiptId)
    {
        var row = connection.Query<ReceiptRow>(
            """
            SELECT id Id,business_id BusinessId,order_id OrderId,reference Reference,operation_key OperationKey,
                   notes Notes,received_at ReceivedAt,created_at CreatedAt
            FROM purchase_receipts WHERE id=? AND business_id=? LIMIT 1;
            """,
            receiptId,
            businessId).FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        var lines = connection.Query<ReceiptLineRow>(
            """
            SELECT id Id,receipt_id ReceiptId,order_line_id OrderLineId,product_id ProductId,lot_id LotId,
                   quantity_milli QuantityMilli,unit_cost_basis UnitCostBasis
            FROM purchase_receipt_lines WHERE receipt_id=? ORDER BY id;
            """,
            receiptId);
        return new PurchaseReceipt
        {
            Id = row.Id,
            BusinessId = row.BusinessId,
            OrderId = row.OrderId,
            Reference = row.Reference,
            OperationKey = row.OperationKey,
            Notes = row.Notes,
            ReceivedAt = SqliteValues.ParseDate(row.ReceivedAt),
            CreatedAt = SqliteValues.ParseDate(row.CreatedAt),
            Lines = lines.Select(line => new PurchaseReceiptLine
            {
                Id = line.Id,
                ReceiptId = line.ReceiptId,
                OrderLineId = line.OrderLineId,
                ProductId = line.ProductId,
                LotId = line.LotId,
                Quantity = SqliteValues.FromMilli(line.QuantityMilli),
                UnitCost = line.UnitCostBasis.HasValue ? SqliteValues.FromMoney(line.UnitCostBasis.Value) : null
            }).ToList()
        };
    }

    private static void ValidateReceiptDates(
        ExpirationMode expirationMode,
        DateOnly? manufacturingDate,
        DateOnly? expirationDate)
    {
        if (expirationMode == ExpirationMode.Unknown)
        {
            throw new InventoryRuleException("Configura si el producto controla caducidad antes de recibirlo.");
        }

        if (expirationMode == ExpirationMode.Tracked && expirationDate is null)
        {
            throw new InventoryRuleException("La fecha de caducidad es obligatoria para este producto.");
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

    private const string OrderSelect = """
        SELECT o.id Id,o.business_id BusinessId,o.folio Folio,o.supplier_id SupplierId,
               s.company_name SupplierName,o.manual_supplier_name ManualSupplierName,o.order_date OrderDate,
               o.estimated_date EstimatedDate,o.status Status,o.notes Notes,o.total_basis TotalBasis,
               o.created_at CreatedAt,o.updated_at UpdatedAt,o.confirmed_at ConfirmedAt,o.cancelled_at CancelledAt
        FROM purchase_orders o LEFT JOIN suppliers s ON s.id=o.supplier_id
        """;

    private sealed record NormalizedOrderLine(
        long? ProductId,
        string Description,
        string? Barcode,
        string? Sku,
        decimal Quantity,
        UnitOfMeasure UnitOfMeasure,
        decimal? EstimatedUnitCost,
        string? Notes);

    private sealed class OrderRow
    {
        public long Id { get; set; }
        public long BusinessId { get; set; }
        public string Folio { get; set; } = string.Empty;
        public long? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? ManualSupplierName { get; set; }
        public string OrderDate { get; set; } = string.Empty;
        public string? EstimatedDate { get; set; }
        public int Status { get; set; }
        public string? Notes { get; set; }
        public long TotalBasis { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
        public string? ConfirmedAt { get; set; }
        public string? CancelledAt { get; set; }
    }

    private sealed class OrderLineRow
    {
        public long Id { get; set; }
        public long OrderId { get; set; }
        public long? ProductId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? Sku { get; set; }
        public long RequestedMilli { get; set; }
        public long ReceivedMilli { get; set; }
        public int UnitOfMeasure { get; set; }
        public long? EstimatedCostBasis { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class ReceiptRow
    {
        public long Id { get; set; }
        public long BusinessId { get; set; }
        public long OrderId { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string OperationKey { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string ReceivedAt { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }

    private sealed class ReceiptLineRow
    {
        public long Id { get; set; }
        public long ReceiptId { get; set; }
        public long OrderLineId { get; set; }
        public long ProductId { get; set; }
        public long LotId { get; set; }
        public long QuantityMilli { get; set; }
        public long? UnitCostBasis { get; set; }
    }

    private sealed class ReceiptKeyRow
    {
        public long Id { get; set; }
        public long OrderId { get; set; }
    }
}
