using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class ItemFullPage : ContentPage, IQueryAttributable
{
    private readonly ProductRepository _products;
    private readonly SupplierRepository _suppliers;
    private readonly BusinessService _businesses;
    private readonly InventoryLotService _lots;
    private long _businessId;
    private long _productId;

    public ItemFullPage(
        ProductRepository products,
        SupplierRepository suppliers,
        BusinessService businesses,
        InventoryLotService lots)
    {
        InitializeComponent();
        _products = products;
        _suppliers = suppliers;
        _businesses = businesses;
        _lots = lots;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("ProductId", out var value))
        {
            _productId = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            await RefreshAsync();
        }
        catch (Exception error)
        {
            DetailErrorLabel.Text = error.Message;
        }
    }

    private async void SaveProduct_Clicked(object? sender, EventArgs e)
    {
        try
        {
            DetailErrorLabel.Text = string.Empty;
            SaveProductButton.IsEnabled = false;
            var input = new ProductInput(
                null,
                BarcodeEntry.Text,
                NameEntry.Text ?? string.Empty,
                DescriptionEntry.Text,
                BrandEntry.Text,
                (UnitOfMeasure)Math.Max(UnitPicker.SelectedIndex, 0),
                ParseOptionalDecimal(MinimumStockEntry.Text, "El stock mínimo no es válido.") ?? 0m,
                ParseOptionalDecimal(SalePriceEntry.Text, "El precio de venta no es válido.") ?? 0m,
                ActiveSwitch.IsToggled,
                ExpirationModePicker.SelectedIndex == 1 ? ExpirationMode.NotApplicable : ExpirationMode.Tracked);
            await _products.SaveAsync(_businessId, input, productId: _productId);
            DetailErrorLabel.Style = (Style)Application.Current!.Resources["InfoText"];
            DetailErrorLabel.Text = "Cambios guardados.";
            await RefreshAsync();
        }
        catch (Exception error)
        {
            DetailErrorLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            DetailErrorLabel.Text = error.Message;
        }
        finally
        {
            SaveProductButton.IsEnabled = true;
        }
    }

    private async void DeleteProduct_Clicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Eliminar producto",
            "Si el producto ya tiene historial se archivará para conservar ventas, entradas y movimientos anteriores. Si nunca se ha utilizado, se eliminará definitivamente. ¿Deseas continuar?",
            "Eliminar",
            "Cancelar");
        if (!confirm) return;

        try
        {
            DeleteProductButton.IsEnabled = false;
            var deleted = await _products.DeleteOrArchiveAsync(_businessId, _productId);
            await DisplayAlertAsync(
                "Producto",
                deleted ? "El producto se eliminó definitivamente." : "El producto tiene historial y fue archivado.",
                "Aceptar");
            await Shell.Current.GoToAsync("//InventoryPage");
        }
        catch (Exception error)
        {
            DetailErrorLabel.Text = error.Message;
        }
        finally
        {
            DeleteProductButton.IsEnabled = true;
        }
    }

    private async void AddSupplier_Clicked(object? sender, EventArgs e)
    {
        if (AvailableSupplierPicker.SelectedItem is not Supplier supplier)
        {
            DetailErrorLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            DetailErrorLabel.Text = "Selecciona un proveedor para agregarlo.";
            return;
        }

        try
        {
            AddSupplierButton.IsEnabled = false;
            DetailErrorLabel.Text = string.Empty;
            await _suppliers.LinkProductAsync(
                _businessId,
                new ProductSupplierInput(_productId, supplier.Id, null, null));
            await RefreshSuppliersAsync();
        }
        catch (Exception error)
        {
            DetailErrorLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            DetailErrorLabel.Text = error.Message;
        }
        finally
        {
            AddSupplierButton.IsEnabled = AvailableSupplierPicker.IsEnabled;
        }
    }

    private async void OpenSupplier_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Supplier supplier })
        {
            return;
        }

        await Shell.Current.GoToAsync(
            nameof(PurveyorContactPage),
            new Dictionary<string, object> { ["SupplierId"] = supplier.Id });
    }

    private async void RemoveSupplier_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Supplier supplier })
        {
            return;
        }

        var confirm = await DisplayAlertAsync(
            "Quitar proveedor",
            $"¿Deseas quitar a {supplier.CompanyName} de los proveedores asociados a este producto? Los lotes y entradas anteriores conservarán su proveedor.",
            "Quitar",
            "Cancelar");
        if (!confirm)
        {
            return;
        }

        try
        {
            DetailErrorLabel.Text = string.Empty;
            await _suppliers.UnlinkProductAsync(_businessId, _productId, supplier.Id);
            await RefreshSuppliersAsync();
        }
        catch (Exception error)
        {
            DetailErrorLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            DetailErrorLabel.Text = error.Message;
        }
    }

    private async void LotList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not InventoryLot lot)
        {
            return;
        }

        LotList.SelectedItem = null;
        await Shell.Current.GoToAsync(
            nameof(LotDetailPage),
            new Dictionary<string, object> { ["LotId"] = lot.Id });
    }

    private async void OpenLots_Clicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//LotsPage");

    private async Task RefreshAsync()
    {
        if (_businessId == 0 || _productId == 0)
        {
            return;
        }

        var product = await _products.GetAsync(_businessId, _productId)
            ?? throw new InventoryRuleException("El producto no existe.");
        ProductNameLabel.Text = product.Name;
        ProductCodeLabel.Text = string.IsNullOrWhiteSpace(product.Barcode) ? $"ID {product.Id}" : $"ID {product.Id} · Código: {product.Barcode}";
        ProductStockLabel.Text = $"Existencia: {product.DisplayStock} · Mínimo: {product.MinimumStock:0.###}";
        ExpirationStatusLabel.Text = product.ExpirationMode switch
        {
            ExpirationMode.Tracked when product.NearestExpirationDate.HasValue => $"Próxima caducidad: {product.NearestExpirationDate:dd/MM/yyyy}",
            ExpirationMode.NotApplicable => "Caducidad: no aplica",
            _ => "Caducidad: sin lotes fechados"
        };

        BarcodeEntry.Text = product.Barcode;
        NameEntry.Text = product.Name;
        BrandEntry.Text = product.Brand;
        DescriptionEntry.Text = product.Description;
        UnitPicker.SelectedIndex = (int)product.UnitOfMeasure;
        ExpirationModePicker.SelectedIndex = product.ExpirationMode == ExpirationMode.NotApplicable ? 1 : 0;
        MinimumStockEntry.Text = product.MinimumStock.ToString(CultureInfo.CurrentCulture);
        SalePriceEntry.Text = product.SalePrice.ToString(CultureInfo.CurrentCulture);
        ActiveSwitch.IsToggled = product.Active;
        await RefreshSuppliersAsync();
        LotList.ItemsSource = await _lots.GetLotsAsync(_businessId, _productId);
    }

    private async Task RefreshSuppliersAsync()
    {
        var linked = await _suppliers.GetSuppliersForProductAsync(_businessId, _productId);
        SupplierList.ItemsSource = linked;

        var linkedIds = linked.Select(supplier => supplier.Id).ToHashSet();
        var available = (await _suppliers.SearchAsync(_businessId))
            .Where(supplier => !linkedIds.Contains(supplier.Id))
            .ToArray();
        AvailableSupplierPicker.ItemsSource = available;
        AvailableSupplierPicker.SelectedItem = null;
        AvailableSupplierPicker.IsEnabled = available.Length > 0;
        AddSupplierButton.IsEnabled = available.Length > 0;
        AvailableSupplierPicker.Title = available.Length > 0
            ? "Selecciona un proveedor"
            : "No hay proveedores disponibles";

        SupplierList.HeightRequest = linked.Count == 0
            ? 58
            : Math.Min(220, Math.Max(68, linked.Count * 58));
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0)
        {
            PageContent.WidthRequest = Math.Max(280, Math.Min(980, width - 56));
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
