using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class ItemFullPage : ContentPage, IQueryAttributable
{
    private readonly ProductRepository _products;
    private readonly BusinessService _businesses;
    private readonly InventoryAdjustmentService _adjustments;
    private readonly InventoryLotService _lots;
    private long _businessId;
    private long _productId;
    private long? _pendingCountId;

    public ItemFullPage(
        ProductRepository products,
        BusinessService businesses,
        InventoryAdjustmentService adjustments,
        InventoryLotService lots)
    {
        InitializeComponent();
        _products = products;
        _businesses = businesses;
        _adjustments = adjustments;
        _lots = lots;
        ExpirationModePicker.SelectedIndex = 0;
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
            await RefreshAsync();
        }
        catch (Exception error)
        {
            await DisplayAlertAsync("Producto", error.Message, "Aceptar");
        }
    }

    private async void ApplyAdjustment_Clicked(object? sender, EventArgs e)
    {
        try
        {
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
            await DisplayAlertAsync("Ajuste", error.Message, "Aceptar");
        }
    }

    private async void SaveLot_Clicked(object? sender, EventArgs e)
    {
        try
        {
            var mode = ExpirationModePicker.SelectedIndex == 1
                ? ExpirationMode.NotApplicable
                : ExpirationMode.Tracked;
            DateOnly? date = mode == ExpirationMode.Tracked
                ? DateOnly.FromDateTime(LotExpirationDate.Date ?? DateTime.Today)
                : null;
            if (string.IsNullOrWhiteSpace(LotQuantityEntry.Text))
            {
                await _lots.ClassifyUndatedStockAsync(
                    _businessId,
                    _productId,
                    mode,
                    date,
                    LotCodeEntry.Text);
            }
            else
            {
                await _lots.ReceiveAsync(
                    _businessId,
                    _productId,
                    ParseDecimal(LotQuantityEntry.Text, "Captura una cantidad válida."),
                    mode,
                    date,
                    LotCodeEntry.Text);
            }

            LotQuantityEntry.Text = string.Empty;
            LotCodeEntry.Text = string.Empty;
            await RefreshAsync();
        }
        catch (Exception error)
        {
            await DisplayAlertAsync("Caducidad", error.Message, "Aceptar");
        }
    }

    private async void CreateCount_Clicked(object? sender, EventArgs e)
    {
        try
        {
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
            await DisplayAlertAsync("Conteo", error.Message, "Aceptar");
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
            await DisplayAlertAsync("Conteo", error.Message, "Aceptar");
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
}
