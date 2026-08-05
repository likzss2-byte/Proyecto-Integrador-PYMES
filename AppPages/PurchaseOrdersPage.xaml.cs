using System.Collections.ObjectModel;
using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class PurchaseOrdersPage : ContentPage
{
    private readonly PurchaseOrderService _ordersService;
    private readonly ProductRepository _products;
    private readonly SupplierRepository _suppliers;
    private readonly BusinessService _businesses;
    private readonly ObservableCollection<PurchaseOrderLineDraft> _draftLines = [];
    private readonly ObservableCollection<PurchaseOrder> _orders = [];
    private long _businessId;
    private PurchaseOrder? _selectedOrder;
    private PurchaseOrderLine? _selectedOrderLine;
    private string _receiptOperationKey = CreateOperationKey();

    public PurchaseOrdersPage(
        PurchaseOrderService ordersService,
        ProductRepository products,
        SupplierRepository suppliers,
        BusinessService businesses)
    {
        InitializeComponent();
        _ordersService = ordersService;
        _products = products;
        _suppliers = suppliers;
        _businesses = businesses;
        DraftLinesList.ItemsSource = _draftLines;
        OrdersList.ItemsSource = _orders;
        UnitPicker.SelectedIndex = 0;
        InitialStatusPicker.SelectedIndex = 0;
        OrderDatePicker.Date = DateTime.Today;
        EstimatedDatePicker.Date = DateTime.Today.AddDays(7);
        ManufacturingDatePicker.Date = DateTime.Today;
        ReceiptExpirationDatePicker.Date = DateTime.Today.AddDays(30);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            var suppliers = await _suppliers.SearchAsync(_businessId);
            SupplierPicker.ItemsSource = suppliers.ToList();
            SupplierPicker.ItemDisplayBinding = new Binding(nameof(Supplier.CompanyName));
            await RefreshOrdersAsync();
        }
        catch (Exception error)
        {
            OrderResultLabel.Text = error.Message;
        }
    }

    private void HasEstimatedDateCheck_Changed(object? sender, CheckedChangedEventArgs e) =>
        EstimatedDatePicker.IsVisible = e.Value;

    private void HasManufacturingDateCheck_Changed(object? sender, CheckedChangedEventArgs e) =>
        ManufacturingDatePicker.IsVisible = e.Value;

    private void HasExpirationDateCheck_Changed(object? sender, CheckedChangedEventArgs e) =>
        ReceiptExpirationDatePicker.IsVisible = e.Value;

    private async void AddOrderLine_Clicked(object? sender, EventArgs e)
    {
        try
        {
            LineErrorLabel.Text = string.Empty;
            Product? product = null;
            var code = ProductCodeEntry.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(code))
            {
                product = await _products.FindByCodeAsync(_businessId, code);
            }

            var unit = product?.UnitOfMeasure ?? (UnitOfMeasure)Math.Max(UnitPicker.SelectedIndex, 0);
            var description = string.IsNullOrWhiteSpace(DescriptionEntry.Text)
                ? product?.Name
                : DescriptionEntry.Text.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new InventoryRuleException("Escribe una descripción o captura el código de un producto registrado.");
            }

            if (product is not null && _draftLines.Any(line => line.ProductId == product.Id))
            {
                throw new InventoryRuleException("El producto ya está incluido en el pedido.");
            }

            var quantity = ParseDecimal(RequestedQuantityEntry.Text, "La cantidad solicitada no es válida.");
            InventoryRules.ValidateQuantity(quantity, unit, "La cantidad solicitada");
            var cost = ParseOptionalDecimal(EstimatedCostEntry.Text, "El costo estimado no es válido.");
            if (cost < 0)
            {
                throw new InventoryRuleException("El costo estimado no puede ser negativo.");
            }

            var manualBarcode = product is null && code?.All(char.IsDigit) == true ? code : null;
            var manualSku = product is null && !string.IsNullOrWhiteSpace(code) && manualBarcode is null ? code : null;
            _draftLines.Add(new PurchaseOrderLineDraft
            {
                ProductId = product?.Id,
                Description = description,
                Barcode = product?.Barcode ?? manualBarcode,
                Sku = product?.Sku ?? manualSku,
                Quantity = quantity,
                UnitOfMeasure = unit,
                EstimatedUnitCost = cost,
                Notes = LineNotesEntry.Text
            });
            ClearLineForm();
            UpdateDraftTotal();
        }
        catch (Exception error)
        {
            LineErrorLabel.Text = error.Message;
        }
    }

    private void RemoveOrderLine_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: PurchaseOrderLineDraft line })
        {
            _draftLines.Remove(line);
            UpdateDraftTotal();
        }
    }

    private async void SaveOrder_Clicked(object? sender, EventArgs e)
    {
        try
        {
            SaveOrderButton.IsEnabled = false;
            OrderResultLabel.Text = string.Empty;
            var status = InitialStatusPicker.SelectedIndex == 1
                ? PurchaseOrderStatus.Draft
                : PurchaseOrderStatus.Pending;
            var input = new PurchaseOrderInput(
                (SupplierPicker.SelectedItem as Supplier)?.Id,
                ManualSupplierEntry.Text,
                DateOnly.FromDateTime(OrderDatePicker.Date ?? DateTime.Today),
                HasEstimatedDateCheck.IsChecked
                    ? DateOnly.FromDateTime(EstimatedDatePicker.Date ?? DateTime.Today)
                    : null,
                OrderNotesEntry.Text,
                _draftLines.Select(line => new PurchaseOrderLineInput(
                    line.ProductId,
                    line.Description,
                    line.Barcode,
                    line.Sku,
                    line.Quantity,
                    line.UnitOfMeasure,
                    line.EstimatedUnitCost,
                    line.Notes)).ToArray(),
                InitialStatus: status);
            var order = await _ordersService.CreateAsync(_businessId, input);
            OrderResultLabel.Text = $"Pedido {order.Folio} guardado como {order.DisplayStatus.ToLowerInvariant()}. El inventario no cambió.";
            ClearOrderForm();
            await RefreshOrdersAsync();
        }
        catch (Exception error)
        {
            OrderResultLabel.Text = error.Message;
        }
        finally
        {
            SaveOrderButton.IsEnabled = true;
        }
    }

    private async void RefreshOrders_Clicked(object? sender, EventArgs e) => await RefreshOrdersAsync();

    private async Task RefreshOrdersAsync()
    {
        var orders = await _ordersService.GetOrdersAsync(_businessId);
        _orders.Clear();
        foreach (var order in orders)
        {
            _orders.Add(order);
        }
    }

    private void OrdersList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedOrder = e.CurrentSelection.FirstOrDefault() as PurchaseOrder;
        ShowSelectedOrder();
    }

    private void ShowSelectedOrder()
    {
        var order = _selectedOrder;
        SelectedOrderPanel.IsVisible = order is not null;
        ReceiptPanel.IsVisible = false;
        _selectedOrderLine = null;
        if (order is null)
        {
            return;
        }

        SelectedOrderTitle.Text = order.Folio;
        SelectedOrderSubtitle.Text = $"{order.DisplaySupplier} · {order.DisplayStatus} · {order.DisplayDate} · {order.DisplayTotal}";
        OrderLinesList.ItemsSource = order.Lines;
        ConfirmOrderButton.IsVisible = order.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Pending;
        CancelOrderButton.IsVisible = order.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Pending or PurchaseOrderStatus.Confirmed;
        CancelReasonEntry.IsVisible = CancelOrderButton.IsVisible;
    }

    private async void ConfirmOrder_Clicked(object? sender, EventArgs e)
    {
        if (_selectedOrder is null)
        {
            return;
        }

        try
        {
            _selectedOrder = await _ordersService.ConfirmAsync(_businessId, _selectedOrder.Id);
            OrderResultLabel.Text = $"Pedido {_selectedOrder.Folio} confirmado.";
            ShowSelectedOrder();
            await RefreshOrdersAsync();
        }
        catch (Exception error)
        {
            OrderResultLabel.Text = error.Message;
        }
    }

    private async void CancelOrder_Clicked(object? sender, EventArgs e)
    {
        if (_selectedOrder is null)
        {
            return;
        }

        try
        {
            _selectedOrder = await _ordersService.CancelAsync(
                _businessId,
                _selectedOrder.Id,
                CancelReasonEntry.Text ?? string.Empty);
            CancelReasonEntry.Text = string.Empty;
            OrderResultLabel.Text = $"Pedido {_selectedOrder.Folio} cancelado; se conservó su historial.";
            ShowSelectedOrder();
            await RefreshOrdersAsync();
        }
        catch (Exception error)
        {
            OrderResultLabel.Text = error.Message;
        }
    }

    private async void OrderLinesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedOrderLine = e.CurrentSelection.FirstOrDefault() as PurchaseOrderLine;
        ReceiptPanel.IsVisible = _selectedOrderLine is { PendingQuantity: > 0 } &&
                                 _selectedOrder?.Status is PurchaseOrderStatus.Pending
                                     or PurchaseOrderStatus.Confirmed
                                     or PurchaseOrderStatus.PartiallyReceived;
        if (!ReceiptPanel.IsVisible || _selectedOrderLine is null)
        {
            return;
        }

        ReceiptLineTitle.Text = $"{_selectedOrderLine.Description} · Pendiente {_selectedOrderLine.PendingQuantity:0.###}";
        ReceiptQuantityEntry.Text = _selectedOrderLine.PendingQuantity.ToString(CultureInfo.CurrentCulture);
        _receiptOperationKey = CreateOperationKey();
        if (_selectedOrderLine.ProductId.HasValue)
        {
            var product = await _products.GetAsync(_businessId, _selectedOrderLine.ProductId.Value);
            ReceiptProductCodeEntry.Text = product?.DisplayCode;
            HasExpirationDateCheck.IsChecked = product?.ExpirationMode == ExpirationMode.Tracked;
        }
        else
        {
            ReceiptProductCodeEntry.Text = _selectedOrderLine.Barcode ?? _selectedOrderLine.Sku;
        }
    }

    private async void CreateProductForReceipt_Clicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//NewItemPage");

    private async void ReceiveSelectedLine_Clicked(object? sender, EventArgs e)
    {
        if (_selectedOrder is null || _selectedOrderLine is null)
        {
            return;
        }

        try
        {
            ReceiptResultLabel.Text = string.Empty;
            var product = await _products.FindByCodeAsync(
                _businessId,
                ReceiptProductCodeEntry.Text ?? string.Empty)
                ?? throw new InventoryRuleException("El producto destino no existe. Regístralo y vuelve a seleccionarlo.");
            var receipt = await _ordersService.ReceiveAsync(
                _businessId,
                _selectedOrder.Id,
                [new PurchaseReceiptInput(
                    _selectedOrderLine.Id,
                    product.Id,
                    ParseDecimal(ReceiptQuantityEntry.Text, "La cantidad recibida no es válida."),
                    ReceiptLotCodeEntry.Text,
                    HasManufacturingDateCheck.IsChecked
                        ? DateOnly.FromDateTime(ManufacturingDatePicker.Date ?? DateTime.Today)
                        : null,
                    HasExpirationDateCheck.IsChecked
                        ? DateOnly.FromDateTime(ReceiptExpirationDatePicker.Date ?? DateTime.Today)
                        : null,
                    ParseOptionalDecimal(ReceiptCostEntry.Text, "El costo recibido no es válido."))],
                _receiptOperationKey,
                ReceiptNotesEntry.Text);
            ReceiptResultLabel.Style = (Style)Application.Current!.Resources["InfoText"];
            ReceiptResultLabel.Text = $"Recepción {receipt.Reference} confirmada. El inventario se actualizó una sola vez.";
            _receiptOperationKey = CreateOperationKey();
            _selectedOrder = await _ordersService.GetAsync(_businessId, _selectedOrder.Id);
            ClearReceiptForm();
            ShowSelectedOrder();
            await RefreshOrdersAsync();
        }
        catch (Exception error)
        {
            ReceiptResultLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            ReceiptResultLabel.Text = error.Message;
        }
    }

    private void ClearLineForm()
    {
        ProductCodeEntry.Text = string.Empty;
        DescriptionEntry.Text = string.Empty;
        RequestedQuantityEntry.Text = string.Empty;
        EstimatedCostEntry.Text = string.Empty;
        LineNotesEntry.Text = string.Empty;
        UnitPicker.SelectedIndex = 0;
    }

    private void ClearOrderForm()
    {
        SupplierPicker.SelectedItem = null;
        ManualSupplierEntry.Text = string.Empty;
        OrderDatePicker.Date = DateTime.Today;
        HasEstimatedDateCheck.IsChecked = false;
        OrderNotesEntry.Text = string.Empty;
        InitialStatusPicker.SelectedIndex = 0;
        _draftLines.Clear();
        UpdateDraftTotal();
    }

    private void ClearReceiptForm()
    {
        ReceiptProductCodeEntry.Text = string.Empty;
        ReceiptQuantityEntry.Text = string.Empty;
        ReceiptLotCodeEntry.Text = string.Empty;
        ReceiptCostEntry.Text = string.Empty;
        ReceiptNotesEntry.Text = string.Empty;
        HasManufacturingDateCheck.IsChecked = false;
        HasExpirationDateCheck.IsChecked = false;
    }

    private void UpdateDraftTotal() =>
        DraftTotalLabel.Text = $"Total estimado: {_draftLines.Sum(line => line.Quantity * (line.EstimatedUnitCost ?? 0m)):C}";

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

    private static string CreateOperationKey() => $"UI-{Guid.NewGuid():N}";
}
