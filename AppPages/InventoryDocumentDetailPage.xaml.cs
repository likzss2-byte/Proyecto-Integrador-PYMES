using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class InventoryDocumentDetailPage : ContentPage, IQueryAttributable
{
    private readonly InventoryTransactionService _transactions;
    private readonly SupplierRepository _suppliers;
    private readonly BusinessService _businesses;
    private long _businessId;
    private long _documentId;
    private InventoryDocument? _document;

    public InventoryDocumentDetailPage(
        InventoryTransactionService transactions,
        SupplierRepository suppliers,
        BusinessService businesses)
    {
        InitializeComponent();
        _transactions = transactions;
        _suppliers = suppliers;
        _businesses = businesses;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("DocumentId", out var value))
        {
            _documentId = Convert.ToInt64(value, CultureInfo.InvariantCulture);
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
            ErrorLabel.Text = error.Message;
        }
    }

    private async Task RefreshAsync()
    {
        _document = await _transactions.GetAsync(_businessId, _documentId)
            ?? throw new InventoryRuleException("La operación no existe.");
        var document = _document;
        TitleLabel.Text = document.Type == InventoryDocumentType.Entry ? "Detalle de entrada" : "Detalle de venta";
        ReferenceLabel.Text = document.Reference;
        DateLabel.Text = document.DisplayDate;
        StatusLabel.Text = document.DisplayStatus;
        TotalLabel.Text = document.DisplayTotal;
        NotesLabel.Text = string.IsNullOrWhiteSpace(document.Notes) ? "Sin notas" : document.Notes;
        LineList.ItemsSource = document.Lines;

        SupplierField.IsVisible = document.Type == InventoryDocumentType.Entry;
        if (document.Type == InventoryDocumentType.Entry)
        {
            if (document.SupplierId.HasValue)
            {
                var supplier = await _suppliers.GetAsync(_businessId, document.SupplierId.Value);
                SupplierLabel.Text = supplier?.CompanyName ?? $"Proveedor ID {document.SupplierId.Value}";
            }
            else
            {
                SupplierLabel.Text = "Sin proveedor";
            }
        }

        CancellationPanel.IsVisible = document.Status == InventoryDocumentStatus.Confirmed;
        CancelButton.Text = document.Type == InventoryDocumentType.Entry ? "Cancelar entrada" : "Cancelar venta";
        CancelReasonEntry.Text = string.Empty;
        ErrorLabel.Text = string.Empty;
    }

    private async void Cancel_Clicked(object? sender, EventArgs e)
    {
        if (_document is not { Status: InventoryDocumentStatus.Confirmed })
        {
            return;
        }

        try
        {
            ErrorLabel.Text = string.Empty;
            CancelButton.IsEnabled = false;
            await _transactions.CancelAsync(
                _businessId,
                _document.Id,
                CancelReasonEntry.Text ?? string.Empty);
            await RefreshAsync();
            ErrorLabel.Style = (Style)Application.Current!.Resources["InfoText"];
            ErrorLabel.Text = "Operación cancelada y existencias revertidas en los lotes correspondientes.";
        }
        catch (Exception error)
        {
            ErrorLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            ErrorLabel.Text = error.Message;
        }
        finally
        {
            CancelButton.IsEnabled = true;
        }
    }

    private async void LineList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not InventoryDocumentLine line)
        {
            return;
        }
        LineList.SelectedItem = null;
        if (!line.LotId.HasValue)
        {
            ErrorLabel.Text = "Esta operación fue creada antes de guardar el ID del lote en cada renglón.";
            return;
        }
        await Shell.Current.GoToAsync(
            nameof(LotDetailPage),
            new Dictionary<string, object> { ["LotId"] = line.LotId.Value });
    }
}
