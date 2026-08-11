using System.Collections.ObjectModel;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Services;

namespace InventorySystem.AppPages;

public partial class InventoryPage : ContentPage
{
    private enum SortOption { Recent, Alphabetical, Price }

    private readonly ProductRepository _products;
    private readonly BusinessService _businesses;
    private readonly BarcodeScannerCoordinator _cameraScanner;
    private readonly ObservableCollection<Product> _items = [];
    private SortOption _selectedSort = SortOption.Recent;
    private bool _sortDescending = true;
    private long _businessId;
    private CancellationTokenSource? _searchDebounce;

    public InventoryPage(ProductRepository products, BusinessService businesses, BarcodeScannerCoordinator cameraScanner)
    {
        InitializeComponent();
        _products = products;
        _businesses = businesses;
        _cameraScanner = cameraScanner;
        InventoryItems.ItemsSource = _items;
        InventorySearch.SearchTextChanged += InventorySearch_TextChanged;
        UpdateSortButtons();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            await RefreshInventoryAsync();
        }
        catch (Exception error)
        {
            await DisplayAlertAsync("Inventario", error.Message, "Aceptar");
        }
    }

    private async void RecentSort_Clicked(object? sender, EventArgs e) => await SelectSortAsync(SortOption.Recent);
    private async void AlphabeticalSort_Clicked(object? sender, EventArgs e) => await SelectSortAsync(SortOption.Alphabetical);
    private async void PriceSort_Clicked(object? sender, EventArgs e) => await SelectSortAsync(SortOption.Price);

    private async Task SelectSortAsync(SortOption option)
    {
        if (_selectedSort == option)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _selectedSort = option;
            _sortDescending = option != SortOption.Alphabetical;
        }
        UpdateSortButtons();
        await RefreshInventoryAsync();
    }

    private void UpdateSortButtons()
    {
        var primary = (Style)Application.Current!.Resources["PrimaryButton"];
        var secondary = (Style)Application.Current.Resources["SecondaryButton"];
        RecentSortButton.Style = _selectedSort == SortOption.Recent ? primary : secondary;
        AlphabeticalSortButton.Style = _selectedSort == SortOption.Alphabetical ? primary : secondary;
        PriceSortButton.Style = _selectedSort == SortOption.Price ? primary : secondary;
        RecentSortButton.Text = "Recientes" + (_selectedSort == SortOption.Recent ? (_sortDescending ? " ▼" : " ▲") : string.Empty);
        AlphabeticalSortButton.Text = "Alfabético" + (_selectedSort == SortOption.Alphabetical ? (_sortDescending ? " ▼" : " ▲") : string.Empty);
        PriceSortButton.Text = "Precio" + (_selectedSort == SortOption.Price ? (_sortDescending ? " ▼" : " ▲") : string.Empty);
    }

    private async void InventorySearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounce?.Cancel();
        _searchDebounce?.Dispose();
        _searchDebounce = new CancellationTokenSource();
        try
        {
            await Task.Delay(300, _searchDebounce.Token);
            await RefreshInventoryAsync(_searchDebounce.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            await DisplayAlertAsync("Búsqueda", error.Message, "Aceptar");
        }
    }

    private async void ScanInventoryCamera_Clicked(object? sender, EventArgs e)
    {
        var result = await _cameraScanner.ScanAsync("busqueda-inventario", "Escanear producto en inventario");
        if (!result.Success || string.IsNullOrWhiteSpace(result.Code)) return;
        InventorySearch.SetText(result.Code);
        await RefreshInventoryAsync();
        if (_items.Count == 1)
        {
            await Shell.Current.GoToAsync(nameof(ItemFullPage), new Dictionary<string, object> { ["ProductId"] = _items[0].Id });
        }
    }

    private async Task RefreshInventoryAsync(CancellationToken cancellationToken = default)
    {
        if (_businessId == 0) return;
        var order = _selectedSort switch
        {
            SortOption.Alphabetical => "name",
            SortOption.Price => "price",
            _ => "recent"
        };
        var products = await _products.SearchAsync(_businessId, InventorySearch.Text, orderBy: order,
            descending: _sortDescending, cancellationToken: cancellationToken);
        _items.Clear();
        foreach (var product in products) _items.Add(product);
    }

    private async void InventoryItems_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Product product) return;
        InventoryItems.SelectedItem = null;
        await Shell.Current.GoToAsync(nameof(ItemFullPage), new Dictionary<string, object> { ["ProductId"] = product.Id });
    }
}
