using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Services;

namespace InventorySystem.AppPages;

public partial class NewItemPage : ContentPage
{
    private readonly ProductRepository _products;
    private readonly ProductLookupService _lookup;
    private readonly BarcodeScannerService _scanner;
    private readonly BarcodeScannerCoordinator _cameraScanner;
    private readonly BarcodeReadGuard _readGuard;
    private readonly BusinessService _businesses;
    private long _businessId;

    public NewItemPage(
        ProductRepository products,
        ProductLookupService lookup,
        BarcodeScannerService scanner,
        BarcodeScannerCoordinator cameraScanner,
        BarcodeReadGuard readGuard,
        BusinessService businesses)
    {
        InitializeComponent();
        _products = products;
        _lookup = lookup;
        _scanner = scanner;
        _cameraScanner = cameraScanner;
        _readGuard = readGuard;
        _businesses = businesses;
        UnitPicker.SelectedIndex = 0;
        ExpirationModePicker.SelectedIndex = 0;
        InitialExpirationDatePicker.Date = DateTime.Today.AddDays(30);
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
            FormErrorLabel.Text = error.Message;
        }
    }

    private async void LookupCode_Clicked(object? sender, EventArgs e) => await LookupAsync(LookupCode.Text);

    private async void LookupCode_Completed(object? sender, EventArgs e) => await LookupAsync(LookupCode.Text);

    private async void ScanCamera_Clicked(object? sender, EventArgs e)
    {
        var result = await _cameraScanner.ScanAsync("registro-producto", "Escanear producto");
        if (!result.Success || string.IsNullOrWhiteSpace(result.Code))
        {
            LookupMessage.Text = "Puedes escribir el código manualmente.";
            LookupCode.Focus();
            return;
        }

        LookupCode.Text = result.Code;
        await LookupAsync(result.Code);
    }

    private async void ReadImage_Clicked(object? sender, EventArgs e)
    {
        try
        {
            var selected = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona una imagen con código",
                FileTypes = FilePickerFileType.Images
            });
            if (selected is null)
            {
                return;
            }

            var result = await _scanner.DecodeImageAsync(selected.FullPath);
            if (!result.Success)
            {
                LookupMessage.Text = result.Error ?? "No se detectó un código.";
                return;
            }

            LookupCode.Text = result.Code;
            await LookupAsync(result.Code);
        }
        catch (Exception error) when (error is not InventoryRuleException)
        {
            LookupMessage.Text = $"No se pudo abrir la imagen. {error.Message}";
        }
    }

    private async Task LookupAsync(string? rawCode)
    {
        if (!_readGuard.TryAccept("registro-producto", rawCode, out var code))
        {
            LookupMessage.Text = string.IsNullOrWhiteSpace(rawCode)
                ? "Captura o escanea un código."
                : "Lectura duplicada ignorada.";
            return;
        }

        try
        {
            LookupMessage.Text = "Buscando primero en el inventario local…";
            var result = await _lookup.LookupAsync(_businessId, code);
            if (result.LocalProduct is not null)
            {
                Populate(result.LocalProduct);
                LookupMessage.Text = "El producto ya existe en el inventario local.";
                return;
            }

            if (result.ExternalSuggestion is not null)
            {
                var useSuggestion = await DisplayAlertAsync(
                    "Producto encontrado",
                    $"{result.ExternalSuggestion.Name}\n{result.ExternalSuggestion.Brand}\n\nLos datos no se guardarán hasta que confirmes el formulario.",
                    "Usar datos",
                    "Cancelar");
                if (useSuggestion)
                {
                    Populate(result.ExternalSuggestion);
                    LookupMessage.Text = $"Datos sugeridos por {result.ExternalSuggestion.Source}. Revisa y guarda para confirmar.";
                }

                return;
            }

            BarcodeEntry.Text = code;
            LookupMessage.Text = "Código desconocido. Completa el formulario; no se guardó automáticamente.";
        }
        catch (Exception error)
        {
            LookupMessage.Text = error.Message;
        }
    }

    private async void SaveProduct_Clicked(object? sender, EventArgs e)
    {
        try
        {
            ClearValidationMessages();
            SaveProductButton.IsEnabled = false;
            if (ExpirationModePicker.SelectedIndex <= 0)
            {
                ExpirationModeErrorLabel.Text = "Selecciona si el producto controla caducidad.";
                return;
            }

            var expirationMode = ExpirationModePicker.SelectedIndex == 1
                ? ExpirationMode.Tracked
                : ExpirationMode.NotApplicable;
            var initialStock = ParseOptionalDecimal(InitialStockEntry.Text, "El inventario inicial no es válido.") ?? 0m;
            var input = new ProductInput(
                SkuEntry.Text ?? string.Empty,
                BarcodeEntry.Text,
                NameEntry.Text ?? string.Empty,
                DescriptionEntry.Text,
                BrandEntry.Text,
                (UnitOfMeasure)Math.Max(UnitPicker.SelectedIndex, 0),
                ParseOptionalDecimal(MinimumStockEntry.Text, "El stock mínimo no es válido.") ?? 0m,
                ParseOptionalDecimal(SalePriceEntry.Text, "El precio de venta no es válido.") ?? 0m,
                ActiveSwitch.IsToggled,
                expirationMode,
                expirationMode == ExpirationMode.Tracked && initialStock > 0
                    ? DateOnly.FromDateTime(InitialExpirationDatePicker.Date ?? DateTime.Today)
                    : null,
                InitialLotCodeEntry.Text);
            var saved = await _products.SaveAsync(_businessId, input, initialStock);
            await DisplayAlertAsync("Producto", $"{saved.Name} se guardó correctamente.", "Aceptar");
            ClearForm();
        }
        catch (Exception error)
        {
            ShowValidationError(error.Message);
        }
        finally
        {
            SaveProductButton.IsEnabled = true;
        }
    }

    private void ExpirationModePicker_Changed(object? sender, EventArgs e) =>
        ExpirationDetailsPanel.IsVisible = ExpirationModePicker.SelectedIndex == 1;

    private void Populate(Product product)
    {
        SkuEntry.Text = product.Sku;
        BarcodeEntry.Text = product.Barcode;
        NameEntry.Text = product.Name;
        BrandEntry.Text = product.Brand;
        DescriptionEntry.Text = product.Description;
        UnitPicker.SelectedIndex = (int)product.UnitOfMeasure;
        ExpirationModePicker.SelectedIndex = product.ExpirationMode switch
        {
            ExpirationMode.Tracked => 1,
            ExpirationMode.NotApplicable => 2,
            _ => 0
        };
        MinimumStockEntry.Text = product.MinimumStock.ToString(CultureInfo.CurrentCulture);
        SalePriceEntry.Text = product.SalePrice.ToString(CultureInfo.CurrentCulture);
        ActiveSwitch.IsToggled = product.Active;
    }

    private void Populate(ExternalProduct product)
    {
        BarcodeEntry.Text = product.Barcode;
        NameEntry.Text = product.Name;
        BrandEntry.Text = product.Brand;
        DescriptionEntry.Text = product.Description;
        if (string.IsNullOrWhiteSpace(SkuEntry.Text))
        {
            var segment = new string(product.Name.ToUpperInvariant().Where(char.IsLetterOrDigit).Take(8).ToArray());
            SkuEntry.Text = $"EXT-{(segment.Length == 0 ? "ITEM" : segment)}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
        }
    }

    private void ClearForm()
    {
        LookupCode.Text = string.Empty;
        LookupMessage.Text = string.Empty;
        SkuEntry.Text = string.Empty;
        BarcodeEntry.Text = string.Empty;
        NameEntry.Text = string.Empty;
        BrandEntry.Text = string.Empty;
        DescriptionEntry.Text = string.Empty;
        UnitPicker.SelectedIndex = 0;
        ExpirationModePicker.SelectedIndex = 0;
        InitialStockEntry.Text = string.Empty;
        MinimumStockEntry.Text = string.Empty;
        SalePriceEntry.Text = string.Empty;
        InitialLotCodeEntry.Text = string.Empty;
        InitialExpirationDatePicker.Date = DateTime.Today.AddDays(30);
        ActiveSwitch.IsToggled = true;
        ClearValidationMessages();
        _readGuard.Reset("registro-producto");
    }

    private void ClearValidationMessages()
    {
        SkuErrorLabel.Text = string.Empty;
        BarcodeErrorLabel.Text = string.Empty;
        NameErrorLabel.Text = string.Empty;
        NumericErrorLabel.Text = string.Empty;
        ExpirationModeErrorLabel.Text = string.Empty;
        FormErrorLabel.Text = string.Empty;
    }

    private void ShowValidationError(string message)
    {
        if (message.Contains("SKU", StringComparison.OrdinalIgnoreCase))
        {
            SkuErrorLabel.Text = message;
        }
        else if (message.Contains("código de barras", StringComparison.OrdinalIgnoreCase))
        {
            BarcodeErrorLabel.Text = message;
        }
        else if (message.Contains("nombre", StringComparison.OrdinalIgnoreCase))
        {
            NameErrorLabel.Text = message;
        }
        else if (message.Contains("stock", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("precio", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("inventario", StringComparison.OrdinalIgnoreCase))
        {
            NumericErrorLabel.Text = message;
        }
        else
        {
            FormErrorLabel.Text = message;
        }
    }

    private static decimal ParseDecimal(string? value, string message)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var result) ||
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
        {
            return result;
        }

        throw new InventoryRuleException(message);
    }

    private static decimal? ParseOptionalDecimal(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseDecimal(value, message);
}
