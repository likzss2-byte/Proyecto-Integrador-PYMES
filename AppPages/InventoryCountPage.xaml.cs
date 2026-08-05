using System.Collections.ObjectModel;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class InventoryCountPage : ContentPage
{
    private readonly InventoryCountType _mode;
    private readonly InventoryCountSessionService _sessions;
    private readonly InventoryCatalogService _catalog;
    private readonly SupplierRepository _suppliers;
    private readonly BusinessService _businesses;
    private readonly ObservableCollection<InventoryCountRowViewModel> _visibleRows = [];
    private readonly ObservableCollection<InventoryLotCountRowViewModel> _lotRows = [];
    private readonly List<InventoryCountRowViewModel> _allRows = [];
    private CancellationTokenSource? _searchCancellation;
    private InventoryCount? _currentSession;
    private InventoryCountRowViewModel? _selectedLotProduct;
    private long _businessId;

    protected InventoryCountPage(
        InventoryCountType mode,
        InventoryCountSessionService sessions,
        InventoryCatalogService catalog,
        SupplierRepository suppliers,
        BusinessService businesses)
    {
        InitializeComponent();
        _mode = mode;
        _sessions = sessions;
        _catalog = catalog;
        _suppliers = suppliers;
        _businesses = businesses;
        CountRowsList.ItemsSource = _visibleRows;
        LotRowsList.ItemsSource = _lotRows;
        ConfigureMode();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            await LoadSelectorsAsync();
            await RefreshOpenSessionsAsync();
            if (_mode == InventoryCountType.FreeOperational)
            {
                await RefreshFreeProductsAsync(string.Empty);
                ScanCodeEntry.Focus();
            }
        }
        catch (Exception error)
        {
            ShowMessage(error.Message, isError: true);
        }
    }

    protected override void OnDisappearing()
    {
        _searchCancellation?.Cancel();
        base.OnDisappearing();
    }

    private void ConfigureMode()
    {
        SupplierFilterPanel.IsVisible = _mode == InventoryCountType.BySupplier;
        BrandFilterPanel.IsVisible = _mode == InventoryCountType.ByBrand;
        FreeStartPanel.IsVisible = _mode == InventoryCountType.FreeOperational;
        FreeProductPanel.IsVisible = _mode == InventoryCountType.FreeOperational;
        (PageTitleLabel.Text, PageSubtitleLabel.Text, Title) = _mode switch
        {
            InventoryCountType.BySupplier => (
                "Inventario por proveedor",
                "Cuenta únicamente los productos relacionados con el proveedor seleccionado.",
                "Inventario por proveedor"),
            InventoryCountType.ByBrand => (
                "Inventario por marca",
                "Cuenta únicamente los productos de una marca registrada.",
                "Inventario por marca"),
            _ => (
                "Inventario operativo",
                "Agrega productos en cualquier orden mediante código, SKU o búsqueda.",
                "Inventario operativo")
        };
    }

    private async Task LoadSelectorsAsync()
    {
        if (_mode == InventoryCountType.BySupplier)
        {
            SupplierPicker.ItemsSource = (await _suppliers.SearchAsync(_businessId)).ToArray();
        }
        else if (_mode == InventoryCountType.ByBrand)
        {
            BrandPicker.ItemsSource = (await _catalog.GetBrandsAsync(_businessId)).ToArray();
        }
    }

    private async Task RefreshOpenSessionsAsync()
    {
        if (_businessId == 0)
        {
            return;
        }

        var sessions = await _sessions.GetOpenAsync(_businessId, _mode);
        OpenSessionsPicker.ItemsSource = sessions
            .Select(session => new InventorySessionOption(
                session.Id,
                $"{session.Reference} · {session.DisplayMode} · {session.DisplayProgress}"))
            .ToArray();
        OpenSessionsPicker.SelectedIndex = sessions.Count > 0 ? 0 : -1;
    }

    private async void RefreshSessions_Clicked(object? sender, EventArgs e)
    {
        try
        {
            await RefreshOpenSessionsAsync();
            ShowMessage("Lista de sesiones actualizada.");
        }
        catch (Exception error)
        {
            ShowMessage(error.Message, isError: true);
        }
    }

    private async void ResumeSession_Clicked(object? sender, EventArgs e)
    {
        if (OpenSessionsPicker.SelectedItem is not InventorySessionOption option)
        {
            ShowMessage("Selecciona una sesión guardada.", isError: true);
            return;
        }

        try
        {
            var session = await _sessions.GetAsync(_businessId, option.Id)
                ?? throw new InventoryRuleException("La sesión seleccionada ya no existe.");
            LoadSession(session);
            ShowMessage("Sesión cargada. Puedes continuar donde la dejaste.");
        }
        catch (Exception error)
        {
            ShowMessage(error.Message, isError: true);
        }
    }

    private async void StartSession_Clicked(object? sender, EventArgs e)
    {
        try
        {
            var input = _mode switch
            {
                InventoryCountType.BySupplier when SupplierPicker.SelectedItem is Supplier supplier =>
                    new InventoryCountSessionInput(_mode, SupplierId: supplier.Id),
                InventoryCountType.ByBrand when BrandPicker.SelectedItem is string brand =>
                    new InventoryCountSessionInput(_mode, Brand: brand),
                InventoryCountType.FreeOperational => new InventoryCountSessionInput(_mode),
                InventoryCountType.BySupplier => throw new InventoryRuleException("Selecciona un proveedor."),
                _ => throw new InventoryRuleException("Selecciona una marca.")
            };
            var session = await _sessions.CreateAsync(_businessId, input);
            LoadSession(session);
            await RefreshOpenSessionsAsync();
            StartErrorLabel.Text = string.Empty;
            ShowMessage($"Sesión {session.Reference} iniciada.");
            if (_mode == InventoryCountType.FreeOperational)
            {
                ScanCodeEntry.Focus();
            }
        }
        catch (Exception error)
        {
            StartErrorLabel.Text = error.Message;
            ShowMessage(error.Message, isError: true);
        }
    }

    private void LoadSession(InventoryCount session)
    {
        _currentSession = session;
        StartPanel.IsVisible = false;
        SessionPanel.IsVisible = true;
        SessionReferenceLabel.Text = session.Reference;
        SessionFilterLabel.Text = session.DisplayMode;
        SessionNotesEditor.Text = session.Notes ?? string.Empty;
        LotPanel.IsVisible = false;
        _selectedLotProduct = null;
        _allRows.Clear();
        _allRows.AddRange(session.Lines.Select(line =>
            new InventoryCountRowViewModel(line, session.Type == InventoryCountType.FreeOperational)));
        ApplySessionFilter();
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        if (_currentSession is null)
        {
            return;
        }

        var counted = _allRows.Count(row => row.TryGetPhysical(out _, out _));
        var pending = _allRows.Count - counted;
        ProgressLabel.Text = $"{counted} / {_allRows.Count}";
        PendingLabel.Text = pending == 0 ? "Conteo completo" : $"{pending} pendiente(s)";
    }

    private void SessionSearch_TextChanged(object? sender, TextChangedEventArgs e) => ApplySessionFilter();

    private void ClearSearch_Clicked(object? sender, EventArgs e)
    {
        SessionSearchEntry.Text = string.Empty;
        ApplySessionFilter();
    }

    private void ApplySessionFilter()
    {
        var search = (SessionSearchEntry.Text ?? string.Empty).Trim();
        var filtered = search.Length == 0
            ? _allRows
            : _allRows.Where(row =>
                    row.ProductName.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                    || row.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || row.Line.Sku.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (row.Line.Barcode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (_mode == InventoryCountType.BySupplier
                        && (row.Line.Brand?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false)))
                .ToList();
        _visibleRows.Clear();
        foreach (var row in filtered)
        {
            _visibleRows.Add(row);
        }
    }

    private async void FreeSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        try
        {
            await Task.Delay(180, _searchCancellation.Token);
            await RefreshFreeProductsAsync(e.NewTextValue, _searchCancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            ShowMessage(error.Message, isError: true);
        }
    }

    private async Task RefreshFreeProductsAsync(string? search, CancellationToken cancellationToken = default)
    {
        var products = await _catalog.SearchForFreeInventoryAsync(_businessId, search, cancellationToken);
        FreeProductPicker.ItemsSource = products.Take(100).ToArray();
        FreeProductPicker.SelectedIndex = products.Count > 0 ? 0 : -1;
    }

    private async void ScanCode_Completed(object? sender, EventArgs e) => await AddByCodeAsync();

    private async void AddByCode_Clicked(object? sender, EventArgs e) => await AddByCodeAsync();

    private async Task AddByCodeAsync()
    {
        if (!EnsureActiveSession())
        {
            return;
        }

        var code = InventoryRules.NormalizeScannedCode(ScanCodeEntry.Text);
        if (code.Length == 0)
        {
            ShowMessage("Escanea o escribe un código de barras o SKU.", isError: true);
            ScanCodeEntry.Focus();
            return;
        }

        try
        {
            var product = await _catalog.FindByCodeAsync(_businessId, code)
                ?? throw new InventoryRuleException("No se encontró un producto con ese código o SKU.");
            await AddProductAsync(product);
        }
        catch (Exception error)
        {
            ShowMessage(error.Message, isError: true);
        }
        finally
        {
            ScanCodeEntry.CursorPosition = 0;
            ScanCodeEntry.SelectionLength = ScanCodeEntry.Text?.Length ?? 0;
            ScanCodeEntry.Focus();
        }
    }

    private async void AddSelectedProduct_Clicked(object? sender, EventArgs e)
    {
        if (FreeProductPicker.SelectedItem is not Product product)
        {
            ShowMessage("Selecciona un producto de los resultados.", isError: true);
            return;
        }

        await AddProductAsync(product);
        ScanCodeEntry.Focus();
    }

    private async Task AddProductAsync(Product product)
    {
        if (!EnsureActiveSession())
        {
            return;
        }

        var existing = _allRows.FirstOrDefault(row => row.ProductId == product.Id);
        if (existing is not null)
        {
            CountRowsList.SelectedItem = existing;
            CountRowsList.ScrollTo(existing, position: ScrollToPosition.Center, animate: true);
            ShowMessage($"{product.Name} ya está en la sesión; se abrió su fila sin cambiar la cantidad.");
            return;
        }

        try
        {
            var session = await _sessions.AddProductAsync(_businessId, _currentSession!.Id, product.Id);
            LoadSession(session);
            var added = _allRows.Single(row => row.ProductId == product.Id);
            CountRowsList.SelectedItem = added;
            CountRowsList.ScrollTo(added, position: ScrollToPosition.Center, animate: true);
            ShowMessage($"{product.Name} agregado.");
        }
        catch (Exception error)
        {
            ShowMessage(error.Message, isError: true);
        }
    }

    private async void RemoveProduct_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: InventoryCountRowViewModel row } || !EnsureActiveSession())
        {
            return;
        }

        try
        {
            var session = await _sessions.RemoveProductAsync(_businessId, _currentSession!.Id, row.ProductId);
            LoadSession(session);
            ShowMessage($"{row.ProductName} se quitó de la sesión.");
        }
        catch (Exception error)
        {
            ShowMessage(error.Message, isError: true);
        }
    }

    private async void PhysicalEntry_Completed(object? sender, EventArgs e)
    {
        if (sender is not Entry { BindingContext: InventoryCountRowViewModel row })
        {
            return;
        }

        if (await SaveRowAsync(row))
        {
            await MoveToNextPendingAsync(row);
        }
    }

    private async Task<bool> SaveRowAsync(InventoryCountRowViewModel row)
    {
        if (!EnsureActiveSession())
        {
            return false;
        }

        if (!row.TryGetPhysical(out var physical, out var error))
        {
            if (error is not null)
            {
                ShowMessage(error, isError: true);
            }

            return false;
        }

        try
        {
            await _sessions.SetPhysicalQuantityAsync(_businessId, _currentSession!.Id, row.ProductId, physical);
            row.Line.PhysicalStock = physical;
            UpdateProgress();
            ShowMessage($"Conteo de {row.ProductName} guardado.");
            return true;
        }
        catch (Exception exception)
        {
            ShowMessage(exception.Message, isError: true);
            return false;
        }
    }

    private async Task MoveToNextPendingAsync(InventoryCountRowViewModel current)
    {
        var currentIndex = _allRows.IndexOf(current);
        var next = _allRows.Skip(currentIndex + 1)
            .Concat(_allRows.Take(currentIndex + 1))
            .FirstOrDefault(row => string.IsNullOrWhiteSpace(row.PhysicalText));
        if (next is null)
        {
            ShowMessage("Todos los productos visibles en la sesión tienen cantidad capturada.");
            return;
        }

        if (!_visibleRows.Contains(next))
        {
            SessionSearchEntry.Text = string.Empty;
            ApplySessionFilter();
        }

        CountRowsList.SelectedItem = next;
        CountRowsList.ScrollTo(next, position: ScrollToPosition.Center, animate: true);
        ShowMessage($"Siguiente pendiente: {next.ProductName}.");
        await Task.Delay(120);
        CountRowsList.GetVisualTreeDescendants()
            .OfType<Entry>()
            .FirstOrDefault(entry => ReferenceEquals(entry.BindingContext, next))
            ?.Focus();
    }

    private async Task SaveAllCapturedRowsAsync()
    {
        foreach (var row in _allRows.Where(item => !string.IsNullOrWhiteSpace(item.PhysicalText)))
        {
            if (!row.TryGetPhysical(out var physical, out var error))
            {
                throw new InventoryRuleException(error ?? "Hay una cantidad inválida.");
            }

            if (row.Line.PhysicalStock != physical)
            {
                await _sessions.SetPhysicalQuantityAsync(_businessId, _currentSession!.Id, row.ProductId, physical);
                row.Line.PhysicalStock = physical;
            }
        }

        _currentSession = await _sessions.SaveProgressAsync(
            _businessId,
            _currentSession!.Id,
            SessionNotesEditor.Text);
        UpdateProgress();
    }

    private async void SaveProgress_Clicked(object? sender, EventArgs e)
    {
        if (!EnsureActiveSession())
        {
            return;
        }

        try
        {
            await SaveAllCapturedRowsAsync();
            await RefreshOpenSessionsAsync();
            ShowMessage("Avance guardado. El stock todavía no fue modificado.");
        }
        catch (Exception error)
        {
            ShowMessage(error.Message, isError: true);
        }
    }

    private async void ConfirmSession_Clicked(object? sender, EventArgs e)
    {
        if (!EnsureActiveSession())
        {
            return;
        }

        try
        {
            await SaveAllCapturedRowsAsync();
            var session = await _sessions.GetAsync(_businessId, _currentSession!.Id)
                ?? throw new InventoryRuleException("La sesión ya no existe.");
            var summary = InventoryCountSessionService.BuildSummary(session);
            var message =
                $"Sin diferencia: {summary.WithoutDifference}\n" +
                $"Con faltante: {summary.WithMissing}\n" +
                $"Con sobrante: {summary.WithSurplus}\n" +
                $"Sin contar: {summary.Pending}\n\n" +
                "Los ajustes se registrarán como movimientos de inventario.";
            if (!await DisplayAlertAsync("Confirmar inventario", message, "Continuar", "Volver"))
            {
                return;
            }

            var allowIncomplete = false;
            if (summary.Pending > 0)
            {
                allowIncomplete = await DisplayAlertAsync(
                    "Sesión incompleta",
                    "Los productos pendientes no serán ajustados. ¿Deseas finalizar la sesión de todas formas?",
                    "Finalizar incompleta",
                    "Seguir contando");
                if (!allowIncomplete)
                {
                    return;
                }
            }

            var completed = await _sessions.ConfirmAsync(_businessId, session.Id, allowIncomplete);
            _currentSession = completed;
            SessionPanel.IsVisible = false;
            StartPanel.IsVisible = true;
            await RefreshOpenSessionsAsync();
            StartErrorLabel.Text = $"{completed.Reference} confirmado correctamente.";
            ShowMessage("Inventario confirmado y movimientos registrados.");
        }
        catch (Exception error)
        {
            ShowMessage(error.Message, isError: true);
        }
    }

    private async void CancelSession_Clicked(object? sender, EventArgs e)
    {
        if (!EnsureActiveSession()
            || !await DisplayAlertAsync(
                "Cancelar sesión",
                "La sesión se conservará en el historial y no modificará el stock.",
                "Cancelar sesión",
                "Volver"))
        {
            return;
        }

        try
        {
            var cancelled = await _sessions.CancelAsync(
                _businessId,
                _currentSession!.Id,
                SessionNotesEditor.Text);
            _currentSession = cancelled;
            SessionPanel.IsVisible = false;
            StartPanel.IsVisible = true;
            await RefreshOpenSessionsAsync();
            StartErrorLabel.Text = $"{cancelled.Reference} fue cancelado sin modificar existencias.";
        }
        catch (Exception error)
        {
            ShowMessage(error.Message, isError: true);
        }
    }

    private async void OpenLots_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: InventoryCountRowViewModel row } || !EnsureActiveSession())
        {
            return;
        }

        try
        {
            var session = row.Line.CountByLot
                ? await _sessions.GetAsync(_businessId, _currentSession!.Id)
                : await _sessions.BeginLotCountAsync(_businessId, _currentSession!.Id, row.ProductId);
            LoadSession(session ?? throw new InventoryRuleException("La sesión ya no existe."));
            var selected = _allRows.Single(item => item.ProductId == row.ProductId);
            OpenLotPanel(selected);
        }
        catch (Exception error)
        {
            ShowMessage(error.Message, isError: true);
        }
    }

    private void OpenLotPanel(InventoryCountRowViewModel row)
    {
        _selectedLotProduct = row;
        LotProductLabel.Text = $"Conteo por lote · {row.ProductName}";
        _lotRows.Clear();
        foreach (var lot in row.Line.LotLines)
        {
            _lotRows.Add(new InventoryLotCountRowViewModel(lot, row.Line.UnitOfMeasure));
        }

        LotPanel.IsVisible = true;
    }

    private void CloseLots_Clicked(object? sender, EventArgs e)
    {
        LotPanel.IsVisible = false;
        _selectedLotProduct = null;
    }

    private async void LotPhysicalEntry_Completed(object? sender, EventArgs e)
    {
        if (sender is not Entry { BindingContext: InventoryLotCountRowViewModel row })
        {
            return;
        }

        if (await SaveLotRowAsync(row))
        {
            var index = _lotRows.IndexOf(row);
            var next = _lotRows.Skip(index + 1).FirstOrDefault(item => string.IsNullOrWhiteSpace(item.PhysicalText));
            if (next is not null)
            {
                LotRowsList.SelectedItem = next;
                LotRowsList.ScrollTo(next, position: ScrollToPosition.Center, animate: true);
                await Task.Delay(120);
                LotRowsList.GetVisualTreeDescendants()
                    .OfType<Entry>()
                    .FirstOrDefault(entry => ReferenceEquals(entry.BindingContext, next))
                    ?.Focus();
            }
        }
    }

    private async Task<bool> SaveLotRowAsync(InventoryLotCountRowViewModel row)
    {
        if (!row.TryGetPhysical(out var physical, out var error))
        {
            ShowMessage(error ?? "La cantidad del lote no es válida.", isError: true);
            return false;
        }

        try
        {
            _currentSession = await _sessions.SetLotPhysicalQuantityAsync(
                _businessId,
                _currentSession!.Id,
                row.Line.Id,
                physical);
            row.Line.PhysicalQuantity = physical;
            ShowMessage($"Lote {row.LotCode} guardado.");
            return true;
        }
        catch (Exception exception)
        {
            ShowMessage(exception.Message, isError: true);
            return false;
        }
    }

    private async void SaveLots_Clicked(object? sender, EventArgs e)
    {
        if (_selectedLotProduct is null || !EnsureActiveSession())
        {
            return;
        }

        try
        {
            foreach (var row in _lotRows.Where(item => !string.IsNullOrWhiteSpace(item.PhysicalText)))
            {
                if (!row.TryGetPhysical(out var physical, out var error))
                {
                    throw new InventoryRuleException(error ?? "Hay una cantidad de lote inválida.");
                }

                if (row.Line.PhysicalQuantity != physical)
                {
                    _currentSession = await _sessions.SetLotPhysicalQuantityAsync(
                        _businessId,
                        _currentSession!.Id,
                        row.Line.Id,
                        physical);
                }
            }

            var productId = _selectedLotProduct.ProductId;
            var refreshed = await _sessions.GetAsync(_businessId, _currentSession!.Id)
                ?? throw new InventoryRuleException("La sesión ya no existe.");
            LoadSession(refreshed);
            var selected = _allRows.Single(item => item.ProductId == productId);
            OpenLotPanel(selected);
            UpdateProgress();
            ShowMessage(selected.Line.Counted
                ? "Conteo por lote completo y guardado."
                : "Avance de lotes guardado; todavía hay lotes pendientes.");
        }
        catch (Exception error)
        {
            ShowMessage(error.Message, isError: true);
        }
    }

    private bool EnsureActiveSession()
    {
        if (_currentSession is not null && _currentSession.IsEditable)
        {
            return true;
        }

        ShowMessage("Inicia o continúa una sesión antes de capturar productos.", isError: true);
        return false;
    }

    private void ShowMessage(string message, bool isError = false)
    {
        SessionMessageLabel.Text = message;
        SessionMessageLabel.TextColor = isError ? Color.FromArgb("#B42318") : Color.FromArgb("#28633A");
    }

    private async void Back_Clicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//InventoryPage");
}

public sealed class SupplierInventoryPage : InventoryCountPage
{
    public SupplierInventoryPage(
        InventoryCountSessionService sessions,
        InventoryCatalogService catalog,
        SupplierRepository suppliers,
        BusinessService businesses)
        : base(InventoryCountType.BySupplier, sessions, catalog, suppliers, businesses)
    {
    }
}

public sealed class BrandInventoryPage : InventoryCountPage
{
    public BrandInventoryPage(
        InventoryCountSessionService sessions,
        InventoryCatalogService catalog,
        SupplierRepository suppliers,
        BusinessService businesses)
        : base(InventoryCountType.ByBrand, sessions, catalog, suppliers, businesses)
    {
    }
}

public sealed class OperationalInventoryPage : InventoryCountPage
{
    public OperationalInventoryPage(
        InventoryCountSessionService sessions,
        InventoryCatalogService catalog,
        SupplierRepository suppliers,
        BusinessService businesses)
        : base(InventoryCountType.FreeOperational, sessions, catalog, suppliers, businesses)
    {
    }
}
