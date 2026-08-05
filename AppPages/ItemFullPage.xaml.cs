using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class ItemFullPage : ContentPage, IQueryAttributable
{
    private readonly ProductRepository _products;
    private readonly SupplierRepository _suppliers;
    private readonly BusinessService _businesses;
    private readonly InventoryAdjustmentService _adjustments;
    private readonly InventoryLotService _lots;
    private long _businessId;
    private long _productId;
    private long? _pendingCountId;

    public ItemFullPage(
        ProductRepository products,
        SupplierRepository suppliers,
        BusinessService businesses,
        InventoryAdjustmentService adjustments,
        InventoryLotService lots)
    {
        InitializeComponent();
        _products = products;
        _suppliers = suppliers;
        _businesses = businesses;
        _adjustments = adjustments;
        _lots = lots;
        ExpirationModePicker.SelectedIndex = 0;
        LotExpirationDate.Date = DateTime.Today.AddDays(30);
        LotManufacturingDate.Date = DateTime.Today;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("ProductId", out var value))
        {
            _productId = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            LotSupplierPicker.ItemsSource = (await _suppliers.SearchAsync(_businessId)).ToList();
            LotSupplierPicker.ItemDisplayBinding = new Binding(nameof(Supplier.CompanyName));
            await RefreshAsync();
        }
        catch (Exception error)
        {
            DetailErrorLabel.Text = error.Message;
        }
    }

    private async void ApplyAdjustment_Clicked(object? sender, EventArgs e)
    {
        try
        {
            DetailErrorLabel.Text = string.Empty;
            var quantity = ParseDecimal(AdjustmentQuantity.Text, "Captura una cantidad válida.");
            await _adjustments.ApplyAdjustmentAsync(
                _businessId,
                new InventoryAdjustmentInput(_productId, quantity, AdjustmentReason.Text ?? string.Empty));
            AdjustmentQuantity.Text = string.Empty;
            AdjustmentReason.Text = string.Empty;
            await RefreshAsync();
        }
        catch (Exception error)
        {
            DetailErrorLabel.Text = error.Message;
        }
    }

    private async void SaveLot_Clicked(object? sender, EventArgs e)
    {
        try
        {
            LotErrorLabel.Text = string.Empty;
            var mode = ExpirationModePicker.SelectedIndex == 1
                ? ExpirationMode.NotApplicable
                : ExpirationMode.Tracked;
            DateOnly? expiration = mode == ExpirationMode.Tracked
                ? DateOnly.FromDateTime(LotExpirationDate.Date ?? DateTime.Today)
                : null;
            if (string.IsNullOrWhiteSpace(LotQuantityEntry.Text))
            {
                await _lots.ClassifyUndatedStockAsync(
                    _businessId,
                    _productId,
                    mode,
                    expiration,
                    LotCodeEntry.Text);
            }
            else
            {
                await _lots.ReceiveAsync(
                    _businessId,
                    new InventoryLotReceiptInput(
                        _productId,
                        ParseDecimal(LotQuantityEntry.Text, "Captura una cantidad válida."),
                        mode,
                        expiration,
                        LotCodeEntry.Text,
                        (LotSupplierPicker.SelectedItem as Supplier)?.Id,
                        HasManufacturingDateCheck.IsChecked
                            ? DateOnly.FromDateTime(LotManufacturingDate.Date ?? DateTime.Today)
                            : null,
                        ParseOptionalDecimal(LotUnitCostEntry.Text, "El costo unitario no es válido.")));
            }

            LotQuantityEntry.Text = string.Empty;
            LotCodeEntry.Text = string.Empty;
            LotUnitCostEntry.Text = string.Empty;
            HasManufacturingDateCheck.IsChecked = false;
            await RefreshAsync();
        }
        catch (Exception error)
        {
            LotErrorLabel.Text = error.Message;
        }
    }

    private void HasManufacturingDateCheck_Changed(object? sender, CheckedChangedEventArgs e) =>
        LotManufacturingDate.IsVisible = e.Value;

    private async void CreateCount_Clicked(object? sender, EventArgs e)
    {
        try
        {
            DetailErrorLabel.Text = string.Empty;
            var physical = ParseDecimal(PhysicalStock.Text, "Captura un inventario físico válido.");
            var count = await _adjustments.CreateCountAsync(
                _businessId,
                [new InventoryCountLineInput(_productId, physical)],
                CountNotes.Text);
            _pendingCountId = count.Id;
            var line = count.Lines.Single();
            CountDifferenceLabel.Text = $"Teórico: {line.TheoreticalStock:0.###} · Faltante: {line.Missing:0.###} · Sobrante: {line.Surplus:0.###}";
            ConfirmCountButton.IsVisible = true;
        }
        catch (Exception error)
        {
            DetailErrorLabel.Text = error.Message;
        }
    }

    private async void ConfirmCount_Clicked(object? sender, EventArgs e)
    {
        if (!_pendingCountId.HasValue)
        {
            return;
        }

        var confirm = await DisplayAlertAsync(
            "Confirmar conteo",
            "El stock se ajustará al inventario físico y se registrará un movimiento.",
            "Confirmar",
            "Cancelar");
        if (!confirm)
        {
            return;
        }

        try
        {
            await _adjustments.ConfirmCountAsync(_businessId, _pendingCountId.Value);
            _pendingCountId = null;
            ConfirmCountButton.IsVisible = false;
            await RefreshAsync();
        }
        catch (Exception error)
        {
            DetailErrorLabel.Text = error.Message;
        }
    }

    private async Task RefreshAsync()
    {
        if (_businessId == 0 || _productId == 0)
        {
            return;
        }

        var product = await _products.GetAsync(_businessId, _productId)
            ?? throw new InventoryRuleException("El producto no existe.");
        ProductNameLabel.Text = product.Name;
        ProductCodeLabel.Text = $"SKU: {product.Sku} · Código: {product.Barcode ?? "Sin código"}";
        ProductStockLabel.Text = $"Existencia: {product.DisplayStock} · Mínimo: {product.MinimumStock:0.###}";
        ExpirationStatusLabel.Text = product.ExpirationMode switch
        {
            ExpirationMode.Tracked when product.UndatedStock > 0 => $"Caducidad: faltan fechas para {product.UndatedStock:0.###}",
            ExpirationMode.Tracked when product.NearestExpirationDate.HasValue => $"Próxima caducidad: {product.NearestExpirationDate:dd/MM/yyyy}",
            ExpirationMode.NotApplicable => "Caducidad: no aplica",
            _ => "Caducidad: por configurar"
        };
        ExpirationModePicker.SelectedIndex = product.ExpirationMode == ExpirationMode.NotApplicable ? 1 : 0;
        LotList.ItemsSource = await _lots.GetLotsAsync(_businessId, _productId);
        MovementList.ItemsSource = await _adjustments.GetMovementsAsync(_businessId, _productId);
    }

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
