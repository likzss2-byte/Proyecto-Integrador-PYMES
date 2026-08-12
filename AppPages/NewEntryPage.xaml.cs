using System.Collections.ObjectModel;
using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Services;

namespace InventorySystem.AppPages;

public partial class NewEntryPage : ContentPage
{
    private static readonly Supplier NoSupplierOption = new() { CompanyName = "Sin proveedor" };
    private readonly ProductRepository _products;
    private readonly SupplierRepository _suppliers;
    private readonly InventoryTransactionService _transactions;
    private readonly InventoryCatalogService _catalog;
    private readonly BusinessService _businesses;
    private readonly BarcodeScannerCoordinator _cameraScanner;
    private readonly ObservableCollection<OperationLineView> _lines = [];
    private readonly ObservableCollection<SupplierProductCatalogItem> _searchResults = [];
    private readonly ObservableCollection<Supplier> _supplierOptions = [];
    private CancellationTokenSource? _searchDebounce;
    private Product? _selectedProduct;
    private bool _ignoreSearchChange;
    private bool _ignoreSupplierChange;
    private long _businessId;

    public NewEntryPage(ProductRepository products, SupplierRepository suppliers, InventoryTransactionService transactions,
        InventoryCatalogService catalog, BusinessService businesses, BarcodeScannerCoordinator cameraScanner)
    {
        InitializeComponent();
        _products = products;
        _suppliers = suppliers;
        _transactions = transactions;
        _catalog = catalog;
        _businesses = businesses;
        _cameraScanner = cameraScanner;
        LineList.ItemsSource = _lines;
        ProductSearchResults.ItemsSource = _searchResults;
        SupplierPicker.ItemsSource = _supplierOptions;
        ProductSearch.SearchTextChanged += ProductSearch_TextChanged;
        ExpirationDatePicker.Date = DateTime.Today.AddDays(30);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            await RefreshSuppliersAsync();
            await RefreshProductCatalogAsync();
        }
        catch (Exception error) { ResultLabel.Text = error.Message; }
    }

    private async Task RefreshSuppliersAsync()
    {
        var selectedId = GetSelectedSupplier()?.Id;
        var suppliers = await _suppliers.SearchAsync(_businessId);

        _supplierOptions.Clear();
        _supplierOptions.Add(NoSupplierOption);
        foreach (var supplier in suppliers)
        {
            _supplierOptions.Add(supplier);
        }

        SupplierPicker.IsEnabled = _lines.Count == 0;
        SupplierPicker.Title = "Selecciona un proveedor";
        SupplierPicker.SelectedItem = selectedId.HasValue
            ? _supplierOptions.FirstOrDefault(supplier => supplier.Id == selectedId.Value)
            : NoSupplierOption;
    }

    private async void SupplierPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_ignoreSupplierChange)
        {
            return;
        }

        ClearProductSelection();
        _searchResults.Clear();
        if (GetSelectedSupplier() is not Supplier supplier)
        {
            SupplierCatalogTitle.Text = "Productos del inventario";
            CatalogEmptyLabel.Text = "No hay productos que coincidan con la búsqueda.";
            UnitCostHelpLabel.Text = "Captura el costo correspondiente a esta entrada.";
            await RefreshProductCatalogAsync();
            return;
        }

        SupplierCatalogTitle.Text = $"Productos de {supplier.CompanyName}";
        CatalogEmptyLabel.Text = "Este proveedor no tiene productos asociados que coincidan con la búsqueda.";
        UnitCostHelpLabel.Text = "Se completa con el costo registrado para este proveedor y puede ajustarse para esta entrada.";
        await RefreshProductCatalogAsync();
    }

    private async void ProductSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_ignoreSearchChange) return;
        _selectedProduct = null;
        SelectedProductLabel.Text = string.Empty;
        SelectedProductLabel.IsVisible = false;
        ExpirationSection.IsVisible = false;
        _searchDebounce?.Cancel();
        _searchDebounce?.Dispose();
        _searchDebounce = new CancellationTokenSource();
        var token = _searchDebounce.Token;
        try
        {
            await Task.Delay(250, token);
            await RefreshProductCatalogAsync(ProductSearch.Text, token, resetSelection: false);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { ResultLabel.Text = error.Message; }
    }

    private async void ProductSearchResults_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SupplierProductCatalogItem item) return;
        ProductSearchResults.SelectedItem = null;
        try
        {
            await SelectProductAsync(item);
        }
        catch (Exception error)
        {
            ResultLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            ResultLabel.Text = error.Message;
        }
    }

    private async Task SelectProductAsync(SupplierProductCatalogItem item)
    {
        if (GetSelectedSupplier() is not null || _lines.Count > 0)
        {
            SelectProduct(item);
            return;
        }

        var suppliers = await _suppliers.GetSuppliersForProductAsync(
            _businessId,
            item.Product.Id,
            includeInactive: false);
        if (suppliers.Count == 0)
        {
            SelectProduct(item);
            return;
        }

        Supplier? selectedSupplier;
        if (suppliers.Count == 1)
        {
            selectedSupplier = suppliers[0];
        }
        else
        {
            var choice = await DisplayActionSheetAsync(
                "Selecciona el proveedor para esta entrada",
                "Sin proveedor",
                null,
                suppliers.Select(supplier => supplier.CompanyName).ToArray());
            selectedSupplier = suppliers.FirstOrDefault(supplier => supplier.CompanyName == choice);
            if (selectedSupplier is null)
            {
                SelectProduct(item);
                return;
            }
        }

        var pickerSupplier = _supplierOptions.FirstOrDefault(supplier => supplier.Id == selectedSupplier.Id);
        if (pickerSupplier is null)
        {
            SelectProduct(item);
            return;
        }

        var relation = (await _suppliers.GetProductSuppliersAsync(_businessId, item.Product.Id))
            .FirstOrDefault(candidate => candidate.SupplierId == selectedSupplier.Id);
        _ignoreSupplierChange = true;
        SupplierPicker.SelectedItem = pickerSupplier;
        _ignoreSupplierChange = false;
        SupplierCatalogTitle.Text = $"Productos de {pickerSupplier.CompanyName}";
        CatalogEmptyLabel.Text = "Este proveedor no tiene productos asociados que coincidan con la búsqueda.";
        UnitCostHelpLabel.Text = "Se completa con el costo registrado para este proveedor y puede ajustarse para esta entrada.";
        await RefreshProductCatalogAsync();
        SelectProduct(new SupplierProductCatalogItem(item.Product, relation?.ReferenceCost));
    }

    private void SelectProduct(SupplierProductCatalogItem item)
    {
        var product = item.Product;
        _selectedProduct = product;
        _ignoreSearchChange = true;
        ProductSearch.SetText(product.Name);
        _ignoreSearchChange = false;
        SelectedProductLabel.Text = $"{product.Name} · {product.DisplayCode} · {product.DisplayStock}";
        SelectedProductLabel.IsVisible = true;
        UnitCostEntry.Text = item.ReferenceCost?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        ExpirationSection.IsVisible = product.ExpirationMode == ExpirationMode.Tracked;
        QuantityEntry.Focus();
    }

    private async void ScanCamera_Clicked(object? sender, EventArgs e)
    {
        var result = await _cameraScanner.ScanAsync("entrada", "Escanear producto para entrada");
        if (!result.Success || string.IsNullOrWhiteSpace(result.Code)) return;
        var product = await _products.FindByCodeAsync(_businessId, result.Code);
        if (product is { Active: true })
        {
            if (GetSelectedSupplier() is not Supplier supplier)
            {
                await SelectProductAsync(new SupplierProductCatalogItem(product, null));
                return;
            }

            var item = (await _catalog.GetSupplierProductCatalogAsync(_businessId, supplier.Id, result.Code))
                .FirstOrDefault(candidate => candidate.Product.Id == product.Id);
            if (item is not null)
            {
                SelectProduct(item);
                return;
            }

            ResultLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            ResultLabel.Text = $"{product.Name} no está asociado con {supplier.CompanyName}.";
            return;
        }
        var register = await DisplayAlertAsync("Producto desconocido", "El código no está registrado. ¿Quieres agregar el producto?", "Agregar producto", "Cancelar");
        if (register) await Shell.Current.GoToAsync("//NewItemPage");
    }

    private async void AddLine_Clicked(object? sender, EventArgs e)
    {
        try
        {
            ResultLabel.Text = string.Empty;
            var product = _selectedProduct ?? throw new InventoryRuleException("Selecciona un producto del inventario.");
            var quantity = ParseDecimal(QuantityEntry.Text, "La cantidad no es válida.");
            InventoryRules.ValidateQuantity(quantity, product.UnitOfMeasure);
            var cost = ParseDecimal(UnitCostEntry.Text, "El costo unitario no es válido.");
            if (cost < 0) throw new InventoryRuleException("El costo unitario no puede ser negativo.");

            _lines.Add(new OperationLineView
            {
                Product = product,
                Quantity = quantity,
                UnitPrice = cost,
                LotCode = LotCodeEntry.Text,
                ExpirationDate = product.ExpirationMode == ExpirationMode.Tracked
                    ? DateOnly.FromDateTime(ExpirationDatePicker.Date ?? DateTime.Today)
                    : null
            });
            SupplierPicker.IsEnabled = false;
            ClearProductSelection();
            await RefreshProductCatalogAsync();
            UpdateTotal();
        }
        catch (Exception error)
        {
            ResultLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            ResultLabel.Text = error.Message;
        }
    }

    private void ClearProductSelection()
    {
        _selectedProduct = null;
        _ignoreSearchChange = true;
        ProductSearch.SetText(string.Empty);
        _ignoreSearchChange = false;
        _searchResults.Clear();
        SelectedProductLabel.Text = string.Empty;
        SelectedProductLabel.IsVisible = false;
        QuantityEntry.Text = UnitCostEntry.Text = LotCodeEntry.Text = string.Empty;
        ExpirationSection.IsVisible = false;
    }

    private void RemoveLine_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: OperationLineView line })
        {
            _lines.Remove(line);
            SupplierPicker.IsEnabled = _lines.Count == 0;
            UpdateTotal();
        }
    }

    private async void ConfirmEntry_Clicked(object? sender, EventArgs e)
    {
        try
        {
            if (_lines.Count == 0) throw new InventoryRuleException("Agrega al menos un producto a la entrada.");
            ConfirmEntryButton.IsEnabled = false;
            var document = await _transactions.CreateEntryAsync(
                _businessId,
                _lines.Select(line => new InventoryDocumentLineInput(line.Product.Id, line.Quantity, line.UnitPrice,
                    line.LotCode, line.ManufacturingDate, line.ExpirationDate)),
                GetSelectedSupplier()?.Id,
                NotesEntry.Text);
            document = await _transactions.ConfirmAsync(_businessId, document.Id);
            ResultLabel.Style = (Style)Application.Current!.Resources["InfoText"];
            ResultLabel.Text = $"Entrada {document.Reference} confirmada por {document.Total:C}.";
            _lines.Clear();
            SupplierPicker.IsEnabled = true;
            SupplierPicker.SelectedItem = NoSupplierOption;
            NotesEntry.Text = string.Empty;
            UpdateTotal();
        }
        catch (Exception error)
        {
            ResultLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            ResultLabel.Text = error.Message;
        }
        finally { ConfirmEntryButton.IsEnabled = true; }
    }

    private void UpdateTotal() => TotalLabel.Text = $"Total: {_lines.Sum(line => line.Subtotal):C}";

    private async Task RefreshProductCatalogAsync(
        string? search = null,
        CancellationToken cancellationToken = default,
        bool resetSelection = true)
    {
        IReadOnlyList<SupplierProductCatalogItem> items;
        if (GetSelectedSupplier() is Supplier supplier)
        {
            items = await _catalog.GetSupplierProductCatalogAsync(
                _businessId,
                supplier.Id,
                search,
                cancellationToken);
        }
        else
        {
            var products = await _products.SearchAsync(
                _businessId,
                search,
                orderBy: "name",
                descending: false,
                cancellationToken: cancellationToken);
            items = products
                .Select(product => new SupplierProductCatalogItem(product, null))
                .ToArray();
        }

        if (resetSelection)
        {
            _ignoreSearchChange = true;
            ProductSearch.SetText(search ?? string.Empty);
            _ignoreSearchChange = false;
        }

        _searchResults.Clear();
        foreach (var item in items.Take(100))
        {
            _searchResults.Add(item);
        }
    }

    private Supplier? GetSelectedSupplier() =>
        SupplierPicker.SelectedItem is Supplier { Id: > 0 } supplier ? supplier : null;

    private static decimal ParseDecimal(string? value, string message)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var result) ||
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result)) return result;
        throw new InventoryRuleException(message);
    }
}
