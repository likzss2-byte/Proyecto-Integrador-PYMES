using InventorySystem.Domain;

namespace InventorySystem.AppPages;

public sealed class OperationLineView
{
    public required Product Product { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string? LotCode { get; init; }
    public DateOnly? ManufacturingDate { get; init; }
    public DateOnly? ExpirationDate { get; init; }
    public decimal Subtotal => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    public string DisplayName => Product.Name;
    public string DisplayDetail => $"{Quantity:0.###} × {UnitPrice:C} = {Subtotal:C}" +
                                   (ExpirationDate.HasValue ? $" · Caduca {ExpirationDate:dd/MM/yyyy}" : string.Empty);
}
