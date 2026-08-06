using System.Collections.ObjectModel;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Services;
using InventorySystem.VisualElementsTemplates;

namespace InventorySystem.AppPages;

public partial class InventoryPage : ContentPage
{
    private readonly ProductRepository _products;
    private readonly BusinessService _businesses;
    private readonly BarcodeScannerCoordinator _cameraScanner;
    private readonly ObservableCollection<Product> _items = [];
    private InventorySortOption _selectedSortOption = InventorySortOption.Recent;
    private bool _sortDescending = true;
    private long _businessId;

    public InventoryPage(
        ProductRepository products,
        BusinessService businesses,
        BarcodeScannerCoordinator cameraScanner)
    {
        InitializeComponent();
        _products = products;
        _businesses = businesses;
        _cameraScanner = cameraScanner;
        InventoryItems.ItemsSource = _items;
        InventorySearch.SearchTextChanged += InventorySearch_TextChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            await RefreshInventoryAsync();
            if (_items.Count > 0)
            {
                InventoryItems.ScrollTo(0, position: ScrollToPosition.Start, animate: false);
            }
        }
        catch (Exception error)
        {
            await DisplayAlertAsync("Inventario", error.Message, "Aceptar");
        }
    }

    private async void InventorySortButton_SortChanged(object? sender, SortChangedEventArgs e)
    {
        _selectedSortOption = e.SortOption;
        _sortDescending = e.IsDescending;
        await RefreshInventoryAsync();
    }

    private async void InventorySearch_TextChanged(object? sender, TextChangedEventArgs e) =>
        await RefreshInventoryAsync();

    private async void ScanInventoryCamera_Clicked(object? sender, EventArgs e)
    {
        var result = await _cameraScanner.ScanAsync("busqueda-inventario", "Escanear producto en inventario");
        if (!result.Success || string.IsNullOrWhiteSpace(result.Code))
        {
            return;
        }

        InventorySearch.SetText(result.Code);
        await RefreshInventoryAsync();
        if (_items.Count == 1)
        {
            await Shell.Current.GoToAsync(
                nameof(ItemFullPage),
                new Dictionary<string, object> { ["ProductId"] = _items[0].Id });
        }
    }

    private async Task RefreshInventoryAsync()
    {
        if (_businessId == 0)
        {
            return;
        }

        var order = _selectedSortOption switch
        {
            InventorySortOption.Alphabetical => "name",
            InventorySortOption.Price => "price",
            _ => "recent"
        };
        var products = await _products.SearchAsync(
            _businessId,
            InventorySearch.Text,
            orderBy: order,
            descending: _sortDescending);
        _items.Clear();
        foreach (var product in products)
        {
            _items.Add(product);
        }
    }

    private async void InventoryItems_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Product product)
        {
            return;
        }

        InventoryItems.SelectedItem = null;
        await Shell.Current.GoToAsync(
            nameof(ItemFullPage),
            new Dictionary<string, object> { ["ProductId"] = product.Id });
    }

    private async void OpenSupplierInventory_Clicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(SupplierInventoryPage));

    private async void OpenBrandInventory_Clicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(BrandInventoryPage));

    private async void OpenOperationalInventory_Clicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(OperationalInventoryPage));
}
