using InventorySystem.Domain;

namespace InventorySystem.Services;

public sealed record BarcodeCameraDevice(
    string Id,
    string DisplayName,
    BarcodeCameraDeviceKind Kind,
    bool IsExternal)
{
    public string Description => IsExternal ? "Cámara externa" : "Cámara integrada o del dispositivo";

    public string SelectorLabel => Kind switch
    {
        BarcodeCameraDeviceKind.Front => $"{DisplayName} (frontal)",
        BarcodeCameraDeviceKind.Back => $"{DisplayName} (trasera)",
        BarcodeCameraDeviceKind.External => $"{DisplayName} (externa o USB)",
        BarcodeCameraDeviceKind.Integrated => $"{DisplayName} (integrada)",
        _ => DisplayName
    };
}

public enum BarcodeCameraDeviceKind
{
    Unknown,
    Front,
    Back,
    Integrated,
    External
}

public enum BarcodeScannerAvailability
{
    Available,
    NoCamera,
    PermissionDenied,
    UnsupportedPlatform,
    Busy,
    Error
}

public enum BarcodeScannerState
{
    Idle,
    RequestingPermission,
    EnumeratingCameras,
    Previewing,
    Detecting,
    Processing,
    Stopped,
    Error
}

public enum BarcodeScannerPermissionStatus
{
    Unknown,
    Granted,
    Denied,
    Disabled,
    Restricted
}

public sealed class BarcodeScannerSession
{
    private readonly TaskCompletionSource<BarcodeScanResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public BarcodeScannerSession(string context, string title)
    {
        Context = context;
        Title = title;
    }

    public string Context { get; }

    public string Title { get; }

    public string? PreferredCameraId { get; set; }

    public Task<BarcodeScanResult> Completion => _completion.Task;

    public bool TrySetResult(BarcodeScanResult result) => _completion.TrySetResult(result);

    public bool TryCancel() => _completion.TrySetResult(BarcodeScanResult.Failed("Escaneo cancelado."));
}
