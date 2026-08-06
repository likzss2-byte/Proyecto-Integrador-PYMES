using Android;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Media;
using Android.OS;
using Android.Views;
using InventorySystem.Controls;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Services;
using Microsoft.Maui.ApplicationModel;
using ACameraAccessException = Android.Hardware.Camera2.CameraAccessException;
using ACameraCharacteristics = Android.Hardware.Camera2.CameraCharacteristics;
using ACameraDevice = Android.Hardware.Camera2.CameraDevice;
using ACameraManager = Android.Hardware.Camera2.CameraManager;
using ACameraMetadata = Android.Hardware.Camera2.CameraMetadata;
using ACameraStateCallback = Android.Hardware.Camera2.CameraDevice.StateCallback;
using ACameraCaptureSession = Android.Hardware.Camera2.CameraCaptureSession;
using AHandler = Android.OS.Handler;

namespace InventorySystem.Platforms.Android;

public sealed class AndroidBarcodeCameraScannerService : Java.Lang.Object, IBarcodeCameraScannerService
{
    private readonly BarcodeScannerService _decoder;
    private readonly SemaphoreSlim _decodeLock = new(1, 1);
    private readonly object _sync = new();
    private ACameraManager? _cameraManager;
    private ACameraDevice? _cameraDevice;
    private ACameraCaptureSession? _captureSession;
    private CaptureRequest.Builder? _requestBuilder;
    private ImageReader? _imageReader;
    private HandlerThread? _cameraThread;
    private AHandler? _cameraHandler;
    private Surface? _previewSurface;
    private TextureView? _textureView;
    private Action<BarcodeScanResult>? _onDetected;
    private Action<string>? _onStatus;
    private long _lastDecodeTicks;
    private bool _resultDelivered;
    private bool _stopping;

    public AndroidBarcodeCameraScannerService(BarcodeScannerService decoder)
    {
        _decoder = decoder;
    }

    public async Task<BarcodeScannerAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var permission = await RequestAccessAsync(cancellationToken);
        if (permission != BarcodeScannerPermissionStatus.Granted)
        {
            return BarcodeScannerAvailability.PermissionDenied;
        }

        var cameras = await GetAvailableCamerasAsync(cancellationToken);
        return cameras.Count == 0 ? BarcodeScannerAvailability.NoCamera : BarcodeScannerAvailability.Available;
    }

    public Task<IReadOnlyList<BarcodeCameraDevice>> GetAvailableCamerasAsync(CancellationToken cancellationToken = default)
    {
        var manager = GetCameraManager();
        var devices = new List<BarcodeCameraDevice>();
        foreach (var id in manager.GetCameraIdList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var characteristics = manager.GetCameraCharacteristics(id);
            var facingObject = characteristics.Get(ACameraCharacteristics.LensFacing);
            var facing = facingObject is Java.Lang.Integer integer ? integer.IntValue() : -1;
            var kind = facing switch
            {
                (int)LensFacing.Back => BarcodeCameraDeviceKind.Back,
                (int)LensFacing.Front => BarcodeCameraDeviceKind.Front,
                _ => BarcodeCameraDeviceKind.Unknown
            };
            var name = kind switch
            {
                BarcodeCameraDeviceKind.Back => "Cámara trasera",
                BarcodeCameraDeviceKind.Front => "Cámara frontal",
                _ => $"Cámara {id}"
            };
            devices.Add(new BarcodeCameraDevice(id, name, kind, false));
        }

        return Task.FromResult<IReadOnlyList<BarcodeCameraDevice>>(devices);
    }

    public async Task<BarcodeScannerPermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        return status switch
        {
            PermissionStatus.Granted => BarcodeScannerPermissionStatus.Granted,
            PermissionStatus.Denied => BarcodeScannerPermissionStatus.Denied,
            PermissionStatus.Restricted => BarcodeScannerPermissionStatus.Restricted,
            PermissionStatus.Disabled => BarcodeScannerPermissionStatus.Disabled,
            _ => BarcodeScannerPermissionStatus.Unknown
        };
    }

    public async Task StartAsync(
        BarcodeCameraPreview preview,
        string cameraId,
        Action<BarcodeScanResult> onDetected,
        Action<string> onStatus,
        CancellationToken cancellationToken = default)
    {
        await StopAsync();
        _onDetected = onDetected;
        _onStatus = onStatus;
        _resultDelivered = false;
        _stopping = false;

        if (preview.Handler?.PlatformView is not TextureView textureView)
        {
            throw new InvalidOperationException("La vista previa de Android no está lista.");
        }

        _textureView = textureView;
        StartCameraThread();
        await EnsureTextureAvailableAsync(textureView, cancellationToken);
        OpenCamera(cameraId, textureView);
    }

    public Task StopAsync()
    {
        lock (_sync)
        {
            _stopping = true;
            try
            {
                _captureSession?.StopRepeating();
                _captureSession?.AbortCaptures();
            }
            catch
            {
            }

            _captureSession?.Close();
            _captureSession?.Dispose();
            _captureSession = null;
            _cameraDevice?.Close();
            _cameraDevice?.Dispose();
            _cameraDevice = null;
            _imageReader?.Close();
            _imageReader?.Dispose();
            _imageReader = null;
            _previewSurface?.Release();
            _previewSurface?.Dispose();
            _previewSurface = null;
            _requestBuilder = null;
        }

        StopCameraThread();
        return Task.CompletedTask;
    }

    private ACameraManager GetCameraManager()
    {
        if (_cameraManager is not null)
        {
            return _cameraManager;
        }

        var context = Platform.CurrentActivity ?? global::Android.App.Application.Context;
        _cameraManager = (ACameraManager)context.GetSystemService(Context.CameraService)!;
        return _cameraManager;
    }

    private void StartCameraThread()
    {
        _cameraThread = new HandlerThread("InventarioPymesBarcodeCamera");
        _cameraThread.Start();
        _cameraHandler = new AHandler(_cameraThread.Looper!);
    }

    private void StopCameraThread()
    {
        _cameraThread?.QuitSafely();
        _cameraThread?.Dispose();
        _cameraThread = null;
        _cameraHandler = null;
    }

    private static Task EnsureTextureAvailableAsync(TextureView textureView, CancellationToken cancellationToken)
    {
        if (textureView.IsAvailable)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        textureView.SurfaceTextureListener = new TextureListener(completion);
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return completion.Task;
    }

    private void OpenCamera(string cameraId, TextureView textureView)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(23) &&
            Platform.CurrentActivity?.CheckSelfPermission(Manifest.Permission.Camera) != Permission.Granted)
        {
            throw new UnauthorizedAccessException("No tenemos permiso para usar la cámara.");
        }

        var texture = textureView.SurfaceTexture
            ?? throw new InvalidOperationException("La vista previa de cámara no está disponible.");
        texture.SetDefaultBufferSize(1280, 720);
        _previewSurface = new Surface(texture);
        _imageReader = ImageReader.NewInstance(1280, 720, ImageFormatType.Yuv420888, 2);
        _imageReader.SetOnImageAvailableListener(new ImageListener(this), _cameraHandler);

        GetCameraManager().OpenCamera(cameraId, new DeviceStateCallback(this), _cameraHandler);
    }

    private void OnCameraOpened(ACameraDevice camera)
    {
        lock (_sync)
        {
            if (_stopping)
            {
                camera.Close();
                return;
            }

            _cameraDevice = camera;
            _requestBuilder = camera.CreateCaptureRequest(CameraTemplate.Preview);
            _requestBuilder.AddTarget(_previewSurface!);
            _requestBuilder.AddTarget(_imageReader!.Surface!);
            #pragma warning disable CA1422
            camera.CreateCaptureSession(
                [_previewSurface!, _imageReader.Surface!],
                new CaptureSessionCallback(this),
                _cameraHandler);
            #pragma warning restore CA1422
        }
    }

    private void OnSessionConfigured(ACameraCaptureSession session)
    {
        lock (_sync)
        {
            if (_stopping || _requestBuilder is null)
            {
                session.Close();
                return;
            }

            _captureSession = session;
            var autoFocusModeKey = CaptureRequest.ControlAfMode;
            if (autoFocusModeKey is not null)
            {
                _requestBuilder.Set(autoFocusModeKey, (int)ControlAFMode.ContinuousPicture);
            }
            _captureSession.SetRepeatingRequest(_requestBuilder.Build(), null, _cameraHandler);
        }

        _onStatus?.Invoke("Coloca el código dentro del recuadro.");
    }

    private void OnImageAvailable(ImageReader reader)
    {
        if (_stopping || _resultDelivered)
        {
            reader.AcquireLatestImage()?.Close();
            return;
        }

        var now = System.Environment.TickCount64;
        if (now - Interlocked.Read(ref _lastDecodeTicks) < 160)
        {
            reader.AcquireLatestImage()?.Close();
            return;
        }

        if (!_decodeLock.Wait(0))
        {
            reader.AcquireLatestImage()?.Close();
            return;
        }

        Interlocked.Exchange(ref _lastDecodeTicks, now);
        using var image = reader.AcquireLatestImage();
        if (image is null)
        {
            _decodeLock.Release();
            return;
        }

        try
        {
            var planes = image.GetPlanes();
            var plane = planes is { Length: > 0 } ? planes[0] : null;
            if (plane is null || plane.Buffer is null)
            {
                return;
            }

            var bytes = CopyLuminancePlane(plane, image.Width, image.Height);
            var result = _decoder.DecodeLuminance(bytes, image.Width, image.Height, "AndroidCamera");
            if (result.Success && !_resultDelivered)
            {
                _resultDelivered = true;
                _onDetected?.Invoke(result);
            }
        }
        finally
        {
            _decodeLock.Release();
        }
    }

    private static byte[] CopyLuminancePlane(global::Android.Media.Image.Plane plane, int width, int height)
    {
        var buffer = plane.Buffer;
        var rowStride = plane.RowStride;
        var pixelStride = plane.PixelStride;
        var luminance = new byte[width * height];
        if (buffer is null || rowStride <= 0 || pixelStride <= 0)
        {
            return luminance;
        }

        if (pixelStride == 1 && rowStride == width)
        {
            buffer.Get(luminance, 0, Math.Min(luminance.Length, buffer.Remaining()));
            return luminance;
        }

        var row = new byte[rowStride];
        for (var y = 0; y < height; y++)
        {
            var bytesToRead = Math.Min(rowStride, buffer.Remaining());
            if (bytesToRead <= 0)
            {
                break;
            }

            buffer.Get(row, 0, bytesToRead);
            for (var x = 0; x < width; x++)
            {
                var sourceOffset = x * pixelStride;
                if (sourceOffset < bytesToRead)
                {
                    luminance[(y * width) + x] = row[sourceOffset];
                }
            }
        }

        return luminance;
    }

    private sealed class TextureListener : Java.Lang.Object, TextureView.ISurfaceTextureListener
    {
        private readonly TaskCompletionSource _completion;

        public TextureListener(TaskCompletionSource completion)
        {
            _completion = completion;
        }

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height) =>
            _completion.TrySetResult();

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface) => true;

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
        }
    }

    private sealed class DeviceStateCallback : ACameraStateCallback
    {
        private readonly AndroidBarcodeCameraScannerService _owner;

        public DeviceStateCallback(AndroidBarcodeCameraScannerService owner)
        {
            _owner = owner;
        }

        public override void OnOpened(ACameraDevice camera) => _owner.OnCameraOpened(camera);

        public override void OnDisconnected(ACameraDevice camera)
        {
            _owner._onStatus?.Invoke("La cámara seleccionada se desconectó.");
            camera.Close();
        }

        public override void OnError(ACameraDevice camera, CameraError error)
        {
            _owner._onStatus?.Invoke("No pudimos iniciar la cámara. Puedes escribir el código manualmente.");
            camera.Close();
        }
    }

    private sealed class CaptureSessionCallback : ACameraCaptureSession.StateCallback
    {
        private readonly AndroidBarcodeCameraScannerService _owner;

        public CaptureSessionCallback(AndroidBarcodeCameraScannerService owner)
        {
            _owner = owner;
        }

        public override void OnConfigured(ACameraCaptureSession session) => _owner.OnSessionConfigured(session);

        public override void OnConfigureFailed(ACameraCaptureSession session) =>
            _owner._onStatus?.Invoke("La cámara no tiene un formato compatible.");
    }

    private sealed class ImageListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        private readonly AndroidBarcodeCameraScannerService _owner;

        public ImageListener(AndroidBarcodeCameraScannerService owner)
        {
            _owner = owner;
        }

        public void OnImageAvailable(ImageReader? reader)
        {
            if (reader is not null)
            {
                _owner.OnImageAvailable(reader);
            }
        }
    }
}
