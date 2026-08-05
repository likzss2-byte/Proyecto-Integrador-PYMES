using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using SQLite;

namespace InventorySystem.Infrastructure.Repositories;

internal sealed record LotAllocation(long LotId, decimal Quantity);

internal static class InventoryLotPersistence
{
    public static long Add(
        SQLiteConnection connection,
        long productId,
        decimal quantity,
        DateOnly? expirationDate,
        string? lotCode,
        string receivedAt,
        long? supplierId = null,
        DateOnly? manufacturingDate = null,
        decimal? unitCost = null,
        long? purchaseOrderId = null,
        long? receiptId = null)
    {
        if (quantity <= 0)
        {
            throw new InventoryRuleException("La cantidad del lote debe ser mayor que cero.");
        }

        connection.Execute(
            """
            INSERT INTO inventory_lots(
                product_id,supplier_id,lot_code,manufacturing_date,quantity_milli,initial_quantity_milli,
                unit_cost_basis,expiration_date,received_at,status,purchase_order_id,receipt_id,created_at,updated_at)
            VALUES(?,?,?,?,?,?,?,?,?,0,?,?,?,?);
            """,
            productId,
            supplierId,
            ProductRepository.DbText(lotCode),
            manufacturingDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SqliteValues.ToMilli(quantity),
            SqliteValues.ToMilli(quantity),
            unitCost.HasValue ? SqliteValues.ToMoney(unitCost.Value) : null,
            expirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            receivedAt,
            purchaseOrderId,
            receiptId,
            receivedAt,
            receivedAt);
        return connection.ExecuteScalar<long>("SELECT last_insert_rowid();");
    }

    public static IReadOnlyList<LotAllocation> ApplyStockChange(
        SQLiteConnection connection,
        long productId,
        decimal signedQuantity,
        string reference,
        string now,
        bool allowExpiredLots = false)
    {
        if (signedQuantity > 0)
        {
            var lotId = Add(connection, productId, signedQuantity, null, reference, now);
            return [new LotAllocation(lotId, signedQuantity)];
        }

        return signedQuantity < 0
            ? ConsumeFefo(connection, productId, decimal.Abs(signedQuantity), now, allowExpiredLots)
            : [];
    }

    public static void RecordMovementAllocations(
        SQLiteConnection connection,
        long movementId,
        IEnumerable<LotAllocation> allocations)
    {
        foreach (var allocation in allocations)
        {
            connection.Execute(
                "INSERT INTO inventory_movement_lots(movement_id,lot_id,quantity_milli) VALUES(?,?,?);",
                movementId,
                allocation.LotId,
                SqliteValues.ToMilli(allocation.Quantity));
        }
    }

    public static IReadOnlyList<LotAllocation> GetMovementAllocations(
        SQLiteConnection connection,
        long businessId,
        long productId,
        string reference,
        InventoryMovementType movementType)
    {
        var rows = connection.Query<MovementLotRow>(
            """
            SELECT ml.lot_id LotId,ml.quantity_milli QuantityMilli
            FROM inventory_movements m
            JOIN inventory_movement_lots ml ON ml.movement_id=m.id
            WHERE m.business_id=? AND m.product_id=? AND m.reference=? AND m.movement_type=?
            ORDER BY ml.id;
            """,
            businessId,
            productId,
            reference,
            (int)movementType);
        return rows.Select(row => new LotAllocation(row.LotId, SqliteValues.FromMilli(row.QuantityMilli))).ToArray();
    }

    public static IReadOnlyList<LotAllocation> GetMovementAllocations(
        SQLiteConnection connection,
        long movementId) =>
        connection.Query<MovementLotRow>(
                """
                SELECT lot_id LotId,quantity_milli QuantityMilli
                FROM inventory_movement_lots WHERE movement_id=? ORDER BY id;
                """,
                movementId)
            .Select(row => new LotAllocation(row.LotId, SqliteValues.FromMilli(row.QuantityMilli)))
            .ToArray();

    public static void RestoreExact(
        SQLiteConnection connection,
        IReadOnlyList<LotAllocation> allocations,
        string now)
    {
        foreach (var allocation in allocations)
        {
            var changed = connection.Execute(
                """
                UPDATE inventory_lots
                SET quantity_milli=quantity_milli+?,status=0,updated_at=?
                WHERE id=?;
                """,
                SqliteValues.ToMilli(allocation.Quantity),
                now,
                allocation.LotId);
            if (changed != 1)
            {
                throw new InventoryRuleException("No se pudo restaurar uno de los lotes originales.");
            }
        }
    }

    public static void ConsumeExact(
        SQLiteConnection connection,
        IReadOnlyList<LotAllocation> allocations,
        string now)
    {
        foreach (var allocation in allocations)
        {
            var quantityMilli = SqliteValues.ToMilli(allocation.Quantity);
            var changed = connection.Execute(
                """
                UPDATE inventory_lots
                SET quantity_milli=quantity_milli-?,
                    status=CASE WHEN quantity_milli-?=0 THEN 1 ELSE 0 END,
                    updated_at=?
                WHERE id=? AND quantity_milli>=?;
                """,
                quantityMilli,
                quantityMilli,
                now,
                allocation.LotId,
                quantityMilli);
            if (changed != 1)
            {
                throw new InventoryRuleException("No hay disponibilidad suficiente en el lote original para revertir la entrada.");
            }
        }
    }

    private static IReadOnlyList<LotAllocation> ConsumeFefo(
        SQLiteConnection connection,
        long productId,
        decimal quantity,
        string now,
        bool allowExpiredLots)
    {
        var remaining = SqliteValues.ToMilli(quantity);
        var today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var lots = connection.Query<LotQuantityRow>(
            """
            SELECT id Id,quantity_milli QuantityMilli
            FROM inventory_lots
            WHERE product_id=? AND quantity_milli>0
              AND (?=1 OR expiration_date IS NULL OR date(expiration_date)>=date(?))
            ORDER BY CASE WHEN expiration_date IS NULL THEN 1 ELSE 0 END,expiration_date,received_at,id;
            """,
            productId,
            allowExpiredLots ? 1 : 0,
            today);
        var allocations = new List<LotAllocation>();
        foreach (var lot in lots)
        {
            if (remaining == 0)
            {
                break;
            }

            var consumed = Math.Min(remaining, lot.QuantityMilli);
            connection.Execute(
                """
                UPDATE inventory_lots
                SET quantity_milli=quantity_milli-?,
                    status=CASE WHEN quantity_milli-?=0 THEN 1 ELSE 0 END,
                    updated_at=?
                WHERE id=?;
                """,
                consumed,
                consumed,
                now,
                lot.Id);
            allocations.Add(new LotAllocation(lot.Id, SqliteValues.FromMilli(consumed)));
            remaining -= consumed;
        }

        if (remaining != 0)
        {
            var expiredAvailable = connection.ExecuteScalar<long>(
                """
                SELECT COALESCE(SUM(quantity_milli),0) FROM inventory_lots
                WHERE product_id=? AND quantity_milli>0 AND expiration_date IS NOT NULL AND date(expiration_date)<date(?);
                """,
                productId,
                today);
            if (!allowExpiredLots && expiredAvailable > 0)
            {
                throw new InventoryRuleException(
                    "El stock disponible incluye lotes caducados. Confirma explícitamente si deseas despacharlos.");
            }

            throw new InventoryRuleException("El detalle de lotes no coincide con el stock disponible del producto.");
        }

        return allocations;
    }

    private sealed class LotQuantityRow
    {
        public long Id { get; set; }
        public long QuantityMilli { get; set; }
    }

    private sealed class MovementLotRow
    {
        public long LotId { get; set; }
        public long QuantityMilli { get; set; }
    }
}
