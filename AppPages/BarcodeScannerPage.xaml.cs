using InventorySystem.Domain;
using InventorySystem.Services;

namespace InventorySystem.AppPages;

public partial class BarcodeScannerPage : ContentPage
{
    private readonly IBarcodeCameraScannerService _scanner;
    private BarcodeScannerSession? _session;
    private IReadOnlyList<BarcodeCameraDevice> _cameras = [];
    private bool _loadingCamera;
    private bool _resultSubmitted;
    private CancellationTokenSource? _pageCancellation;

    public BarcodeScannerPage(IBarcodeCameraScannerService scanner)
    {
        InitializeComponent();
        _scanner = scanner;
    }

    public void Configure(BarcodeScannerSession session)
    {
        _session = session;
        Title = session.Title;
        _resultSubmitted = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = new CancellationTokenSource();
        await InitializeScannerAsync(_pageCancellation.Token);
    }

    protected override async void OnDisappearing()
    {
        await StopScannerAsync();
        _pageCancellation?.Cancel();
        base.OnDisappearing();
    }

    private async Task InitializeScannerAsync(CancellationToken cancellationToken)
    {
        if (_session is null || _loadingCamera)
        {
            return;
        }

        try
        {
            _loadingCamera = true;
            BusyPanel.IsVisible = true;
            PermissionLabel.IsVisible = false;
            StatusLabel.Text = "Comprobando cámaras disponibles...";
            CameraCountLabel.Text = "Buscando cámaras conectadas...";
            CameraPicker.IsEnabled = false;
            CameraPicker.ItemsSource = null;
            CameraPicker.SelectedItem = null;
            SelectedCameraLabel.Text = "Ninguna cámara seleccionada.";

            var permission = await _scanner.RequestAccessAsync(cancellationToken);
            if (permission != BarcodeScannerPermissionStatus.Granted)
            {
                PermissionLabel.IsVisible = true;
                PermissionLabel.Text = permission switch
                {
                    BarcodeScannerPermissionStatus.Denied =>
                        "No tenemos permiso para usar la cámara. Puedes escribir el código manualmente.",
                    BarcodeScannerPermissionStatus.Restricted =>
                        "El acceso a la cámara está restringido por el sistema.",
                    BarcodeScannerPermissionStatus.Disabled =>
                        "El acceso a la cámara está deshabilitado. En Windows revisa Configuración > Privacidad y seguridad > Cámara.",
                    _ => "No fue posible acceder a la cámara."
                };
                StatusLabel.Text = "Puedes escribir el código manualmente o reintentar después de habilitar el permiso.";
                return;
            }

            _cameras = await _scanner.GetAvailableCamerasAsync(cancellationToken);
            var cameraList = _cameras.ToList();
            CameraPicker.ItemsSource = cameraList;
            CameraPicker.IsEnabled = _cameras.Count > 0;
            CameraCountLabel.Text = _cameras.Count switch
            {
                0 => "No se detectaron cámaras conectadas.",
                1 => "Se detectó 1 cámara. Puedes abrir el selector para revisar el dispositivo.",
                _ => $"Se detectaron {_cameras.Count} cámaras. Selecciona la que quieras utilizar."
            };
            if (_cameras.Count == 0)
            {
                StatusLabel.Text = "No encontramos cámaras disponibles. Puedes escribir el código manualmente.";
                return;
            }

            var preferred = _cameras.FirstOrDefault(camera => camera.Id == _session.PreferredCameraId)
                ?? _cameras.First();
            CameraPicker.SelectedIndex = cameraList.FindIndex(camera => camera.Id == preferred.Id);
            await StartSelectedCameraAsync(preferred.Id, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            StatusLabel.Text = BarcodeScannerMessages.ToUserMessage(error);
        }
        finally
        {
            BusyPanel.IsVisible = false;
            _loadingCamera = false;
        }
    }

    private async Task StartSelectedCameraAsync(string cameraId, CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            return;
        }

        var camera = _cameras.FirstOrDefault(item => item.Id == cameraId);
        StatusLabel.Text = camera is null
            ? "Iniciando vista previa..."
            : $"Iniciando {camera.SelectorLabel}...";
        await _scanner.StartAsync(
            CameraPreview,
            cameraId,
            OnBarcodeDetected,
            message => MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = message),
            cancellationToken);
        _session.PreferredCameraId = cameraId;
        SelectedCameraLabel.Text = camera is null
            ? "Cámara en uso: dispositivo seleccionado."
            : $"Cámara en uso: {camera.SelectorLabel}.";
        StatusLabel.Text = camera is null
            ? "Coloca el código dentro del recuadro."
            : $"Usando {camera.SelectorLabel}. Coloca el código dentro del recuadro.";
    }

    private void OnBarcodeDetected(BarcodeScanResult result)
    {
        if (_resultSubmitted)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_resultSubmitted)
            {
                return;
            }

            if (!result.Success || string.IsNullOrWhiteSpace(result.Code))
            {
                StatusLabel.Text = result.Error ?? "No pudimos leer el código. Intenta de nuevo.";
                return;
            }

            _resultSubmitted = true;
            BusyPanel.IsVisible = true;
            StatusLabel.Text = "Código detectado. Estamos regresando al formulario.";
            _session?.TrySetResult(result);
            await StopScannerAsync();
            await Navigation.PopModalAsync();
        });
    }

    private async void CameraPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loadingCamera || CameraPicker.SelectedItem is not BarcodeCameraDevice camera || _pageCancellation is null)
        {
            return;
        }

        try
        {
            BusyPanel.IsVisible = true;
            await StopScannerAsync();
            await StartSelectedCameraAsync(camera.Id, _pageCancellation.Token);
        }
        catch (Exception error)
        {
            StatusLabel.Text = BarcodeScannerMessages.ToUserMessage(error);
        }
        finally
        {
            BusyPanel.IsVisible = false;
        }
    }

    private async void RefreshCameras_Clicked(object? sender, EventArgs e)
    {
        _resultSubmitted = false;
        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = new CancellationTokenSource();
        await StopScannerAsync();
        await InitializeScannerAsync(_pageCancellation.Token);
    }

    private async void Retry_Clicked(object? sender, EventArgs e)
    {
        _resultSubmitted = false;
        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = new CancellationTokenSource();
        await StopScannerAsync();
        await InitializeScannerAsync(_pageCancellation.Token);
    }

    private async void Manual_Clicked(object? sender, EventArgs e)
    {
        _session?.TrySetResult(BarcodeScanResult.Failed("Captura manual solicitada."));
        await StopScannerAsync();
        await Navigation.PopModalAsync();
    }

    private async void Cancel_Clicked(object? sender, EventArgs e)
    {
        _session?.TryCancel();
        await StopScannerAsync();
        await Navigation.PopModalAsync();
    }

    private async Task StopScannerAsync()
    {
        try
        {
            await _scanner.StopAsync();
        }
        catch
        {
            // El cierre de cámara no debe bloquear la navegación.
        }
    }
}
