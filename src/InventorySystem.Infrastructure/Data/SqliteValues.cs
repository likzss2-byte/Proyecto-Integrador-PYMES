using System.Globalization;

namespace InventorySystem.Infrastructure.Data;

internal static class SqliteValues
{
    public static long ToMilli(decimal value) => checked((long)decimal.Round(
        value * 1000m,
        0,
        MidpointRounding.AwayFromZero));

    public static decimal FromMilli(long value) => value / 1000m;

    public static long ToMoney(decimal value) => checked((long)decimal.Round(
        value * 10000m,
        0,
        MidpointRounding.AwayFromZero));

    public static decimal FromMoney(long value) => value / 10000m;

    public static string Date(DateTime value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static DateTime ParseDate(string value) => DateTime.Parse(
        value,
        CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind);
}
