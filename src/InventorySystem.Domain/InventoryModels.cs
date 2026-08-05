using System.Globalization;

namespace InventorySystem.Domain;

public enum UnitOfMeasure
{
    Unit = 0,
    Kilogram = 1,
    Liter = 2
}

public enum InventoryDocumentType
{
    Entry = 0,
    Sale = 1
}

public enum InventoryDocumentStatus
{
    Draft = 0,
    Confirmed = 1,
    Cancelled = 2
}

public enum InventoryCountStatus
{
    Draft = 0,
    Confirmed = 1
}

public enum InventoryMovementType
{
    InitialInventory = 0,
    Entry = 1,
    Sale = 2,
    PositiveAdjustment = 3,
    NegativeAdjustment = 4,
    PhysicalCount = 5,
    EntryCancellation = 6,
    SaleCancellation = 7
}

public sealed class Business
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool AllowNegativeStock { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class Product
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; }
    public decimal Stock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal SalePrice { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string DisplayCode => string.IsNullOrWhiteSpace(Barcode) ? Sku : Barcode;
    public string DisplayStock => UnitOfMeasure switch
    {
        UnitOfMeasure.Kilogram => $"{Stock:0.###} kg",
        UnitOfMeasure.Liter => $"{Stock:0.###} L",
        _ => Stock.ToString("0.###", CultureInfo.CurrentCulture)
    };
}

public sealed class Supplier
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ProductSupplier
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long SupplierId { get; set; }
    public string? SupplierSku { get; set; }
    public decimal? ReferenceCost { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class InventoryDocument
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public InventoryDocumentType Type { get; set; }
    public InventoryDocumentStatus Status { get; set; }
    public string Reference { get; set; } = string.Empty;
    public long? SupplierId { get; set; }
    public string? Notes { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public List<InventoryDocumentLine> Lines { get; set; } = [];
}

public sealed class InventoryDocumentLine
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}

public sealed class InventoryMovement
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public InventoryMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal PreviousStock { get; set; }
    public decimal ResultingStock { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime OccurredAt { get; set; }
}

public sealed class InventoryCount
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public InventoryCountStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CountedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public List<InventoryCountLine> Lines { get; set; } = [];
}

public sealed class InventoryCountLine
{
    public long Id { get; set; }
    public long CountId { get; set; }
    public long ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public UnitOfMeasure UnitOfMeasure { get; set; }
    public decimal TheoreticalStock { get; set; }
    public decimal PhysicalStock { get; set; }
    public decimal Difference => PhysicalStock - TheoreticalStock;
    public decimal Missing => Difference < 0 ? decimal.Abs(Difference) : 0m;
    public decimal Surplus => Difference > 0 ? Difference : 0m;
}

public sealed record ProductInput(
    string Sku,
    string? Barcode,
    string Name,
    string? Description,
    string? Brand,
    UnitOfMeasure UnitOfMeasure,
    decimal MinimumStock,
    decimal SalePrice,
    bool Active = true);

public sealed record SupplierInput(
    string CompanyName,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Country,
    string? State,
    string? Address,
    string? Notes,
    bool Active = true);

public sealed record ProductSupplierInput(
    long ProductId,
    long SupplierId,
    string? SupplierSku,
    decimal? ReferenceCost,
    bool Active = true);

public sealed record InventoryDocumentLineInput(long ProductId, decimal Quantity, decimal UnitPrice);

public sealed record InventoryAdjustmentInput(long ProductId, decimal Quantity, string Reason);

public sealed record InventoryCountLineInput(long ProductId, decimal PhysicalStock);

public sealed record ExternalProduct(
    string Barcode,
    string Name,
    string? Brand,
    string? Description,
    string Source);

public sealed record ProductLookupResult(Product? LocalProduct, ExternalProduct? ExternalSuggestion)
{
    public bool FoundLocally => LocalProduct is not null;
    public bool RequiresConfirmation => LocalProduct is null && ExternalSuggestion is not null;
}

public sealed record BarcodeScanResult(bool Success, string? Code, string? Format, string? Error)
{
    public static BarcodeScanResult Found(string code, string? format = null) => new(true, code, format, null);
    public static BarcodeScanResult Failed(string error) => new(false, null, null, error);
}

public interface IExternalProductCatalog
{
    Task<ExternalProduct?> FindAsync(string barcode, CancellationToken cancellationToken = default);
}

public sealed class InventoryRuleException : Exception
{
    public InventoryRuleException(string message) : base(message)
    {
    }

    public InventoryRuleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public static class InventoryRules
{
    public static string NormalizeSku(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static string? NormalizeBarcode(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    public static string NormalizeScannedCode(string? value) => (value ?? string.Empty).Trim();

    public static decimal NormalizeQuantity(decimal value) =>
        decimal.Round(value, 3, MidpointRounding.AwayFromZero);

    public static void ValidateQuantity(decimal value, UnitOfMeasure unit, string fieldName = "La cantidad")
    {
        value = NormalizeQuantity(value);
        if (value <= 0)
        {
            throw new InventoryRuleException($"{fieldName} debe ser mayor que cero.");
        }

        if (unit == UnitOfMeasure.Unit && value != decimal.Truncate(value))
        {
            throw new InventoryRuleException($"{fieldName} debe ser entera cuando la unidad de medida es pieza.");
        }
    }

    public static void ValidateProduct(ProductInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Sku))
        {
            throw new InventoryRuleException("El SKU es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new InventoryRuleException("El nombre del producto es obligatorio.");
        }

        if (input.MinimumStock < 0)
        {
            throw new InventoryRuleException("El stock mínimo no puede ser negativo.");
        }

        if (input.SalePrice < 0)
        {
            throw new InventoryRuleException("El precio de venta no puede ser negativo.");
        }
    }

    public static void ValidateSupplier(SupplierInput input)
    {
        if (string.IsNullOrWhiteSpace(input.CompanyName))
        {
            throw new InventoryRuleException("La empresa o razón social es obligatoria.");
        }

        if (!string.IsNullOrWhiteSpace(input.Email) && !input.Email.Contains('@', StringComparison.Ordinal))
        {
            throw new InventoryRuleException("El correo del proveedor no es válido.");
        }
    }
}

public static class BarcodeRules
{
    public static bool IsSupportedExternalBarcode(string? value)
    {
        var code = InventoryRules.NormalizeScannedCode(value);
        return code.All(char.IsDigit) && code.Length is 8 or 12 or 13 or 14;
    }

    public static bool IsChecksumValid(string? value)
    {
        var code = InventoryRules.NormalizeScannedCode(value);
        if (!IsSupportedExternalBarcode(code))
        {
            return false;
        }

        var checkIndex = code.Length - 1;
        var sum = 0;
        for (var index = checkIndex - 1; index >= 0; index--)
        {
            var digit = code[index] - '0';
            var positionFromRight = checkIndex - index;
            sum += positionFromRight % 2 == 1 ? digit * 3 : digit;
        }

        var expected = (10 - sum % 10) % 10;
        return expected == code[checkIndex] - '0';
    }
}
