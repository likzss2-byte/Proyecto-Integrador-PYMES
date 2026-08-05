using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class NewItemPage : ContentPage
{
    private readonly ProductRepository _products;
    private readonly ProductLookupService _lookup;
    private readonly BarcodeScannerService _scanner;
    private readonly BarcodeReadGuard _readGuard;
    private readonly BusinessService _businesses;
    private long _businessId;

    public NewItemPage(
        ProductRepository products,
        ProductLookupService lookup,
        BarcodeScannerService scanner,
        BarcodeReadGuard readGuard,
        BusinessService businesses)
    {
        InitializeComponent();
        _products = products;
        _lookup = lookup;
        _scanner = scanner;
        _readGuard = readGuard;
        _businesses = businesses;
        UnitPicker.SelectedIndex = 0;
        InitialStockEntry.Text = "0";
        MinimumStockEntry.Text = "0";
        SalePriceEntry.Text = "0";
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
            await DisplayAlertAsync("Producto", error.Message, "Aceptar");
        }
    }

    private async void LookupCode_Clicked(object? sender, EventArgs e) => await LookupAsync(LookupCode.Text);

    private async void LookupCode_Completed(object? sender, EventArgs e) => await LookupAsync(LookupCode.Text);

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
                await DisplayAlertAsync("Lectura", result.Error ?? "No se detectó un código.", "Aceptar");
                return;
            }

            LookupCode.Text = result.Code;
            await LookupAsync(result.Code);
        }
        catch (Exception error) when (error is not InventoryRuleException)
        {
            await DisplayAlertAsync("Lectura", $"No se pudo abrir la imagen. {error.Message}", "Aceptar");
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
            LookupMessage.Text = "Código desconocido. Completa el formulario para registrarlo; no se guardó automáticamente.";
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
            var input = new ProductInput(
                SkuEntry.Text ?? string.Empty,
                BarcodeEntry.Text,
                NameEntry.Text ?? string.Empty,
                DescriptionEntry.Text,
                BrandEntry.Text,
                (UnitOfMeasure)Math.Max(UnitPicker.SelectedIndex, 0),
                ParseDecimal(MinimumStockEntry.Text, "El stock mínimo no es válido."),
                ParseDecimal(SalePriceEntry.Text, "El precio de venta no es válido."),
                ActiveSwitch.IsToggled);
            var saved = await _products.SaveAsync(
                _businessId,
                input,
                ParseDecimal(InitialStockEntry.Text, "El inventario inicial no es válido."));
            await DisplayAlertAsync("Producto", $"{saved.Name} se guardó correctamente.", "Aceptar");
            ClearForm();
        }
        catch (Exception error)
        {
            await DisplayAlertAsync("Producto", error.Message, "Aceptar");
        }
    }

    private void Populate(Product product)
    {
        SkuEntry.Text = product.Sku;
        BarcodeEntry.Text = product.Barcode;
        NameEntry.Text = product.Name;
        BrandEntry.Text = product.Brand;
        DescriptionEntry.Text = product.Description;
        UnitPicker.SelectedIndex = (int)product.UnitOfMeasure;
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
        InitialStockEntry.Text = "0";
        MinimumStockEntry.Text = "0";
        SalePriceEntry.Text = "0";
        ActiveSwitch.IsToggled = true;
        _readGuard.Reset("registro-producto");
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
}
