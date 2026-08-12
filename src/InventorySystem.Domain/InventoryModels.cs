using System.Globalization;

namespace InventorySystem.Domain;

public enum UnitOfMeasure
{
    Unit = 0,
    Kilogram = 1,
    Liter = 2
}

public enum ExpirationMode
{
    Unknown = 0,
    Tracked = 1,
    NotApplicable = 2
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

public enum InventoryCountType
{
    BySupplier = 0,
    ByBrand = 1,
    FreeOperational = 2
}

public enum InventoryCountStatus
{
    Draft = 0,
    Completed = 1,
    Confirmed = Completed,
    InProgress = 2,
    Cancelled = 3
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
    SaleCancellation = 7,
    PurchaseReceipt = Entry
}

public enum InventoryLotStatus
{
    Active = 0,
    Exhausted = 1
}

public enum PurchaseOrderStatus
{
    Draft = 0,
    Pending = 1,
    Confirmed = 2,
    PartiallyReceived = 3,
    Received = 4,
    Cancelled = 5
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
    // Campo legado conservado únicamente para compatibilidad con bases anteriores. No se usa en la UI ni en búsquedas.
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; }
    public decimal Stock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal SalePrice { get; set; }
    public ExpirationMode ExpirationMode { get; set; }
    public DateOnly? NearestExpirationDate { get; set; }
    public decimal UndatedStock { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string DisplayCode => string.IsNullOrWhiteSpace(Barcode) ? $"ID {Id}" : Barcode;
    public string DisplayStock => UnitOfMeasure switch
    {
        UnitOfMeasure.Kilogram => $"{Stock:0.###} kg",
        UnitOfMeasure.Liter => $"{Stock:0.###} L",
        _ => Stock.ToString("0.###", CultureInfo.CurrentCulture)
    };
}

public sealed class InventoryLot
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public ExpirationMode ProductExpirationMode { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? LotCode { get; set; }
    public DateOnly? ManufacturingDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal InitialQuantity { get; set; }
    public decimal? UnitCost { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public DateTime ReceivedAt { get; set; }
    public InventoryLotStatus Status { get; set; }
    public long? PurchaseOrderId { get; set; }
    public long? ReceiptId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public int? DaysUntilExpiration => ExpirationDate?.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
    public bool IsExpired => ProductExpirationMode == ExpirationMode.Tracked &&
        ExpirationDate.HasValue &&
        ExpirationDate.Value < DateOnly.FromDateTime(DateTime.Today);
    public string DisplayExpiration => ProductExpirationMode == ExpirationMode.NotApplicable
        ? "Sin caducidad"
        : ExpirationDate.HasValue
            ? ExpirationDate.Value.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)
            : "Sin fecha de caducidad";
    public string DisplaySelection => $"Lote {LotCode ?? "Sin código"} · Disponible {Quantity:0.###} · " +
        (ProductExpirationMode == ExpirationMode.NotApplicable
            ? "Sin caducidad"
            : ExpirationDate.HasValue
                ? $"Caduca {ExpirationDate:dd/MM/yyyy}"
                : "Sin fecha de caducidad") +
        (IsExpired ? " · CADUCADO" : string.Empty);
    public string EffectiveStatus => Quantity <= 0
        ? "Agotado"
        : ProductExpirationMode != ExpirationMode.Tracked
            ? "Vigente"
            : !ExpirationDate.HasValue
                ? "Falta caducidad"
                : DaysUntilExpiration < 0
                    ? "Caducado"
                    : DaysUntilExpiration <= 7
                        ? "Próximo a caducar"
                        : "Vigente";
}

public sealed record ExpirationAlert(
    long ProductId,
    string ProductName,
    string Code,
    string? LotCode,
    decimal Quantity,
    UnitOfMeasure UnitOfMeasure,
    DateOnly ExpirationDate,
    string? SupplierName = null)
{
    public int DaysRemaining => ExpirationDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
    public string DisplayExpiration => ExpirationDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);
    public string DisplayDays => DaysRemaining switch
    {
        < 0 => $"Vencido hace {Math.Abs(DaysRemaining)} día(s)",
        0 => "Caduca hoy",
        1 => "Caduca mañana",
        _ => $"Caduca en {DaysRemaining} días"
    };
    public string DisplayQuantity => UnitOfMeasure switch
    {
        UnitOfMeasure.Kilogram => $"{Quantity:0.###} kg",
        UnitOfMeasure.Liter => $"{Quantity:0.###} L",
        _ => $"{Quantity:0.###} unidades"
    };
}

public sealed record ExpirationSummary(
    int ExpiredProducts,
    int ExpiringProducts,
    int MissingDateProducts,
    int NeedsSetupProducts);

public sealed class Supplier
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public IReadOnlyList<string> Phones { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Emails { get; set; } = Array.Empty<string>();
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

public sealed record SupplierProductCatalogItem(Product Product, decimal? ReferenceCost)
{
    public string DisplayReferenceCost => ReferenceCost.HasValue
        ? $"Costo sugerido: {ReferenceCost.Value:C}"
        : "Sin costo sugerido";
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

    public string DisplayStatus => Status switch
    {
        InventoryDocumentStatus.Draft => "Borrador",
        InventoryDocumentStatus.Confirmed => "Confirmada",
        _ => "Cancelada"
    };
    public string DisplayDate => (ConfirmedAt ?? CreatedAt).ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    public string DisplayTotal => Total.ToString("C", CultureInfo.CurrentCulture);
    public string DisplayLineCount => $"{Lines.Count} renglón(es)";
}

public sealed class InventoryDocumentLine
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public long? LotId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? LotCode { get; set; }
    public DateOnly? ManufacturingDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public decimal Subtotal => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    public string DisplayLot => $"Lote {LotCode ?? "Sin código"}" +
        (ExpirationDate.HasValue ? $" · Caduca {ExpirationDate:dd/MM/yyyy}" : " · Sin caducidad");
    public string DisplayAmounts => $"{Quantity:0.###} × {UnitPrice:C} = {Subtotal:C}";
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
    public long? InventoryCountId { get; set; }
    public DateTime OccurredAt { get; set; }
    public List<InventoryMovementLot> LotAllocations { get; set; } = [];
}

public sealed record InventoryMovementLot(long LotId, decimal Quantity);

public sealed class PurchaseOrder
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string Folio { get; set; } = string.Empty;
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? ManualSupplierName { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly? EstimatedDate { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public string? Notes { get; set; }
    public decimal EstimatedTotal { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public List<PurchaseOrderLine> Lines { get; set; } = [];

    public string DisplaySupplier => SupplierName ?? ManualSupplierName ?? "Sin proveedor especificado";
    public string DisplayStatus => Status switch
    {
        PurchaseOrderStatus.Draft => "Borrador",
        PurchaseOrderStatus.Pending => "Pendiente",
        PurchaseOrderStatus.Confirmed => "Confirmado",
        PurchaseOrderStatus.PartiallyReceived => "Recibido parcialmente",
        PurchaseOrderStatus.Received => "Recibido",
        _ => "Cancelado"
    };
    public string DisplayDate => OrderDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);
    public string DisplayTotal => EstimatedTotal.ToString("C", CultureInfo.CurrentCulture);
}

public sealed class PurchaseOrderLine
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? Sku { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; }
    public decimal? EstimatedUnitCost { get; set; }
    public string? Notes { get; set; }

    public decimal PendingQuantity => Math.Max(0m, RequestedQuantity - ReceivedQuantity);
    public decimal EstimatedSubtotal => decimal.Round(
        RequestedQuantity * (EstimatedUnitCost ?? 0m),
        2,
        MidpointRounding.AwayFromZero);
    public string DisplayQuantity => $"Solicitado: {RequestedQuantity:0.###} · Recibido: {ReceivedQuantity:0.###} · Pendiente: {PendingQuantity:0.###}";
}

public sealed class PurchaseReceipt
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public long OrderId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string OperationKey { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PurchaseReceiptLine> Lines { get; set; } = [];
}

public sealed class PurchaseReceiptLine
{
    public long Id { get; set; }
    public long ReceiptId { get; set; }
    public long OrderLineId { get; set; }
    public long ProductId { get; set; }
    public long LotId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
}

public sealed record MinimumStockAlert(
    long ProductId,
    string ProductName,
    string Code,
    decimal Stock,
    decimal MinimumStock,
    UnitOfMeasure UnitOfMeasure,
    string Status)
{
    public string DisplayStock => $"Actual: {Stock:0.###} · Mínimo: {MinimumStock:0.###} · {UnitOfMeasure switch
    {
        UnitOfMeasure.Kilogram => "kg",
        UnitOfMeasure.Liter => "L",
        _ => "unidades"
    }}";
}

public sealed record DashboardSummary(
    int MinimumStockProducts,
    int ExpiringLots,
    int ExpiredLots,
    int PendingOrders,
    int PartiallyReceivedOrders);

public sealed record InventoryDashboard(
    DashboardSummary Summary,
    IReadOnlyList<MinimumStockAlert> MinimumStock,
    IReadOnlyList<ExpirationAlert> ExpiringLots,
    IReadOnlyList<ExpirationAlert> ExpiredLots);

public sealed class InventoryCount
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public InventoryCountType Type { get; set; } = InventoryCountType.FreeOperational;
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? Brand { get; set; }
    public InventoryCountStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CountedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public List<InventoryCountLine> Lines { get; set; } = [];

    public int TotalProducts => Lines.Count;
    public int CountedProducts => Lines.Count(line => line.Counted);
    public int PendingProducts => TotalProducts - CountedProducts;
    public bool IsEditable => Status is InventoryCountStatus.Draft or InventoryCountStatus.InProgress;
    public string DisplayMode => Type switch
    {
        InventoryCountType.BySupplier => $"Por proveedor · {SupplierName ?? "Sin proveedor"}",
        InventoryCountType.ByBrand => $"Por marca · {Brand ?? "Sin marca"}",
        _ => "Inventario operativo"
    };
    public string DisplayProgress => $"{CountedProducts} de {TotalProducts} productos contados";
}

public sealed class InventoryCountLine
{
    public long Id { get; set; }
    public long CountId { get; set; }
    public long ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    // Campo legado conservado únicamente para compatibilidad con bases anteriores. No se usa en la UI ni en búsquedas.
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; }
    public ExpirationMode ExpirationMode { get; set; }
    public decimal TheoreticalStock { get; set; }
    public decimal? PhysicalStock { get; set; }
    public DateTime? CountedAt { get; set; }
    public string? Observations { get; set; }
    public bool CountByLot { get; set; }
    public List<InventoryCountLotLine> LotLines { get; set; } = [];
    public bool Counted => PhysicalStock.HasValue;
    public decimal Difference => Counted ? PhysicalStock!.Value - TheoreticalStock : 0m;
    public decimal Missing => Difference < 0 ? decimal.Abs(Difference) : 0m;
    public decimal Surplus => Difference > 0 ? Difference : 0m;
    public string UnitSymbol => UnitOfMeasure switch
    {
        UnitOfMeasure.Kilogram => "kg",
        UnitOfMeasure.Liter => "L",
        _ => "u"
    };
}

public sealed class InventoryCountLotLine
{
    public long Id { get; set; }
    public long CountLineId { get; set; }
    public long LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public DateOnly? ExpirationDate { get; set; }
    public decimal TheoreticalQuantity { get; set; }
    public decimal? PhysicalQuantity { get; set; }
    public DateTime? CountedAt { get; set; }
    public string? Observations { get; set; }
    public bool Counted => PhysicalQuantity.HasValue;
    public decimal Difference => Counted ? PhysicalQuantity!.Value - TheoreticalQuantity : 0m;
    public string ExpirationStatus => ExpirationDate is null
        ? "Sin caducidad"
        : ExpirationDate < DateOnly.FromDateTime(DateTime.Today)
            ? "Caducado"
            : ExpirationDate <= DateOnly.FromDateTime(DateTime.Today).AddDays(7)
                ? "Próximo a caducar"
                : "Vigente";
}

public sealed record InventoryCountProgress(int Total, int Counted, int Pending)
{
    public static InventoryCountProgress From(InventoryCount count) =>
        new(count.TotalProducts, count.CountedProducts, count.PendingProducts);
}

public sealed record InventoryCountSummary(
    int WithoutDifference,
    int WithMissing,
    int WithSurplus,
    int Pending);

public sealed record ProductInput(
    string? Sku,
    string? Barcode,
    string Name,
    string? Description,
    string? Brand,
    UnitOfMeasure UnitOfMeasure,
    decimal MinimumStock,
    decimal SalePrice,
    bool Active = true,
    ExpirationMode ExpirationMode = ExpirationMode.Unknown,
    DateOnly? InitialExpirationDate = null,
    string? InitialLotCode = null);

public sealed record SupplierInput(
    string CompanyName,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Country,
    string? State,
    string? Address,
    string? Notes,
    bool Active = true,
    IReadOnlyList<string>? Phones = null,
    IReadOnlyList<string>? Emails = null);

public sealed record ProductSupplierInput(
    long ProductId,
    long SupplierId,
    string? SupplierSku,
    decimal? ReferenceCost,
    bool Active = true);

public sealed record InventoryDocumentLineInput(
    long ProductId,
    decimal Quantity,
    decimal UnitPrice,
    string? LotCode = null,
    DateOnly? ManufacturingDate = null,
    DateOnly? ExpirationDate = null,
    long? LotId = null);

public sealed record InventoryAdjustmentInput(long ProductId, decimal Quantity, string Reason);

public sealed record InventoryCountLineInput(long ProductId, decimal PhysicalStock);

public sealed record InventoryCountSessionInput(
    InventoryCountType Type,
    long? SupplierId = null,
    string? Brand = null,
    string? Notes = null);

public sealed record InventoryLotReceiptInput(
    long ProductId,
    decimal Quantity,
    ExpirationMode ExpirationMode,
    DateOnly? ExpirationDate = null,
    string? LotCode = null,
    long? SupplierId = null,
    DateOnly? ManufacturingDate = null,
    decimal? UnitCost = null,
    long? PurchaseOrderId = null,
    long? ReceiptId = null);

public sealed record InventoryLotUpdateInput(
    string? LotCode,
    long? SupplierId,
    DateOnly? ManufacturingDate,
    DateOnly? ExpirationDate,
    decimal? UnitCost);

public sealed record InventoryLotAdjustmentInput(
    long LotId,
    decimal QuantityChange,
    string Reason);

public sealed record PurchaseOrderInput(
    long? SupplierId,
    string? ManualSupplierName,
    DateOnly OrderDate,
    DateOnly? EstimatedDate,
    string? Notes,
    IReadOnlyList<PurchaseOrderLineInput> Lines,
    string? Folio = null,
    PurchaseOrderStatus InitialStatus = PurchaseOrderStatus.Pending);

public sealed record PurchaseOrderLineInput(
    long? ProductId,
    string? Description,
    string? Barcode,
    string? Sku,
    decimal Quantity,
    UnitOfMeasure UnitOfMeasure,
    decimal? EstimatedUnitCost = null,
    string? Notes = null);

public sealed record PurchaseReceiptInput(
    long OrderLineId,
    long ProductId,
    decimal Quantity,
    string? LotCode = null,
    DateOnly? ManufacturingDate = null,
    DateOnly? ExpirationDate = null,
    decimal? UnitCost = null);

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

        if (!Enum.IsDefined(input.ExpirationMode))
        {
            throw new InventoryRuleException("El manejo de caducidad no es válido.");
        }
    }

    public static void ValidateSupplier(SupplierInput input)
    {
        if (string.IsNullOrWhiteSpace(input.CompanyName))
        {
            throw new InventoryRuleException("La empresa o razón social es obligatoria.");
        }

        var emails = input.Emails ?? (string.IsNullOrWhiteSpace(input.Email) ? Array.Empty<string>() : new[] { input.Email });
        if (emails.Any(email => !string.IsNullOrWhiteSpace(email) && !email.Contains('@', StringComparison.Ordinal)))
        {
            throw new InventoryRuleException("Uno de los correos del proveedor no es válido.");
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
