using System.Collections.ObjectModel;
using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Services;

namespace InventorySystem.AppPages;

public partial class NewEntryPage : ContentPage
{
    private readonly ProductRepository _products;
    private readonly SupplierRepository _suppliers;
    private readonly InventoryTransactionService _transactions;
    private readonly BusinessService _businesses;
    private readonly BarcodeScannerCoordinator _cameraScanner;
    private readonly ObservableCollection<OperationLineView> _lines = [];
    private readonly ObservableCollection<Product> _searchResults = [];
    private readonly ObservableCollection<Supplier> _supplierOptions = [];
    private CancellationTokenSource? _searchDebounce;
    private Product? _selectedProduct;
    private bool _ignoreSearchChange;
    private long _businessId;

    public NewEntryPage(ProductRepository products, SupplierRepository suppliers, InventoryTransactionService transactions,
        BusinessService businesses, BarcodeScannerCoordinator cameraScanner)
    {
        InitializeComponent();
        _products = products;
        _suppliers = suppliers;
        _transactions = transactions;
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
        }
        catch (Exception error) { ResultLabel.Text = error.Message; }
    }

    private async Task RefreshSuppliersAsync()
    {
        var selectedId = (SupplierPicker.SelectedItem as Supplier)?.Id;
        var suppliers = await _suppliers.SearchAsync(_businessId);

        _supplierOptions.Clear();
        foreach (var supplier in suppliers)
        {
            _supplierOptions.Add(supplier);
        }

        SupplierPicker.IsEnabled = _supplierOptions.Count > 0;
        SupplierPicker.Title = _supplierOptions.Count > 0 ? "Selecciona un proveedor" : "No hay proveedores registrados";
        SupplierPicker.SelectedItem = selectedId.HasValue
            ? _supplierOptions.FirstOrDefault(supplier => supplier.Id == selectedId.Value)
            : null;
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
            if (string.IsNullOrWhiteSpace(ProductSearch.Text))
            {
                _searchResults.Clear();
                ProductSearchResults.IsVisible = false;
                return;
            }
            var products = await _products.SearchAsync(_businessId, ProductSearch.Text, orderBy: "name", descending: false, cancellationToken: token);
            _searchResults.Clear();
            foreach (var product in products.Take(30)) _searchResults.Add(product);
            ProductSearchResults.IsVisible = _searchResults.Count > 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { ResultLabel.Text = error.Message; }
    }

    private async void ProductSearchResults_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Product product) return;
        ProductSearchResults.SelectedItem = null;
        SelectProduct(product);
        await Task.CompletedTask;
    }

    private void SelectProduct(Product product)
    {
        _selectedProduct = product;
        _ignoreSearchChange = true;
        ProductSearch.SetText(product.Name);
        _ignoreSearchChange = false;
        _searchResults.Clear();
        ProductSearchResults.IsVisible = false;
        SelectedProductLabel.Text = $"{product.Name} · {product.DisplayCode} · {product.DisplayStock}";
        SelectedProductLabel.IsVisible = true;
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
            SelectProduct(product);
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
            ClearProductSelection();
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
        ProductSearchResults.IsVisible = false;
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
                (SupplierPicker.SelectedItem as Supplier)?.Id,
                NotesEntry.Text);
            document = await _transactions.ConfirmAsync(_businessId, document.Id);
            ResultLabel.Style = (Style)Application.Current!.Resources["InfoText"];
            ResultLabel.Text = $"Entrada {document.Reference} confirmada por {document.Total:C}.";
            _lines.Clear();
            SupplierPicker.SelectedIndex = -1;
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

    private static decimal ParseDecimal(string? value, string message)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var result) ||
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result)) return result;
        throw new InventoryRuleException(message);
    }
}
