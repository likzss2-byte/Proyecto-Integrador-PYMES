using System.Collections.ObjectModel;
using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Services;

namespace InventorySystem.AppPages;

public partial class NewOrderPage : ContentPage
{
    private readonly ProductRepository _products;
    private readonly SupplierRepository _suppliers;
    private readonly InventoryTransactionService _transactions;
    private readonly BusinessService _businesses;
    private readonly BarcodeReadGuard _readGuard;
    private readonly BarcodeScannerCoordinator _cameraScanner;
    private readonly ObservableCollection<OperationLineView> _lines = [];
    private long _businessId;
    private long? _lastConfirmedEntryId;

    public NewOrderPage(
        ProductRepository products,
        SupplierRepository suppliers,
        InventoryTransactionService transactions,
        BusinessService businesses,
        BarcodeReadGuard readGuard,
        BarcodeScannerCoordinator cameraScanner)
    {
        InitializeComponent();
        _products = products;
        _suppliers = suppliers;
        _transactions = transactions;
        _businesses = businesses;
        _readGuard = readGuard;
        _cameraScanner = cameraScanner;
        LineList.ItemsSource = _lines;
        ExpirationDatePicker.Date = DateTime.Today.AddDays(30);
    }

    private async void ScanCamera_Clicked(object? sender, EventArgs e)
    {
        var result = await _cameraScanner.ScanAsync("entrada", "Escanear producto para entrada");
        if (!result.Success || string.IsNullOrWhiteSpace(result.Code))
        {
            ResultLabel.Text = "Puedes escribir el código manualmente.";
            ProductCodeEntry.Focus();
            return;
        }

        ProductCodeEntry.Text = result.Code;
        ResultLabel.Style = (Style)Application.Current!.Resources["InfoText"];
        ResultLabel.Text = "Código detectado. Captura cantidad y costo para agregarlo a la entrada.";
        QuantityEntry.Focus();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await PageScroll.ScrollToAsync(0, 0, false);
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            SupplierPicker.ItemsSource = (await _suppliers.SearchAsync(_businessId)).ToList();
            SupplierPicker.ItemDisplayBinding = new Binding(nameof(Supplier.CompanyName));
        }
        catch (Exception error)
        {
            ResultLabel.Text = error.Message;
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
            ResultLabel.Text = string.Empty;
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

            if (product.ExpirationMode == ExpirationMode.Tracked && !HasExpirationDateCheck.IsChecked)
            {
                throw new InventoryRuleException("La fecha de caducidad es obligatoria para este producto.");
            }

            _lines.Add(new OperationLineView
            {
                Product = product,
                Quantity = quantity,
                UnitPrice = cost,
                LotCode = LotCodeEntry.Text,
                ManufacturingDate = null,
                ExpirationDate = HasExpirationDateCheck.IsChecked
                    ? DateOnly.FromDateTime(ExpirationDatePicker.Date ?? DateTime.Today)
                    : null
            });
            ProductCodeEntry.Text = string.Empty;
            QuantityEntry.Text = string.Empty;
            UnitCostEntry.Text = string.Empty;
            LotCodeEntry.Text = string.Empty;
            HasExpirationDateCheck.IsChecked = false;
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
            ConfirmEntryButton.IsEnabled = false;
            var document = await _transactions.CreateEntryAsync(
                _businessId,
                _lines.Select(line => new InventoryDocumentLineInput(
                    line.Product.Id,
                    line.Quantity,
                    line.UnitPrice,
                    line.LotCode,
                    line.ManufacturingDate,
                    line.ExpirationDate)),
                (SupplierPicker.SelectedItem as Supplier)?.Id,
                NotesEntry.Text);
            document = await _transactions.ConfirmAsync(_businessId, document.Id);
            _lastConfirmedEntryId = document.Id;
            ResultLabel.Style = (Style)Application.Current!.Resources["InfoText"];
            ResultLabel.Text = $"Entrada {document.Reference} confirmada por {document.Total:C}.";
            CancellationPanel.IsVisible = true;
            _lines.Clear();
            UpdateTotal();
        }
        catch (Exception error)
        {
            ResultLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            ResultLabel.Text = error.Message;
        }
        finally
        {
            ConfirmEntryButton.IsEnabled = true;
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
            ResultLabel.Text = $"Entrada {cancelled.Reference} cancelada y stock revertido sobre sus lotes originales.";
            _lastConfirmedEntryId = null;
            CancelReasonEntry.Text = string.Empty;
            CancellationPanel.IsVisible = false;
        }
        catch (Exception error)
        {
            ResultLabel.Text = error.Message;
        }
    }

    private void HasExpirationDateCheck_Changed(object? sender, CheckedChangedEventArgs e) =>
        ExpirationDatePicker.IsVisible = e.Value;

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
