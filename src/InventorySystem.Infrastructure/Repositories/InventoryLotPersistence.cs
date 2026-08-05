using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using SQLite;

namespace InventorySystem.Infrastructure.Repositories;

internal static class InventoryLotPersistence
{
    public static void Add(
        SQLiteConnection connection,
        long productId,
        decimal quantity,
        DateOnly? expirationDate,
        string? lotCode,
        string receivedAt)
    {
        if (quantity <= 0)
        {
            return;
        }

        connection.Execute(
            """
            INSERT INTO inventory_lots(product_id,lot_code,quantity_milli,expiration_date,received_at,created_at,updated_at)
            VALUES(?,?,?,?,?,?,?);
            """,
            productId,
            ProductRepository.DbText(lotCode),
            SqliteValues.ToMilli(quantity),
            expirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            receivedAt,
            receivedAt,
            receivedAt);
    }

    public static void ApplyStockChange(
        SQLiteConnection connection,
        long productId,
        decimal signedQuantity,
        string reference,
        string now)
    {
        if (signedQuantity > 0)
        {
            Add(connection, productId, signedQuantity, null, reference, now);
            return;
        }

        if (signedQuantity < 0)
        {
            ConsumeFefo(connection, productId, decimal.Abs(signedQuantity), now);
        }
    }

    private static void ConsumeFefo(
        SQLiteConnection connection,
        long productId,
        decimal quantity,
        string now)
    {
        var remaining = SqliteValues.ToMilli(quantity);
        var lots = connection.Query<LotQuantityRow>(
            """
            SELECT id Id,quantity_milli QuantityMilli
            FROM inventory_lots
            WHERE product_id=? AND quantity_milli>0
            ORDER BY CASE WHEN expiration_date IS NULL THEN 1 ELSE 0 END,expiration_date,received_at,id;
            """,
            productId);
        foreach (var lot in lots)
        {
            if (remaining == 0)
            {
                break;
            }

            var consumed = Math.Min(remaining, lot.QuantityMilli);
            connection.Execute(
                "UPDATE inventory_lots SET quantity_milli=quantity_milli-?,updated_at=? WHERE id=?;",
                consumed,
                now,
                lot.Id);
            remaining -= consumed;
        }

        if (remaining != 0)
        {
            throw new InventoryRuleException("El detalle de lotes no coincide con el stock del producto.");
        }
    }

    private sealed class LotQuantityRow
    {
        public long Id { get; set; }
        public long QuantityMilli { get; set; }
    }
}
