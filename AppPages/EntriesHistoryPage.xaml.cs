using InventorySystem.Domain;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class EntriesHistoryPage : ContentPage
{
    private readonly InventoryTransactionService _transactions;
    private readonly BusinessService _businesses;
    private long _businessId;

    public EntriesHistoryPage(InventoryTransactionService transactions, BusinessService businesses)
    {
        InitializeComponent();
        _transactions = transactions;
        _businesses = businesses;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            HistoryList.ItemsSource = (await _transactions.GetRecentAsync(_businessId, InventoryDocumentType.Entry, 500))
                .Where(document => document.Status != InventoryDocumentStatus.Draft)
                .ToArray();
        }
        catch (Exception error)
        {
            ErrorLabel.Text = error.Message;
        }
    }

    private async void HistoryList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not InventoryDocument document) return;
        HistoryList.SelectedItem = null;
        await Shell.Current.GoToAsync(
            nameof(InventoryDocumentDetailPage),
            new Dictionary<string, object> { ["DocumentId"] = document.Id });
    }
}
