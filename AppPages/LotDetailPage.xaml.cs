using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class LotDetailPage : ContentPage, IQueryAttributable
{
    private readonly InventoryLotService _lots;
    private readonly ProductRepository _products;
    private readonly SupplierRepository _suppliers;
    private readonly BusinessService _businesses;
    private long _businessId;
    private long _lotId;
    private long _productId;
    private ExpirationMode _expirationMode;
    private decimal? _unitCost;

    public LotDetailPage(
        InventoryLotService lots,
        ProductRepository products,
        SupplierRepository suppliers,
        BusinessService businesses)
    {
        InitializeComponent();
        _lots = lots;
        _products = products;
        _suppliers = suppliers;
        _businesses = businesses;
        ManufacturingDatePicker.Date = DateTime.Today;
        ExpirationDatePicker.Date = DateTime.Today.AddDays(30);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("LotId", out var value))
        {
            _lotId = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            var suppliers = (await _suppliers.SearchAsync(_businessId, includeInactive: true)).ToList();
            suppliers.Insert(0, new Supplier { Id = 0, CompanyName = "Sin proveedor" });
            SupplierPicker.ItemsSource = suppliers;
            SupplierPicker.ItemDisplayBinding = new Binding(nameof(Supplier.CompanyName));
            await RefreshAsync();
        }
        catch (Exception error)
        {
            ErrorLabel.Text = error.Message;
        }
    }

    private async Task RefreshAsync()
    {
        var lot = await _lots.GetAsync(_businessId, _lotId)
            ?? throw new InventoryRuleException("El lote no existe.");
        var product = await _products.GetAsync(_businessId, lot.ProductId)
            ?? throw new InventoryRuleException("El producto del lote no existe.");
        _productId = product.Id;
        _expirationMode = product.ExpirationMode;

        ProductNameLabel.Text = product.Name;
        LotSummaryLabel.Text = $"Lote {lot.LotCode ?? "Sin código"} · {lot.EffectiveStatus}";
        CurrentQuantityLabel.Text = $"Disponible actualmente: {lot.Quantity:0.###}";
        LotCodeEntry.Text = lot.LotCode;
        _unitCost = lot.UnitCost;
        UnitCostDisplay.IsVisible = lot.UnitCost.HasValue;
        UnitCostLabel.Text = lot.UnitCost.HasValue ? lot.UnitCost.Value.ToString("C", CultureInfo.CurrentCulture) : string.Empty;
        HasManufacturingDateCheck.IsChecked = lot.ManufacturingDate.HasValue;
        ManufacturingDatePicker.IsVisible = lot.ManufacturingDate.HasValue;
        if (lot.ManufacturingDate.HasValue)
        {
            ManufacturingDatePicker.Date = lot.ManufacturingDate.Value.ToDateTime(TimeOnly.MinValue);
        }

        ExpirationField.IsVisible = product.ExpirationMode == ExpirationMode.Tracked;
        if (lot.ExpirationDate.HasValue)
        {
            ExpirationDatePicker.Date = lot.ExpirationDate.Value.ToDateTime(TimeOnly.MinValue);
        }

        if (SupplierPicker.ItemsSource is IEnumerable<Supplier> suppliers)
        {
            SupplierPicker.SelectedItem = suppliers.FirstOrDefault(item => item.Id == (lot.SupplierId ?? 0));
        }
    }

    private void HasManufacturingDateCheck_Changed(object? sender, CheckedChangedEventArgs e) =>
        ManufacturingDatePicker.IsVisible = e.Value;

    private async void SaveLot_Clicked(object? sender, EventArgs e)
    {
        try
        {
            ErrorLabel.Text = string.Empty;
            SaveLotButton.IsEnabled = false;
            await _lots.UpdateAsync(
                _businessId,
                _lotId,
                new InventoryLotUpdateInput(
                    LotCodeEntry.Text,
                    (SupplierPicker.SelectedItem as Supplier) is { Id: > 0 } supplier ? supplier.Id : null,
                    HasManufacturingDateCheck.IsChecked
                        ? DateOnly.FromDateTime(ManufacturingDatePicker.Date ?? DateTime.Today)
                        : null,
                    _expirationMode == ExpirationMode.Tracked
                        ? DateOnly.FromDateTime(ExpirationDatePicker.Date ?? DateTime.Today)
                        : null,
                    _unitCost));
            ErrorLabel.Style = (Style)Application.Current!.Resources["InfoText"];
            ErrorLabel.Text = "Lote actualizado.";
            await RefreshAsync();
        }
        catch (Exception error)
        {
            ErrorLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            ErrorLabel.Text = error.Message;
        }
        finally
        {
            SaveLotButton.IsEnabled = true;
        }
    }

    private async void ApplyAdjustment_Clicked(object? sender, EventArgs e)
    {
        try
        {
            ErrorLabel.Text = string.Empty;
            await _lots.AdjustQuantityAsync(
                _businessId,
                new InventoryLotAdjustmentInput(
                    _lotId,
                    ParseDecimal(AdjustmentEntry.Text, "El ajuste no es válido."),
                    AdjustmentReasonEntry.Text ?? string.Empty));
            AdjustmentEntry.Text = string.Empty;
            AdjustmentReasonEntry.Text = string.Empty;
            ErrorLabel.Style = (Style)Application.Current!.Resources["InfoText"];
            ErrorLabel.Text = "Cantidad ajustada.";
            await RefreshAsync();
        }
        catch (Exception error)
        {
            ErrorLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            ErrorLabel.Text = error.Message;
        }
    }

    private async void OpenProduct_Clicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(
            nameof(ItemFullPage),
            new Dictionary<string, object> { ["ProductId"] = _productId });

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
