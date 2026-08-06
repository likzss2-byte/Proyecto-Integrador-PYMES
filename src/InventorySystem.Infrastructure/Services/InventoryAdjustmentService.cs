using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using InventorySystem.Infrastructure.Repositories;
using SQLite;

namespace InventorySystem.Infrastructure.Services;

public sealed class InventoryAdjustmentService
{
    private readonly InventoryDatabase _database;

    public InventoryAdjustmentService(InventoryDatabase database)
    {
        _database = database;
    }

    public Task<Product> ApplyAdjustmentAsync(
        long businessId,
        InventoryAdjustmentInput input,
        CancellationToken cancellationToken = default)
    {
        var quantity = InventoryRules.NormalizeQuantity(input.Quantity);
        if (quantity == 0)
        {
            throw new InventoryRuleException("La cantidad del ajuste no puede ser cero.");
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new InventoryRuleException("El motivo del ajuste es obligatorio.");
        }

        return _database.WriteAsync(connection =>
        {
            var productRow = ProductRepository.GetRow(connection, businessId, input.ProductId)
                ?? throw new InventoryRuleException("El producto no existe.");
            if (productRow.Active != 1)
            {
                throw new InventoryRuleException("No se puede ajustar un producto inactivo.");
            }

            InventoryRules.ValidateQuantity(decimal.Abs(quantity), (UnitOfMeasure)productRow.UnitOfMeasure);
            var product = productRow.ToDomain();
            var resulting = InventoryRules.NormalizeQuantity(product.Stock + quantity);
            if (!AllowsNegativeStock(connection, businessId) && resulting < 0)
            {
                throw new InventoryRuleException("El ajuste produciría inventario negativo.");
            }

            var now = SqliteValues.Date(DateTime.UtcNow);
            var reference = $"AJU-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
            var allocations = InventoryLotPersistence.ApplyStockChange(
                connection,
                product.Id,
                quantity,
                reference,
                now,
                allowExpiredLots: true);
            connection.Execute(
                "UPDATE products SET stock_milli=?,updated_at=? WHERE id=?;",
                SqliteValues.ToMilli(resulting),
                now,
                product.Id);
            var movementId = ProductRepository.InsertMovement(
                connection,
                businessId,
                product.Id,
                quantity > 0 ? InventoryMovementType.PositiveAdjustment : InventoryMovementType.NegativeAdjustment,
                quantity,
                product.Stock,
                resulting,
                reference,
                input.Reason.Trim(),
                now);
            InventoryLotPersistence.RecordMovementAllocations(connection, movementId, allocations);
            return ProductRepository.GetRow(connection, businessId, product.Id)!.ToDomain();
        }, cancellationToken);
    }

    public Task<InventoryCount> CreateCountAsync(
        long businessId,
        IEnumerable<InventoryCountLineInput> lines,
        string? notes = null,
        string? reference = null,
        CancellationToken cancellationToken = default)
    {
        var requestedLines = lines?.ToList() ?? [];
        if (requestedLines.Count == 0)
        {
            throw new InventoryRuleException("El conteo debe incluir al menos un producto.");
        }

        if (requestedLines.GroupBy(line => line.ProductId).Any(group => group.Count() > 1))
        {
            throw new InventoryRuleException("El conteo contiene productos repetidos.");
        }

        return _database.WriteAsync(connection =>
        {
            var prepared = new List<(ProductRow Product, decimal Physical)>(requestedLines.Count);
            foreach (var line in requestedLines)
            {
                var product = ProductRepository.GetRow(connection, businessId, line.ProductId)
                    ?? throw new InventoryRuleException("Uno de los productos no existe.");
                if (product.Active != 1)
                {
                    throw new InventoryRuleException($"El producto {product.Name} está inactivo.");
                }

                var physical = InventoryRules.NormalizeQuantity(line.PhysicalStock);
                if (physical < 0)
                {
                    throw new InventoryRuleException("El inventario físico no puede ser negativo.");
                }

                if (physical > 0)
                {
                    InventoryRules.ValidateQuantity(physical, (UnitOfMeasure)product.UnitOfMeasure, "El inventario físico");
                }

                prepared.Add((product, physical));
            }

            var now = SqliteValues.Date(DateTime.UtcNow);
            var normalizedReference = string.IsNullOrWhiteSpace(reference)
                ? $"CON-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}"
                : reference.Trim();
            connection.Execute(
                """
                INSERT INTO inventory_counts(
                    business_id,reference,inventory_type,supplier_id,brand,status,notes,
                    started_at,counted_at,created_at,updated_at,confirmed_at,cancelled_at)
                VALUES(?,?,2,NULL,NULL,?,?,?,?,?,?,NULL,NULL);
                """,
                businessId,
                normalizedReference,
                (int)InventoryCountStatus.Draft,
                ProductRepository.DbText(notes),
                now,
                now,
                now,
                now);
            var countId = connection.ExecuteScalar<long>("SELECT last_insert_rowid();");
            foreach (var item in prepared)
            {
                var theoretical = SqliteValues.FromMilli(item.Product.StockMilli);
                connection.Execute(
                    """
                    INSERT INTO inventory_count_lines(count_id,product_id,theoretical_milli,physical_milli,difference_milli)
                    VALUES(?,?,?,?,?);
                    """,
                    countId,
                    item.Product.Id,
                    item.Product.StockMilli,
                    SqliteValues.ToMilli(item.Physical),
                    SqliteValues.ToMilli(item.Physical - theoretical));
            }

            return GetCount(connection, businessId, countId)!;
        }, cancellationToken);
    }

    public Task<InventoryCount> ConfirmCountAsync(
        long businessId,
        long countId,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var count = GetCount(connection, businessId, countId)
                ?? throw new InventoryRuleException("El conteo no existe.");
            if (count.Status == InventoryCountStatus.Confirmed)
            {
                throw new InventoryRuleException("El conteo ya fue confirmado.");
            }

            var changes = new List<(Product Product, decimal Physical, decimal Difference)>();
            foreach (var line in count.Lines)
            {
                if (!line.PhysicalStock.HasValue)
                {
                    throw new InventoryRuleException($"El producto {line.ProductName} todavía no ha sido contado.");
                }

                var product = ProductRepository.GetRow(connection, businessId, line.ProductId)?.ToDomain()
                    ?? throw new InventoryRuleException("Uno de los productos ya no existe.");
                if (product.Stock != line.TheoreticalStock)
                {
                    throw new InventoryRuleException(
                        $"El stock de {product.Name} cambió después del conteo. Captura un conteo nuevo.");
                }

                changes.Add((product, line.PhysicalStock.Value, line.PhysicalStock.Value - product.Stock));
            }

            var now = SqliteValues.Date(DateTime.UtcNow);
            foreach (var change in changes.Where(change => change.Difference != 0))
            {
                var allocations = InventoryLotPersistence.ApplyStockChange(
                    connection,
                    change.Product.Id,
                    change.Difference,
                    count.Reference,
                    now,
                    allowExpiredLots: true);
                connection.Execute(
                    "UPDATE products SET stock_milli=?,updated_at=? WHERE id=?;",
                    SqliteValues.ToMilli(change.Physical),
                    now,
                    change.Product.Id);
                var movementId = ProductRepository.InsertMovement(
                    connection,
                    businessId,
                    change.Product.Id,
                    InventoryMovementType.PhysicalCount,
                    change.Difference,
                    change.Product.Stock,
                    change.Physical,
                    count.Reference,
                    count.Notes ?? "Conteo físico",
                    now);
                InventoryLotPersistence.RecordMovementAllocations(connection, movementId, allocations);
            }

            var changed = connection.Execute(
                "UPDATE inventory_counts SET status=?,confirmed_at=? WHERE id=? AND business_id=? AND status=?;",
                (int)InventoryCountStatus.Confirmed,
                now,
                countId,
                businessId,
                (int)InventoryCountStatus.Draft);
            if (changed != 1)
            {
                throw new InventoryRuleException("El conteo cambió de estado y no pudo confirmarse.");
            }

            return GetCount(connection, businessId, countId)!;
        }, cancellationToken);

    public Task<InventoryCount?> GetCountAsync(
        long businessId,
        long countId,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync(
            connection => GetCount(connection, businessId, countId),
            cancellationToken);

    public Task<IReadOnlyList<InventoryMovement>> GetMovementsAsync(
        long businessId,
        long? productId = null,
        int limit = 100,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<InventoryMovement>>(connection =>
        {
            var sql = """
                SELECT m.id Id,m.business_id BusinessId,m.product_id ProductId,p.name ProductName,
                       m.movement_type MovementType,m.quantity_milli QuantityMilli,
                       m.previous_stock_milli PreviousStockMilli,m.resulting_stock_milli ResultingStockMilli,
                       m.reference Reference,m.reason Reason,m.inventory_count_id InventoryCountId,m.occurred_at OccurredAt
                FROM inventory_movements m JOIN products p ON p.id=m.product_id
                WHERE m.business_id=?
                """;
            var rows = productId.HasValue
                ? connection.Query<MovementRow>(sql + " AND m.product_id=? ORDER BY m.occurred_at DESC,m.id DESC LIMIT ?;", businessId, productId.Value, limit)
                : connection.Query<MovementRow>(sql + " ORDER BY m.occurred_at DESC,m.id DESC LIMIT ?;", businessId, limit);
            return rows.Select(row => new InventoryMovement
            {
                Id = row.Id,
                BusinessId = row.BusinessId,
                ProductId = row.ProductId,
                ProductName = row.ProductName,
                Type = (InventoryMovementType)row.MovementType,
                Quantity = SqliteValues.FromMilli(row.QuantityMilli),
                PreviousStock = SqliteValues.FromMilli(row.PreviousStockMilli),
                ResultingStock = SqliteValues.FromMilli(row.ResultingStockMilli),
                Reference = row.Reference,
                Reason = row.Reason,
                InventoryCountId = row.InventoryCountId,
                OccurredAt = SqliteValues.ParseDate(row.OccurredAt),
                LotAllocations = InventoryLotPersistence.GetMovementAllocations(connection, row.Id)
                    .Select(allocation => new InventoryMovementLot(allocation.LotId, allocation.Quantity))
                    .ToList()
            }).ToArray();
        }, cancellationToken);

    private static InventoryCount? GetCount(SQLiteConnection connection, long businessId, long countId)
    {
        var row = connection.Query<CountRow>(
                """
                SELECT id Id,business_id BusinessId,reference Reference,status Status,notes Notes,
                       counted_at CountedAt,created_at CreatedAt,confirmed_at ConfirmedAt
                FROM inventory_counts WHERE id=? AND business_id=? LIMIT 1;
                """,
                countId,
                businessId)
            .FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        var lines = connection.Query<CountLineRow>(
            """
            SELECT l.id Id,l.count_id CountId,l.product_id ProductId,COALESCE(p.barcode,p.sku) Code,
                   p.name ProductName,p.unit_of_measure UnitOfMeasure,l.theoretical_milli TheoreticalMilli,
                   l.physical_milli PhysicalMilli
            FROM inventory_count_lines l JOIN products p ON p.id=l.product_id
            WHERE l.count_id=? ORDER BY l.id;
            """,
            row.Id);
        return new InventoryCount
        {
            Id = row.Id,
            BusinessId = row.BusinessId,
            Reference = row.Reference,
            Status = (InventoryCountStatus)row.Status,
            Notes = row.Notes,
            CountedAt = SqliteValues.ParseDate(row.CountedAt),
            CreatedAt = SqliteValues.ParseDate(row.CreatedAt),
            ConfirmedAt = row.ConfirmedAt is null ? null : SqliteValues.ParseDate(row.ConfirmedAt),
            Lines = lines.Select(line => new InventoryCountLine
            {
                Id = line.Id,
                CountId = line.CountId,
                ProductId = line.ProductId,
                Code = line.Code,
                ProductName = line.ProductName,
                UnitOfMeasure = (UnitOfMeasure)line.UnitOfMeasure,
                TheoreticalStock = SqliteValues.FromMilli(line.TheoreticalMilli),
                PhysicalStock = line.PhysicalMilli.HasValue
                    ? SqliteValues.FromMilli(line.PhysicalMilli.Value)
                    : null
            }).ToList()
        };
    }

    private static bool AllowsNegativeStock(SQLiteConnection connection, long businessId) =>
        connection.ExecuteScalar<int>("SELECT allow_negative_stock FROM businesses WHERE id=?;", businessId) == 1;
}
