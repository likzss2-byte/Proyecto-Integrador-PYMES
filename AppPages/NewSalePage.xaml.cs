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
    private readonly InventoryLotService _lots;
    private readonly InventoryTransactionService _transactions;
    private readonly BusinessService _businesses;
    private readonly BarcodeScannerCoordinator _cameraScanner;
    private readonly ObservableCollection<OperationLineView> _lines = [];
    private readonly ObservableCollection<Product> _searchResults = [];
    private CancellationTokenSource? _searchDebounce;
    private Product? _selectedProduct;
    private bool _ignoreSearchChange;
    private long _businessId;

    public NewSalePage(ProductRepository products, InventoryLotService lots, InventoryTransactionService transactions,
        BusinessService businesses, BarcodeScannerCoordinator cameraScanner)
    {
        InitializeComponent();
        _products = products;
        _lots = lots;
        _transactions = transactions;
        _businesses = businesses;
        _cameraScanner = cameraScanner;
        LineList.ItemsSource = _lines;
        ProductSearchResults.ItemsSource = _searchResults;
        ProductSearch.SearchTextChanged += ProductSearch_TextChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            UpdateConfirmationState();
        }
        catch (Exception error) { ResultLabel.Text = error.Message; }
    }

    private async void ProductSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_ignoreSearchChange) return;
        _selectedProduct = null;
        SelectedProductLabel.Text = string.Empty;
        SelectedProductLabel.IsVisible = false;
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

    private void ProductSearchResults_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Product product) return;
        ProductSearchResults.SelectedItem = null;
        SelectProduct(product);
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
        if (string.IsNullOrWhiteSpace(UnitPriceEntry.Text))
            UnitPriceEntry.Text = product.SalePrice.ToString(CultureInfo.CurrentCulture);
    }

    private async void ScanCamera_Clicked(object? sender, EventArgs e)
    {
        var result = await _cameraScanner.ScanAsync("venta", "Escanear producto para venta");
        if (!result.Success || string.IsNullOrWhiteSpace(result.Code)) return;
        var product = await _products.FindByCodeAsync(_businessId, result.Code);
        if (product is { Active: true })
        {
            SelectProduct(product);
            return;
        }
        ResultLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
        ResultLabel.Text = "El código escaneado no corresponde a un producto registrado.";
    }

    private async void AddLine_Clicked(object? sender, EventArgs e)
    {
        try
        {
            ResultLabel.Text = string.Empty;
            var product = _selectedProduct ?? throw new InventoryRuleException("Selecciona un producto del inventario.");
            var quantity = string.IsNullOrWhiteSpace(QuantityEntry.Text) ? 1m : ParseDecimal(QuantityEntry.Text, "La cantidad no es válida.");
            InventoryRules.ValidateQuantity(quantity, product.UnitOfMeasure);
            var price = string.IsNullOrWhiteSpace(UnitPriceEntry.Text) ? product.SalePrice : ParseDecimal(UnitPriceEntry.Text, "El precio unitario no es válido.");
            if (price < 0) throw new InventoryRuleException("El precio unitario no puede ser negativo.");

            var availableLots = (await _lots.GetLotsAsync(_businessId, product.Id))
                .Where(lot => lot.Quantity > 0)
                .Where(lot => lot.ProductExpirationMode != ExpirationMode.Tracked || lot.ExpirationDate.HasValue)
                .ToArray();
            var line = new OperationLineView { Product = product, Quantity = quantity, UnitPrice = price, AvailableLots = availableLots };
            line.PropertyChanged += Line_PropertyChanged;
            _lines.Add(line);
            ClearProductSelection();

            ResultLabel.Style = (Style)Application.Current!.Resources[availableLots.Length == 0 ? "ErrorText" : "InfoText"];
            ResultLabel.Text = availableLots.Length == 0
                ? $"{product.Name} no tiene lotes disponibles para vender."
                : "Producto agregado. Selecciona el lote antes de confirmar.";
            UpdateTotal();
            UpdateConfirmationState();
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
        QuantityEntry.Text = UnitPriceEntry.Text = string.Empty;
    }

    private void Line_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is OperationLineView line && e.PropertyName == nameof(OperationLineView.SelectedLot))
        {
            ClampQuantityToSelectedLot(line);
        }

        if (e.PropertyName is nameof(OperationLineView.Quantity) or nameof(OperationLineView.SelectedLot))
        {
            UpdateTotal();
            UpdateConfirmationState();
        }
    }

    private void LineLot_Changed(object? sender, EventArgs e) => UpdateConfirmationState();

    private void LineQuantity_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not Entry { BindingContext: OperationLineView line }) return;
        if (string.IsNullOrWhiteSpace(e.NewTextValue)) { line.Quantity = 0m; return; }
        if (decimal.TryParse(e.NewTextValue, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity) ||
            decimal.TryParse(e.NewTextValue, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity))
        {
            line.Quantity = InventoryRules.NormalizeQuantity(quantity);
            ClampQuantityToSelectedLot(line);
        }
    }

    private void IncreaseQuantity_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: OperationLineView line }) return;
        var step = line.Product.UnitOfMeasure == UnitOfMeasure.Unit ? 1m : 0.1m;
        line.Quantity = InventoryRules.NormalizeQuantity(line.Quantity + step);
        ClampQuantityToSelectedLot(line);
    }

    private void DecreaseQuantity_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: OperationLineView line }) return;
        var step = line.Product.UnitOfMeasure == UnitOfMeasure.Unit ? 1m : 0.1m;
        var next = InventoryRules.NormalizeQuantity(line.Quantity - step);
        if (next > 0) line.Quantity = next;
    }

    private void RemoveLine_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: OperationLineView line })
        {
            line.PropertyChanged -= Line_PropertyChanged;
            _lines.Remove(line);
            UpdateTotal();
            UpdateConfirmationState();
        }
    }

    private void AllowExpiredLotsCheck_Changed(object? sender, CheckedChangedEventArgs e) => UpdateConfirmationState();

    private async void ConfirmSale_Clicked(object? sender, EventArgs e)
    {
        try
        {
            ValidateLines();
            ConfirmSaleButton.IsEnabled = false;
            var usesExpired = _lines.Any(line => line.UsesExpiredLot);
            if (usesExpired && !AllowExpiredLotsCheck.IsChecked)
                throw new InventoryRuleException("Debes confirmar que entiendes que la venta incluye producto caducado.");

            var document = await _transactions.CreateSaleAsync(_businessId,
                _lines.Select(line => new InventoryDocumentLineInput(line.Product.Id, line.Quantity, line.UnitPrice, LotId: line.SelectedLot!.Id)),
                NotesEntry.Text);
            document = await _transactions.ConfirmAsync(_businessId, document.Id, usesExpired && AllowExpiredLotsCheck.IsChecked);
            ResultLabel.Style = (Style)Application.Current!.Resources["InfoText"];
            ResultLabel.Text = $"Venta {document.Reference} confirmada por {document.Total:C}.";
            foreach (var line in _lines) line.PropertyChanged -= Line_PropertyChanged;
            _lines.Clear();
            AllowExpiredLotsCheck.IsChecked = false;
            NotesEntry.Text = string.Empty;
            UpdateTotal();
        }
        catch (Exception error)
        {
            ResultLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            ResultLabel.Text = error.Message;
        }
        finally { UpdateConfirmationState(); }
    }

    private void ValidateLines()
    {
        if (_lines.Count == 0) throw new InventoryRuleException("Agrega al menos un producto a la venta.");
        foreach (var line in _lines)
        {
            InventoryRules.ValidateQuantity(line.Quantity, line.Product.UnitOfMeasure);
            if (line.SelectedLot is null) throw new InventoryRuleException($"Selecciona un lote para {line.Product.Name}.");
        }
        foreach (var group in _lines.GroupBy(line => line.SelectedLot!.Id))
        {
            var available = group.First().SelectedLot!.Quantity;
            var requested = group.Sum(line => line.Quantity);
            if (requested > available)
                throw new InventoryRuleException($"El lote {group.First().SelectedLot!.LotCode ?? "Sin código"} solo tiene {available:0.###} disponibles y se solicitaron {requested:0.###}.");
        }
    }

    private void UpdateConfirmationState()
    {
        var hasExpired = _lines.Any(line => line.UsesExpiredLot);
        ExpiredAuthorizationPanel.IsVisible = hasExpired;
        if (!hasExpired && AllowExpiredLotsCheck.IsChecked) AllowExpiredLotsCheck.IsChecked = false;
        var allLotsSelected = _lines.Count > 0 && _lines.All(line => line.SelectedLot is not null);
        var quantitiesValid = _lines.Count > 0 && _lines.All(line => line.Quantity > 0 &&
            (line.Product.UnitOfMeasure != UnitOfMeasure.Unit || line.Quantity == decimal.Truncate(line.Quantity)));
        var quantitiesWithinLots = _lines
            .Where(line => line.SelectedLot is not null)
            .GroupBy(line => line.SelectedLot!.Id)
            .All(group => group.Sum(line => line.Quantity) <= group.First().SelectedLot!.Quantity);
        ConfirmSaleButton.IsEnabled = allLotsSelected && quantitiesValid && quantitiesWithinLots &&
            (!hasExpired || AllowExpiredLotsCheck.IsChecked);
    }

    private void ClampQuantityToSelectedLot(OperationLineView line)
    {
        if (line.SelectedLot is null || line.Quantity <= 0)
        {
            return;
        }

        var alreadyRequested = _lines
            .Where(other => !ReferenceEquals(other, line) && other.SelectedLot?.Id == line.SelectedLot.Id)
            .Sum(other => other.Quantity);
        var maximum = InventoryRules.NormalizeQuantity(Math.Max(0m, line.SelectedLot.Quantity - alreadyRequested));
        if (line.Quantity <= maximum)
        {
            return;
        }

        line.Quantity = maximum;
        ResultLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
        ResultLabel.Text = maximum > 0
            ? $"El lote seleccionado solo permite {maximum:0.###} adicionales en este renglón."
            : "La disponibilidad de ese lote ya está asignada a otros renglones de la venta.";
    }

    private void UpdateTotal() => TotalLabel.Text = $"Total: {_lines.Sum(line => line.Subtotal):C}";

    private static decimal ParseDecimal(string? value, string message)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var result) ||
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result)) return result;
        throw new InventoryRuleException(message);
    }
}
