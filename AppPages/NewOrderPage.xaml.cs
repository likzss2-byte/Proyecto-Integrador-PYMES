using System.Collections.ObjectModel;
using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class NewOrderPage : ContentPage
{
    private readonly ProductRepository _products;
    private readonly SupplierRepository _suppliers;
    private readonly InventoryTransactionService _transactions;
    private readonly BusinessService _businesses;
    private readonly BarcodeReadGuard _readGuard;
    private readonly ObservableCollection<OperationLineView> _lines = [];
    private long _businessId;
    private long? _lastConfirmedEntryId;

    public NewOrderPage(
        ProductRepository products,
        SupplierRepository suppliers,
        InventoryTransactionService transactions,
        BusinessService businesses,
        BarcodeReadGuard readGuard)
    {
        InitializeComponent();
        _products = products;
        _suppliers = suppliers;
        _transactions = transactions;
        _businesses = businesses;
        _readGuard = readGuard;
        LineList.ItemsSource = _lines;
        QuantityEntry.Text = "1";
        UnitCostEntry.Text = "0";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            SupplierPicker.ItemsSource = (await _suppliers.SearchAsync(_businessId)).ToList();
            SupplierPicker.ItemDisplayBinding = new Binding(nameof(Supplier.CompanyName));
        }
        catch (Exception error)
        {
            await DisplayAlertAsync("Entrada", error.Message, "Aceptar");
        }
    }

    private async void AddLine_Clicked(object? sender, EventArgs e)
    {
        if (!_readGuard.TryAccept("entrada", ProductCodeEntry.Text, out var code))
        {
            ResultLabel.Text = string.IsNullOrWhiteSpace(ProductCodeEntry.Text)
                ? "Captura un código o SKU."
                : "Lectura duplicada ignorada.";
            return;
        }

        try
        {
            var product = await _products.FindByCodeAsync(_businessId, code);
            if (product is null)
            {
                var register = await DisplayAlertAsync(
                    "Producto desconocido",
                    "El código no existe. ¿Quieres abrir el registro de producto?",
                    "Registrar",
                    "Cancelar");
                if (register)
                {
                    await Shell.Current.GoToAsync("//NewItemPage");
                }

                return;
            }

            if (_lines.Any(line => line.Product.Id == product.Id))
            {
                throw new InventoryRuleException("El producto ya está incluido en la entrada.");
            }

            var quantity = ParseDecimal(QuantityEntry.Text, "La cantidad no es válida.");
            InventoryRules.ValidateQuantity(quantity, product.UnitOfMeasure);
            var cost = ParseDecimal(UnitCostEntry.Text, "El costo unitario no es válido.");
            if (cost < 0)
            {
                throw new InventoryRuleException("El costo unitario no puede ser negativo.");
            }

            _lines.Add(new OperationLineView { Product = product, Quantity = quantity, UnitPrice = cost });
            ProductCodeEntry.Text = string.Empty;
            QuantityEntry.Text = "1";
            UnitCostEntry.Text = "0";
            _readGuard.Reset("entrada");
            UpdateTotal();
        }
        catch (Exception error)
        {
            ResultLabel.Text = error.Message;
        }
    }

    private void RemoveLine_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: OperationLineView line })
        {
            _lines.Remove(line);
            UpdateTotal();
        }
    }

    private async void ConfirmEntry_Clicked(object? sender, EventArgs e)
    {
        try
        {
            var document = await _transactions.CreateEntryAsync(
                _businessId,
                _lines.Select(line => new InventoryDocumentLineInput(line.Product.Id, line.Quantity, line.UnitPrice)),
                (SupplierPicker.SelectedItem as Supplier)?.Id,
                NotesEntry.Text);
            document = await _transactions.ConfirmAsync(_businessId, document.Id);
            _lastConfirmedEntryId = document.Id;
            ResultLabel.Text = $"Entrada {document.Reference} confirmada por {document.Total:C}.";
            CancelReasonEntry.IsVisible = true;
            CancelEntryButton.IsVisible = true;
            _lines.Clear();
            UpdateTotal();
        }
        catch (Exception error)
        {
            await DisplayAlertAsync("Entrada", error.Message, "Aceptar");
        }
    }

    private async void CancelEntry_Clicked(object? sender, EventArgs e)
    {
        if (!_lastConfirmedEntryId.HasValue)
        {
            return;
        }

        try
        {
            var cancelled = await _transactions.CancelAsync(
                _businessId,
                _lastConfirmedEntryId.Value,
                CancelReasonEntry.Text ?? string.Empty);
            ResultLabel.Text = $"Entrada {cancelled.Reference} cancelada y stock revertido.";
            _lastConfirmedEntryId = null;
            CancelReasonEntry.Text = string.Empty;
            CancelReasonEntry.IsVisible = false;
            CancelEntryButton.IsVisible = false;
        }
        catch (Exception error)
        {
            await DisplayAlertAsync("Cancelación", error.Message, "Aceptar");
        }
    }

    private void UpdateTotal() => TotalLabel.Text = $"Total: {_lines.Sum(line => line.Subtotal):C}";

    private static decimal ParseDecimal(string? value, string message)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var result) ||
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
        {
            return result;
        }

        throw new InventoryRuleException(message);
    }
}
