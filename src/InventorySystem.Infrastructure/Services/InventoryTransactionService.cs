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
                UpdateStock(connection, change.Product.Id, change.ResultingStock, now);
                ProductRepository.InsertMovement(
                    connection,
                    businessId,
                    change.Product.Id,
                    change.MovementType,
                    change.SignedQuantity,
                    change.Product.Stock,
                    change.ResultingStock,
                    document.Reference,
                    document.Notes,
                    now);
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
                UpdateStock(connection, change.Product.Id, change.ResultingStock, now);
                ProductRepository.InsertMovement(
                    connection,
                    businessId,
                    change.Product.Id,
                    change.MovementType,
                    change.SignedQuantity,
                    change.Product.Stock,
                    change.ResultingStock,
                    document.Reference,
                    reason.Trim(),
                    now);
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

        if (requestedLines.GroupBy(line => line.ProductId).Any(group => group.Count() > 1))
        {
            throw new InventoryRuleException("La operación contiene productos repetidos.");
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

                normalizedLines.Add(new InventoryDocumentLineInput(
                    line.ProductId,
                    quantity,
                    decimal.Round(line.UnitPrice, 4, MidpointRounding.AwayFromZero)));
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
                    "INSERT INTO inventory_document_lines(document_id,product_id,quantity_milli,unit_price_basis) VALUES(?,?,?,?);",
                    id,
                    line.ProductId,
                    SqliteValues.ToMilli(line.Quantity),
                    SqliteValues.ToMoney(line.UnitPrice));
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
        foreach (var line in document.Lines)
        {
            var productRow = ProductRepository.GetRow(connection, document.BusinessId, line.ProductId)
                ?? throw new InventoryRuleException("Uno de los productos ya no existe.");
            if (productRow.Active != 1)
            {
                throw new InventoryRuleException($"El producto {productRow.Name} está inactivo.");
            }

            var product = productRow.ToDomain();
            var addStock = document.Type == InventoryDocumentType.Entry ^ cancelling;
            var signedQuantity = addStock ? line.Quantity : -line.Quantity;
            var resulting = InventoryRules.NormalizeQuantity(product.Stock + signedQuantity);
            if (!allowNegative && resulting < 0)
            {
                throw new InventoryRuleException($"Stock insuficiente para {product.Name}. Disponible: {product.Stock:0.###}.");
            }

            var movementType = (document.Type, cancelling) switch
            {
                (InventoryDocumentType.Entry, false) => InventoryMovementType.Entry,
                (InventoryDocumentType.Entry, true) => InventoryMovementType.EntryCancellation,
                (InventoryDocumentType.Sale, false) => InventoryMovementType.Sale,
                _ => InventoryMovementType.SaleCancellation
            };
            changes.Add(new StockChange(product, signedQuantity, resulting, movementType));
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
            SELECT id Id,document_id DocumentId,product_id ProductId,quantity_milli QuantityMilli,unit_price_basis UnitPriceBasis
            FROM inventory_document_lines WHERE document_id=? ORDER BY id;
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
                Quantity = SqliteValues.FromMilli(line.QuantityMilli),
                UnitPrice = SqliteValues.FromMoney(line.UnitPriceBasis)
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

    private sealed record StockChange(
        Product Product,
        decimal SignedQuantity,
        decimal ResultingStock,
        InventoryMovementType MovementType);
}
