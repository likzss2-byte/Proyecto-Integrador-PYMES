using InventorySystem.Controls;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using WinUIImage = Microsoft.UI.Xaml.Controls.Image;

namespace InventorySystem.Platforms.Windows;

public sealed class WindowsBarcodeCameraScannerService : IBarcodeCameraScannerService
{
    private readonly BarcodeScannerService _decoder;
    private readonly SemaphoreSlim _decodeLock = new(1, 1);
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private MediaFrameSourceGroup? _activeGroup;
    private WinUIImage? _previewImage;
    private Action<BarcodeScanResult>? _onDetected;
    private Action<string>? _onStatus;
    private DispatcherQueue? _dispatcherQueue;
    private long _lastDecodeTicks;
    private long _lastPreviewTicks;
    private int _previewUpdateQueued;
    private int _previewFramePresented;
    private bool _resultDelivered;
    private bool _stopping;

    public WindowsBarcodeCameraScannerService(BarcodeScannerService decoder)
    {
        _decoder = decoder;
    }

    public async Task<BarcodeScannerAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var devices = await GetAvailableCamerasAsync(cancellationToken);
            return devices.Count == 0 ? BarcodeScannerAvailability.NoCamera : BarcodeScannerAvailability.Available;
        }
        catch (UnauthorizedAccessException)
        {
            return BarcodeScannerAvailability.PermissionDenied;
        }
        catch
        {
            return BarcodeScannerAvailability.Error;
        }
    }

    public async Task<IReadOnlyList<BarcodeCameraDevice>> GetAvailableCamerasAsync(CancellationToken cancellationToken = default)
    {
        var videoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        var groups = await MediaFrameSourceGroup.FindAllAsync();
        var devices = new List<BarcodeCameraDevice>();

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deviceInfo = group.SourceInfos
                .Select(info => info.DeviceInformation)
                .FirstOrDefault(info => info is not null && videoDevices.Any(device => device.Id == info.Id));
            var displayName = group.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = deviceInfo?.Name ?? "Cámara de video";
            }

            var external = IsExternalCamera(displayName);
            var kind = external ? BarcodeCameraDeviceKind.External : BarcodeCameraDeviceKind.Integrated;
            devices.Add(new BarcodeCameraDevice(group.Id, displayName, kind, external));
        }

        if (devices.Count == 0)
        {
            foreach (var device in videoDevices)
            {
                var external = IsExternalCamera(device.Name);
                devices.Add(new BarcodeCameraDevice(
                    device.Id,
                    device.Name,
                    external ? BarcodeCameraDeviceKind.External : BarcodeCameraDeviceKind.Integrated,
                    external));
            }
        }

        return devices
            .GroupBy(device => device.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private static bool IsExternalCamera(string displayName)
    {
        var lower = displayName.ToLowerInvariant();
        var explicitlyIntegrated = lower.Contains("integrated", StringComparison.Ordinal)
            || lower.Contains("internal", StringComparison.Ordinal)
            || lower.Contains("interna", StringComparison.Ordinal)
            || lower.Contains("built-in", StringComparison.Ordinal)
            || lower.Contains("builtin", StringComparison.Ordinal);
        if (explicitlyIntegrated)
        {
            return false;
        }

        return lower.Contains("usb", StringComparison.Ordinal)
            || lower.Contains("external", StringComparison.Ordinal)
            || lower.Contains("externa", StringComparison.Ordinal)
            || lower.Contains("logitech", StringComparison.Ordinal)
            || lower.Contains("razer", StringComparison.Ordinal)
            || lower.Contains("elgato", StringComparison.Ordinal)
            || lower.Contains("webcam", StringComparison.Ordinal);
    }

    public Task<BarcodeScannerPermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(BarcodeScannerPermissionStatus.Granted);

    public async Task StartAsync(
        BarcodeCameraPreview preview,
        string cameraId,
        Action<BarcodeScanResult> onDetected,
        Action<string> onStatus,
        CancellationToken cancellationToken = default)
    {
        await StopAsync();
        if (preview.Handler?.PlatformView is not WinUIImage image)
        {
            throw new InvalidOperationException("La vista previa de Windows no está lista.");
        }

        _previewImage = image;
        _dispatcherQueue = image.DispatcherQueue;
        _onDetected = onDetected;
        _onStatus = onStatus;
        _resultDelivered = false;
        _stopping = false;
        Interlocked.Exchange(ref _previewFramePresented, 0);

        var groups = await MediaFrameSourceGroup.FindAllAsync();
        _activeGroup = groups.FirstOrDefault(group => group.Id == cameraId)
            ?? groups.FirstOrDefault(group => group.SourceInfos.Any(info => info.DeviceInformation?.Id == cameraId))
            ?? throw new InvalidOperationException("La cámara seleccionada se desconectó.");

        _mediaCapture = new MediaCapture();
        var settings = new MediaCaptureInitializationSettings
        {
            SourceGroup = _activeGroup,
            SharingMode = MediaCaptureSharingMode.SharedReadOnly,
            StreamingCaptureMode = StreamingCaptureMode.Video,
            MemoryPreference = MediaCaptureMemoryPreference.Cpu
        };

        try
        {
            await _mediaCapture.InitializeAsync(settings);
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception error)
        {
            throw new InvalidOperationException("La cámara está ocupada o no pudo inicializarse.", error);
        }

        var source = _mediaCapture.FrameSources.Values
            .FirstOrDefault(item => item.Info.SourceKind == MediaFrameSourceKind.Color)
            ?? throw new InvalidOperationException("La cámara no tiene un formato compatible.");

        _frameReader = await _mediaCapture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);
        _frameReader.FrameArrived += FrameReader_FrameArrived;
        var status = await _frameReader.StartAsync();
        if (status != MediaFrameReaderStartStatus.Success)
        {
            throw new InvalidOperationException(status == MediaFrameReaderStartStatus.ExclusiveControlNotAvailable
                ? "La cámara está siendo utilizada por otra aplicación."
                : "No pudimos iniciar la lectura de la cámara.");
        }

        _onStatus?.Invoke("Coloca el código dentro del recuadro.");
    }

    public async Task StopAsync()
    {
        _stopping = true;
        if (_frameReader is not null)
        {
            _frameReader.FrameArrived -= FrameReader_FrameArrived;
            try
            {
                await _frameReader.StopAsync();
            }
            catch
            {
            }

            _frameReader.Dispose();
            _frameReader = null;
        }

        _mediaCapture?.Dispose();
        _mediaCapture = null;
        _activeGroup = null;
        if (_previewImage is not null)
        {
            _dispatcherQueue?.TryEnqueue(() => _previewImage.Source = null);
        }
    }

    private void FrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (_stopping || _resultDelivered)
        {
            sender.TryAcquireLatestFrame()?.Dispose();
            return;
        }

        using var frame = sender.TryAcquireLatestFrame();
        var bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
        if (bitmap is null)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now - Interlocked.Read(ref _lastPreviewTicks) > 100)
        {
            Interlocked.Exchange(ref _lastPreviewTicks, now);
            QueuePreviewUpdate(bitmap);
        }

        if (now - Interlocked.Read(ref _lastDecodeTicks) < 160 || !_decodeLock.Wait(0))
        {
            return;
        }

        Interlocked.Exchange(ref _lastDecodeTicks, now);
        SoftwareBitmap decodeBitmap;
        try
        {
            decodeBitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }
        catch
        {
            _decodeLock.Release();
            return;
        }

        _ = Task.Run(() =>
        {
            using (decodeBitmap)
            try
            {
                var bytes = CopyBgraBytes(decodeBitmap);
                var result = _decoder.DecodeBgra32(bytes, decodeBitmap.PixelWidth, decodeBitmap.PixelHeight, "WindowsCamera");
                if (result.Success && !_resultDelivered)
                {
                    _resultDelivered = true;
                    _onDetected?.Invoke(result);
                }
            }
            catch
            {
                _onStatus?.Invoke("No pudimos leer el código. Prueba acercando o alejando la cámara.");
            }
            finally
            {
                _decodeLock.Release();
            }
        });
    }

    private void QueuePreviewUpdate(SoftwareBitmap bitmap)
    {
        if (_dispatcherQueue is null || _previewImage is null || Interlocked.Exchange(ref _previewUpdateQueued, 1) == 1)
        {
            return;
        }

        SoftwareBitmap previewBitmap;
        try
        {
            previewBitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }
        catch
        {
            Interlocked.Exchange(ref _previewUpdateQueued, 0);
            return;
        }

        if (!_dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var source = new SoftwareBitmapSource();
                await source.SetBitmapAsync(previewBitmap);
                if (!_stopping && _previewImage is not null)
                {
                    _previewImage.Source = source;
                    if (Interlocked.Exchange(ref _previewFramePresented, 1) == 0)
                    {
                        _onStatus?.Invoke("Vista previa activa. Coloca el código dentro del recuadro.");
                    }
                }
            }
            catch
            {
                _onStatus?.Invoke("La cámara está activa, pero no pudimos mostrar la vista previa.");
            }
            finally
            {
                previewBitmap.Dispose();
                Interlocked.Exchange(ref _previewUpdateQueued, 0);
            }
        }))
        {
            previewBitmap.Dispose();
            Interlocked.Exchange(ref _previewUpdateQueued, 0);
        }
    }

    private static byte[] CopyBgraBytes(SoftwareBitmap bitmap)
    {
        var bytes = new byte[checked(bitmap.PixelWidth * bitmap.PixelHeight * 4)];
        var buffer = new global::Windows.Storage.Streams.Buffer((uint)bytes.Length);
        bitmap.CopyToBuffer(buffer);
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(bytes);
        return bytes;
    }
}
