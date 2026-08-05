using System.Collections.ObjectModel;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem;

public partial class MainPage : ContentPage
{
    private readonly DashboardService _dashboard;
    private readonly BusinessService _businesses;
    private readonly ObservableCollection<MinimumStockAlert> _minimumStock = [];
    private readonly ObservableCollection<ExpirationAlert> _expiring = [];
    private readonly ObservableCollection<ExpirationAlert> _expired = [];
    private long _businessId;

    public MainPage(DashboardService dashboard, BusinessService businesses)
    {
        InitializeComponent();
        _dashboard = dashboard;
        _businesses = businesses;
        MinimumStockList.ItemsSource = _minimumStock;
        ExpiringList.ItemsSource = _expiring;
        ExpiredList.ItemsSource = _expired;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async void RefreshDashboard_Clicked(object? sender, EventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            DashboardErrorLabel.Text = string.Empty;
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            var dashboard = await _dashboard.GetAsync(_businessId);
            Replace(_minimumStock, dashboard.MinimumStock);
            Replace(_expiring, dashboard.ExpiringLots);
            Replace(_expired, dashboard.ExpiredLots);
            MinimumStockCount.Text = dashboard.Summary.MinimumStockProducts.ToString();
            ExpiringCount.Text = dashboard.Summary.ExpiringLots.ToString();
            ExpiredCount.Text = dashboard.Summary.ExpiredLots.ToString();
            PendingOrdersCount.Text = dashboard.Summary.PendingOrders.ToString();
            PartialOrdersCount.Text = dashboard.Summary.PartiallyReceivedOrders.ToString();
        }
        catch (Exception error)
        {
            DashboardErrorLabel.Text = error.Message;
        }
    }

    private async void ProductAlert_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MinimumStockAlert alert)
        {
            return;
        }

        MinimumStockList.SelectedItem = null;
        await OpenProductAsync(alert.ProductId);
    }

    private async void ExpirationAlert_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ExpirationAlert alert)
        {
            return;
        }

        if (sender is CollectionView list)
        {
            list.SelectedItem = null;
        }

        await OpenProductAsync(alert.ProductId);
    }

    private static Task OpenProductAsync(long productId) => Shell.Current.GoToAsync(
        nameof(AppPages.ItemFullPage),
        new Dictionary<string, object> { ["ProductId"] = productId });

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }
}
