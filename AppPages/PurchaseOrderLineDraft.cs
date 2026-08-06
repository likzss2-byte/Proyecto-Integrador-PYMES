using InventorySystem.Domain;

namespace InventorySystem.AppPages;

public sealed class PurchaseOrderLineDraft
{
    public long? ProductId { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public string? Sku { get; init; }
    public decimal Quantity { get; init; }
    public UnitOfMeasure UnitOfMeasure { get; init; }
    public decimal? EstimatedUnitCost { get; init; }
    public string? Notes { get; init; }

    public string DisplayDetail => $"{Quantity:0.###} {UnitOfMeasure switch
    {
        UnitOfMeasure.Kilogram => "kg",
        UnitOfMeasure.Liter => "L",
        _ => "unidades"
    }} · {(EstimatedUnitCost ?? 0m):C} c/u";
}
