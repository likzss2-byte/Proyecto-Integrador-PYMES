using InventorySystem.Domain;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class LotsPage : ContentPage
{
    private readonly InventoryLotService _lots;
    private readonly BusinessService _businesses;
    private long _businessId;
    private CancellationTokenSource? _searchDebounce;

    public LotsPage(InventoryLotService lots, BusinessService businesses)
    {
        InitializeComponent();
        _lots = lots;
        _businesses = businesses;
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
            ShowError(error.Message);
        }
    }

    private async void LotSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounce?.Cancel();
        _searchDebounce?.Dispose();
        _searchDebounce = new CancellationTokenSource();
        try
        {
            await Task.Delay(250, _searchDebounce.Token);
            await RefreshAsync(_searchDebounce.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            ShowError(error.Message);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_businessId == 0)
        {
            return;
        }

        LotList.ItemsSource = await _lots.GetAllAsync(_businessId, LotSearch.Text, cancellationToken: cancellationToken);
        ShowError(null);
    }

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message ?? string.Empty;
        ErrorLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
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
}
