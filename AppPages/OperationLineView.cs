using System.ComponentModel;
using System.Runtime.CompilerServices;
using InventorySystem.Domain;

namespace InventorySystem.AppPages;

public sealed class OperationLineView : INotifyPropertyChanged
{
    private decimal _quantity;
    private decimal _unitPrice;
    private InventoryLot? _selectedLot;

    public required Product Product { get; init; }
    public IReadOnlyList<InventoryLot> AvailableLots { get; init; } = [];

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity == value) return;
            _quantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(DisplayDetail));
        }
    }

    public decimal UnitPrice
    {
        get => _unitPrice;
        set
        {
            if (_unitPrice == value) return;
            _unitPrice = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(DisplayDetail));
        }
    }

    public InventoryLot? SelectedLot
    {
        get => _selectedLot;
        set
        {
            if (ReferenceEquals(_selectedLot, value)) return;
            _selectedLot = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedLotText));
            OnPropertyChanged(nameof(UsesExpiredLot));
        }
    }

    public string? LotCode { get; init; }
    public DateOnly? ManufacturingDate { get; init; }
    public DateOnly? ExpirationDate { get; init; }
    public decimal Subtotal => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    public string DisplayName => Product.Name;
    public string DisplayProductCode => string.IsNullOrWhiteSpace(Product.Barcode)
        ? $"ID {Product.Id}"
        : $"ID {Product.Id} · {Product.Barcode}";
    public string SelectedLotText => SelectedLot?.DisplaySelection ?? "Seleccione el lote de producto";
    public bool UsesExpiredLot => SelectedLot?.IsExpired == true;
    public string DisplayDetail => $"{Quantity:0.###} × {UnitPrice:C} = {Subtotal:C}" +
                                   (ExpirationDate.HasValue ? $" · Caduca {ExpirationDate:dd/MM/yyyy}" : string.Empty);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
