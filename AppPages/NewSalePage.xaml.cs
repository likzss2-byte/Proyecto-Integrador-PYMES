using System.Collections.ObjectModel;
using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Services;

namespace InventorySystem.AppPages;

public partial class NewSalePage : ContentPage
{
    private readonly ProductRepository _products;
    private readonly InventoryTransactionService _transactions;
    private readonly BusinessService _businesses;
    private readonly BarcodeReadGuard _readGuard;
    private readonly BarcodeScannerCoordinator _cameraScanner;
    private readonly ObservableCollection<OperationLineView> _lines = [];
    private long _businessId;
    private long? _lastConfirmedSaleId;

    public NewSalePage(
        ProductRepository products,
        InventoryTransactionService transactions,
        BusinessService businesses,
        BarcodeReadGuard readGuard,
        BarcodeScannerCoordinator cameraScanner)
    {
        InitializeComponent();
        _products = products;
        _transactions = transactions;
        _businesses = businesses;
        _readGuard = readGuard;
        _cameraScanner = cameraScanner;
        LineList.ItemsSource = _lines;
    }

    private async void ScanCamera_Clicked(object? sender, EventArgs e)
    {
        var result = await _cameraScanner.ScanAsync("venta", "Escanear producto para salida");
        if (!result.Success || string.IsNullOrWhiteSpace(result.Code))
        {
            ResultLabel.Text = "Puedes escribir el código manualmente.";
            ProductCodeEntry.Focus();
            return;
        }

        ProductCodeEntry.Text = result.Code;
        ResultLabel.Style = (Style)Application.Current!.Resources["InfoText"];
        ResultLabel.Text = "Código detectado. Captura cantidad para agregarlo a la venta.";
        QuantityEntry.Focus();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
        }
        catch (Exception error)
        {
            ResultLabel.Text = error.Message;
        }
    }

    private async void AddLine_Clicked(object? sender, EventArgs e)
    {
        if (!_readGuard.TryAccept("venta", ProductCodeEntry.Text, out var code))
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
                throw new InventoryRuleException("El producto ya está incluido en la venta.");
            }

            var quantity = ParseDecimal(QuantityEntry.Text, "La cantidad no es válida.");
            InventoryRules.ValidateQuantity(quantity, product.UnitOfMeasure);
            var price = string.IsNullOrWhiteSpace(UnitPriceEntry.Text)
                ? product.SalePrice
                : ParseDecimal(UnitPriceEntry.Text, "El precio unitario no es válido.");
            if (price < 0)
            {
                throw new InventoryRuleException("El precio unitario no puede ser negativo.");
            }

            _lines.Add(new OperationLineView { Product = product, Quantity = quantity, UnitPrice = price });
            ProductCodeEntry.Text = string.Empty;
            QuantityEntry.Text = string.Empty;
            UnitPriceEntry.Text = string.Empty;
            _readGuard.Reset("venta");
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

    private async void ConfirmSale_Clicked(object? sender, EventArgs e)
    {
        try
        {
            ConfirmSaleButton.IsEnabled = false;
            var allowExpired = AllowExpiredLotsCheck.IsChecked;
            if (allowExpired)
            {
                var confirmed = await DisplayAlertAsync(
                    "Despacho de producto caducado",
                    "Has autorizado usar lotes caducados si no alcanza el inventario vigente. ¿Deseas continuar?",
                    "Sí, continuar",
                    "Cancelar");
                if (!confirmed)
                {
                    return;
                }
            }

            var document = await _transactions.CreateSaleAsync(
                _businessId,
                _lines.Select(line => new InventoryDocumentLineInput(line.Product.Id, line.Quantity, line.UnitPrice)),
                NotesEntry.Text);
            document = await _transactions.ConfirmAsync(_businessId, document.Id, allowExpired);
            _lastConfirmedSaleId = document.Id;
            ResultLabel.Style = (Style)Application.Current!.Resources["InfoText"];
            ResultLabel.Text = $"Venta {document.Reference} confirmada por {document.Total:C}.";
            CancellationPanel.IsVisible = true;
            _lines.Clear();
            AllowExpiredLotsCheck.IsChecked = false;
            UpdateTotal();
        }
        catch (Exception error)
        {
            ResultLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            ResultLabel.Text = error.Message;
        }
        finally
        {
            ConfirmSaleButton.IsEnabled = true;
        }
    }

    private async void CancelSale_Clicked(object? sender, EventArgs e)
    {
        if (!_lastConfirmedSaleId.HasValue)
        {
            return;
        }

        try
        {
            var cancelled = await _transactions.CancelAsync(
                _businessId,
                _lastConfirmedSaleId.Value,
                CancelReasonEntry.Text ?? string.Empty);
            ResultLabel.Text = $"Venta {cancelled.Reference} cancelada y stock repuesto en sus lotes originales.";
            _lastConfirmedSaleId = null;
            CancelReasonEntry.Text = string.Empty;
            CancellationPanel.IsVisible = false;
        }
        catch (Exception error)
        {
            ResultLabel.Text = error.Message;
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
