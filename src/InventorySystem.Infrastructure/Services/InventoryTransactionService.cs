using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using InventorySystem.Infrastructure.Repositories;
using SQLite;

namespace InventorySystem.Infrastructure.Services;

public sealed class InventoryTransactionService
{
    private readonly InventoryDatabase _database;

    public InventoryTransactionService(InventoryDatabase database)
    {
        _database = database;
    }

    public Task<InventoryDocument> CreateEntryAsync(
        long businessId,
        IEnumerable<InventoryDocumentLineInput> lines,
        long? supplierId = null,
        string? notes = null,
        string? reference = null,
        CancellationToken cancellationToken = default) =>
        CreateDocumentAsync(
            businessId,
            InventoryDocumentType.Entry,
            lines,
            supplierId,
            notes,
            reference,
            cancellationToken);

    public Task<InventoryDocument> CreateSaleAsync(
        long businessId,
        IEnumerable<InventoryDocumentLineInput> lines,
        string? notes = null,
        string? reference = null,
        CancellationToken cancellationToken = default) =>
        CreateDocumentAsync(
            businessId,
            InventoryDocumentType.Sale,
            lines,
            null,
            notes,
            reference,
            cancellationToken);

    public Task<InventoryDocument> ConfirmAsync(
        long businessId,
        long documentId,
        bool allowExpiredLots = false,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var document = GetDocument(connection, businessId, documentId)
                ?? throw new InventoryRuleException("La operación no existe.");
            if (document.Status == InventoryDocumentStatus.Confirmed)
            {
                throw new InventoryRuleException("La operación ya fue confirmada.");
            }

            if (document.Status == InventoryDocumentStatus.Cancelled)
            {
                throw new InventoryRuleException("Una operación cancelada no se puede confirmar.");
            }

            var allowNegative = AllowsNegativeStock(connection, businessId);
            var changes = PrepareChanges(connection, document, cancelling: false, allowNegative);
            var now = SqliteValues.Date(DateTime.UtcNow);
            foreach (var change in changes)
            {
                IReadOnlyList<LotAllocation> allocations;
                if (document.Type == InventoryDocumentType.Entry)
                {
                    var lotId = InventoryLotPersistence.Add(
                        connection,
                        change.Product.Id,
                        change.Line.Quantity,
                        change.Line.ExpirationDate,
                        change.Line.LotCode ?? document.Reference,
                        now,
                        document.SupplierId,
                        change.Line.ManufacturingDate,
                        change.Line.UnitPrice);
                    connection.Execute(
                        "UPDATE inventory_document_lines SET lot_id=? WHERE id=?;",
                        lotId,
                        change.Line.Id);
                    change.Line.LotId = lotId;
                    allocations = [new LotAllocation(lotId, change.Line.Quantity)];
                }
                else
                {
                    if (!change.Line.LotId.HasValue)
                    {
                        throw new InventoryRuleException($"Selecciona un lote para {change.Product.Name}.");
                    }

                    allocations = InventoryLotPersistence.ConsumeSelected(
                        connection,
                        change.Product.Id,
                        change.Line.LotId.Value,
                        change.Line.Quantity,
                        now,
                        allowExpiredLots);
                }
                UpdateStock(connection, change.Product.Id, change.ResultingStock, now);
                var movementId = ProductRepository.InsertMovement(
                    connection,
                    businessId,
                    change.Product.Id,
                    change.MovementType,
                    change.SignedQuantity,
                    change.PreviousStock,
                    change.ResultingStock,
                    document.Reference,
                    document.Notes,
                    now);
                InventoryLotPersistence.RecordMovementAllocations(connection, movementId, allocations);
            }

            var changed = connection.Execute(
                """
                UPDATE inventory_documents SET status=?,confirmed_at=?,updated_at=?
                WHERE id=? AND business_id=? AND status=?;
                """,
                (int)InventoryDocumentStatus.Confirmed,
                now,
                now,
                documentId,
                businessId,
                (int)InventoryDocumentStatus.Draft);
            if (changed != 1)
            {
                throw new InventoryRuleException("La operación cambió de estado y no pudo confirmarse.");
            }

            return GetDocument(connection, businessId, documentId)!;
        }, cancellationToken);

    public Task<InventoryDocument> CancelAsync(
        long businessId,
        long documentId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InventoryRuleException("El motivo de cancelación es obligatorio.");
        }

        return _database.WriteAsync(connection =>
        {
            var document = GetDocument(connection, businessId, documentId)
                ?? throw new InventoryRuleException("La operación no existe.");
            if (document.Status == InventoryDocumentStatus.Draft)
            {
                throw new InventoryRuleException("Solo se pueden cancelar operaciones confirmadas.");
            }

            if (document.Status == InventoryDocumentStatus.Cancelled)
            {
                throw new InventoryRuleException("La operación ya fue cancelada.");
            }

            var allowNegative = AllowsNegativeStock(connection, businessId);
            var changes = PrepareChanges(connection, document, cancelling: true, allowNegative);
            var now = SqliteValues.Date(DateTime.UtcNow);
            foreach (var change in changes)
            {
                IReadOnlyList<LotAllocation> allocations;
                if (change.Line.LotId.HasValue)
                {
                    allocations = [new LotAllocation(change.Line.LotId.Value, change.Line.Quantity)];
                }
                else
                {
                    // Compatibilidad con documentos creados antes de que las líneas guardaran el lote.
                    var originalType = document.Type == InventoryDocumentType.Entry
                        ? InventoryMovementType.Entry
                        : InventoryMovementType.Sale;
                    allocations = InventoryLotPersistence.GetMovementAllocations(
                        connection,
                        businessId,
                        change.Product.Id,
                        document.Reference,
                        originalType);
                }

                if (allocations.Count == 0)
                {
                    allocations = InventoryLotPersistence.ApplyStockChange(
                        connection,
                        change.Product.Id,
                        change.SignedQuantity,
                        $"CANCELACION-{document.Reference}",
                        now,
                        allowExpiredLots: true);
                }
                else if (document.Type == InventoryDocumentType.Entry)
                {
                    InventoryLotPersistence.ConsumeExact(connection, allocations, now);
                }
                else
                {
                    InventoryLotPersistence.RestoreExact(connection, allocations, now);
                }

                UpdateStock(connection, change.Product.Id, change.ResultingStock, now);
                if (document.Type == InventoryDocumentType.Sale)
                {
                    // Si el producto se archivó usando "Eliminar" después de la venta,
                    // al cancelar esa venta vuelve a estar disponible en el inventario.
                    // Los productos desactivados manualmente no se tocan.
                    connection.Execute(
                        "UPDATE products SET active=1,archived_by_delete=0,updated_at=? WHERE id=? AND archived_by_delete=1;",
                        now,
                        change.Product.Id);
                }

                var movementId = ProductRepository.InsertMovement(
                    connection,
                    businessId,
                    change.Product.Id,
                    change.MovementType,
                    change.SignedQuantity,
                    change.PreviousStock,
                    change.ResultingStock,
                    document.Reference,
                    reason.Trim(),
                    now);
                InventoryLotPersistence.RecordMovementAllocations(connection, movementId, allocations);
            }

            var changed = connection.Execute(
                """
                UPDATE inventory_documents SET status=?,cancelled_at=?,updated_at=?,notes=COALESCE(notes,'') || ?
                WHERE id=? AND business_id=? AND status=?;
                """,
                (int)InventoryDocumentStatus.Cancelled,
                now,
                now,
                $"\nCancelación: {reason.Trim()}",
                documentId,
                businessId,
                (int)InventoryDocumentStatus.Confirmed);
            if (changed != 1)
            {
                throw new InventoryRuleException("La operación cambió de estado y no pudo cancelarse.");
            }

            return GetDocument(connection, businessId, documentId)!;
        }, cancellationToken);
    }

    public Task<InventoryDocument?> GetAsync(
        long businessId,
        long documentId,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync(
            connection => GetDocument(connection, businessId, documentId),
            cancellationToken);

    public Task<IReadOnlyList<InventoryDocument>> GetRecentAsync(
        long businessId,
        InventoryDocumentType? type = null,
        int limit = 100,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<InventoryDocument>>(connection =>
        {
            var rows = type.HasValue
                ? connection.Query<DocumentRow>(
                    DocumentSelect + " WHERE business_id=? AND document_type=? ORDER BY created_at DESC LIMIT ?;",
                    businessId,
                    (int)type.Value,
                    limit)
                : connection.Query<DocumentRow>(
                    DocumentSelect + " WHERE business_id=? ORDER BY created_at DESC LIMIT ?;",
                    businessId,
                    limit);
            return rows.Select(row => MapDocument(connection, row)).ToArray();
        }, cancellationToken);

    private Task<InventoryDocument> CreateDocumentAsync(
        long businessId,
        InventoryDocumentType type,
        IEnumerable<InventoryDocumentLineInput> lines,
        long? supplierId,
        string? notes,
        string? reference,
        CancellationToken cancellationToken)
    {
        var requestedLines = lines?.ToList() ?? [];
        if (requestedLines.Count == 0)
        {
            throw new InventoryRuleException("La operación debe incluir al menos un producto.");
        }

        return _database.WriteAsync(connection =>
        {
            if (supplierId.HasValue && SupplierRepository.GetRow(connection, businessId, supplierId.Value) is null)
            {
                throw new InventoryRuleException("El proveedor no existe.");
            }

            var normalizedLines = new List<InventoryDocumentLineInput>(requestedLines.Count);
            foreach (var line in requestedLines)
            {
                var product = ProductRepository.GetRow(connection, businessId, line.ProductId)
                    ?? throw new InventoryRuleException("Uno de los productos no existe.");
                if (product.Active != 1)
                {
                    throw new InventoryRuleException($"El producto {product.Name} está inactivo.");
                }

                var quantity = InventoryRules.NormalizeQuantity(line.Quantity);
                InventoryRules.ValidateQuantity(quantity, (UnitOfMeasure)product.UnitOfMeasure);
                if (line.UnitPrice < 0)
                {
                    throw new InventoryRuleException("El costo o precio unitario no puede ser negativo.");
                }

                string? lotCode = string.IsNullOrWhiteSpace(line.LotCode) ? null : line.LotCode.Trim();
                DateOnly? manufacturingDate = line.ManufacturingDate;
                DateOnly? expirationDate = line.ExpirationDate;
                long? lotId = line.LotId;
                if (type == InventoryDocumentType.Entry)
                {
                    var expirationMode = (ExpirationMode)product.ExpirationMode;
                    ValidateEntryLot(expirationMode, manufacturingDate, expirationDate, product.Name);
                    if (expirationMode == ExpirationMode.NotApplicable)
                    {
                        expirationDate = null;
                    }
                    lotId = null;
                }
                else
                {
                    if (!lotId.HasValue)
                    {
                        throw new InventoryRuleException($"Selecciona un lote para {product.Name}.");
                    }

                    var selectedLot = connection.Query<SaleLotRow>(
                            """
                            SELECT id Id,lot_code LotCode,manufacturing_date ManufacturingDate,expiration_date ExpirationDate
                            FROM inventory_lots WHERE id=? AND product_id=? LIMIT 1;
                            """,
                            lotId.Value,
                            line.ProductId)
                        .FirstOrDefault()
                        ?? throw new InventoryRuleException($"El lote seleccionado de {product.Name} ya no existe.");
                    lotCode = selectedLot.LotCode;
                    manufacturingDate = ParseDateOnly(selectedLot.ManufacturingDate);
                    expirationDate = (ExpirationMode)product.ExpirationMode == ExpirationMode.Tracked
                        ? ParseDateOnly(selectedLot.ExpirationDate)
                        : null;
                }

                normalizedLines.Add(new InventoryDocumentLineInput(
                    line.ProductId,
                    quantity,
                    decimal.Round(line.UnitPrice, 4, MidpointRounding.AwayFromZero),
                    lotCode,
                    manufacturingDate,
                    expirationDate,
                    lotId));
            }

            var total = normalizedLines.Sum(line => line.Quantity * line.UnitPrice);
            var now = SqliteValues.Date(DateTime.UtcNow);
            var normalizedReference = string.IsNullOrWhiteSpace(reference)
                ? CreateReference(type)
                : reference.Trim();
            if (connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM inventory_documents WHERE business_id=? AND reference=? COLLATE NOCASE;",
                    businessId,
                    normalizedReference) > 0)
            {
                throw new InventoryRuleException("La referencia de la operación ya existe.");
            }

            connection.Execute(
                """
                INSERT INTO inventory_documents(
                    business_id,document_type,status,reference,supplier_id,notes,total_basis,
                    created_at,updated_at,confirmed_at,cancelled_at)
                VALUES(?,?,?,?,?,?,?,?,?,NULL,NULL);
                """,
                businessId,
                (int)type,
                (int)InventoryDocumentStatus.Draft,
                normalizedReference,
                supplierId,
                ProductRepository.DbText(notes),
                SqliteValues.ToMoney(total),
                now,
                now);
            var id = connection.ExecuteScalar<long>("SELECT last_insert_rowid();");
            foreach (var line in normalizedLines)
            {
                connection.Execute(
                    """
                    INSERT INTO inventory_document_lines(
                        document_id,product_id,lot_id,quantity_milli,unit_price_basis,lot_code,manufacturing_date,expiration_date)
                    VALUES(?,?,?,?,?,?,?,?);
                    """,
                    id,
                    line.ProductId,
                    line.LotId,
                    SqliteValues.ToMilli(line.Quantity),
                    SqliteValues.ToMoney(line.UnitPrice),
                    ProductRepository.DbText(line.LotCode),
                    line.ManufacturingDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    line.ExpirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            return GetDocument(connection, businessId, id)!;
        }, cancellationToken);
    }

    private static IReadOnlyList<StockChange> PrepareChanges(
        SQLiteConnection connection,
        InventoryDocument document,
        bool cancelling,
        bool allowNegative)
    {
        var changes = new List<StockChange>(document.Lines.Count);
        var runningStocks = new Dictionary<long, decimal>();
        foreach (var line in document.Lines)
        {
            var productRow = ProductRepository.GetRow(connection, document.BusinessId, line.ProductId)
                ?? throw new InventoryRuleException("Uno de los productos ya no existe.");
            if (!cancelling && productRow.Active != 1)
            {
                throw new InventoryRuleException($"El producto {productRow.Name} está inactivo.");
            }

            var product = productRow.ToDomain();
            var previousStock = runningStocks.TryGetValue(product.Id, out var running)
                ? running
                : product.Stock;
            var addStock = document.Type == InventoryDocumentType.Entry ^ cancelling;
            var signedQuantity = addStock ? line.Quantity : -line.Quantity;
            var resulting = InventoryRules.NormalizeQuantity(previousStock + signedQuantity);
            if (!allowNegative && resulting < 0)
            {
                throw new InventoryRuleException($"Stock insuficiente para {product.Name}. Disponible: {previousStock:0.###}.");
            }

            var movementType = (document.Type, cancelling) switch
            {
                (InventoryDocumentType.Entry, false) => InventoryMovementType.Entry,
                (InventoryDocumentType.Entry, true) => InventoryMovementType.EntryCancellation,
                (InventoryDocumentType.Sale, false) => InventoryMovementType.Sale,
                _ => InventoryMovementType.SaleCancellation
            };
            changes.Add(new StockChange(product, line, signedQuantity, previousStock, resulting, movementType));
            runningStocks[product.Id] = resulting;
        }

        return changes;
    }

    private static void UpdateStock(SQLiteConnection connection, long productId, decimal stock, string now) =>
        connection.Execute(
            "UPDATE products SET stock_milli=?,updated_at=? WHERE id=?;",
            SqliteValues.ToMilli(stock),
            now,
            productId);

    private static bool AllowsNegativeStock(SQLiteConnection connection, long businessId) =>
        connection.ExecuteScalar<int>("SELECT allow_negative_stock FROM businesses WHERE id=?;", businessId) == 1;

    private static InventoryDocument? GetDocument(SQLiteConnection connection, long businessId, long documentId)
    {
        var row = connection.Query<DocumentRow>(
                DocumentSelect + " WHERE id=? AND business_id=? LIMIT 1;",
                documentId,
                businessId)
            .FirstOrDefault();
        return row is null ? null : MapDocument(connection, row);
    }

    private static InventoryDocument MapDocument(SQLiteConnection connection, DocumentRow row)
    {
        var lines = connection.Query<DocumentLineRow>(
            """
            SELECT dl.id Id,dl.document_id DocumentId,dl.product_id ProductId,p.name ProductName,dl.lot_id LotId,
                   dl.quantity_milli QuantityMilli,dl.unit_price_basis UnitPriceBasis,
                   COALESCE(dl.lot_code,l.lot_code) LotCode,
                   COALESCE(dl.manufacturing_date,l.manufacturing_date) ManufacturingDate,
                   COALESCE(dl.expiration_date,l.expiration_date) ExpirationDate
            FROM inventory_document_lines dl
            JOIN products p ON p.id=dl.product_id
            LEFT JOIN inventory_lots l ON l.id=dl.lot_id
            WHERE dl.document_id=? ORDER BY dl.id;
            """,
            row.Id);
        return new InventoryDocument
        {
            Id = row.Id,
            BusinessId = row.BusinessId,
            Type = (InventoryDocumentType)row.DocumentType,
            Status = (InventoryDocumentStatus)row.Status,
            Reference = row.Reference,
            SupplierId = row.SupplierId,
            Notes = row.Notes,
            Total = SqliteValues.FromMoney(row.TotalBasis),
            CreatedAt = SqliteValues.ParseDate(row.CreatedAt),
            UpdatedAt = SqliteValues.ParseDate(row.UpdatedAt),
            ConfirmedAt = row.ConfirmedAt is null ? null : SqliteValues.ParseDate(row.ConfirmedAt),
            CancelledAt = row.CancelledAt is null ? null : SqliteValues.ParseDate(row.CancelledAt),
            Lines = lines.Select(line => new InventoryDocumentLine
            {
                Id = line.Id,
                DocumentId = line.DocumentId,
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                LotId = line.LotId,
                Quantity = SqliteValues.FromMilli(line.QuantityMilli),
                UnitPrice = SqliteValues.FromMoney(line.UnitPriceBasis),
                LotCode = line.LotCode,
                ManufacturingDate = line.ManufacturingDate is null ? null : DateOnly.ParseExact(line.ManufacturingDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                ExpirationDate = line.ExpirationDate is null ? null : DateOnly.ParseExact(line.ExpirationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            }).ToList()
        };
    }

    private static string CreateReference(InventoryDocumentType type) =>
        $"{(type == InventoryDocumentType.Entry ? "ENT" : "VTA")}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private const string DocumentSelect = """
        SELECT id Id,business_id BusinessId,document_type DocumentType,status Status,reference Reference,
               supplier_id SupplierId,notes Notes,total_basis TotalBasis,created_at CreatedAt,updated_at UpdatedAt,
               confirmed_at ConfirmedAt,cancelled_at CancelledAt
        FROM inventory_documents
        """;

    private static void ValidateEntryLot(
        ExpirationMode expirationMode,
        DateOnly? manufacturingDate,
        DateOnly? expirationDate,
        string productName)
    {
        if (expirationMode == ExpirationMode.Tracked && expirationDate is null)
        {
            throw new InventoryRuleException($"La caducidad del lote de {productName} es obligatoria.");
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

    private static DateOnly? ParseDateOnly(string? value) => value is null
        ? null
        : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed class SaleLotRow
    {
        public long Id { get; set; }
        public string? LotCode { get; set; }
        public string? ManufacturingDate { get; set; }
        public string? ExpirationDate { get; set; }
    }

    private sealed record StockChange(
        Product Product,
        InventoryDocumentLine Line,
        decimal SignedQuantity,
        decimal PreviousStock,
        decimal ResultingStock,
        InventoryMovementType MovementType);
}
