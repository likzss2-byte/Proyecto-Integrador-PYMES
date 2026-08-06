using InventorySystem.Controls;
using InventorySystem.Domain;

namespace InventorySystem.Services;

public interface IBarcodeCameraScannerService
{
    Task<BarcodeScannerAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeCameraDevice>> GetAvailableCamerasAsync(CancellationToken cancellationToken = default);

    Task<BarcodeScannerPermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default);

    Task StartAsync(
        BarcodeCameraPreview preview,
        string cameraId,
        Action<BarcodeScanResult> onDetected,
        Action<string> onStatus,
        CancellationToken cancellationToken = default);

    Task StopAsync();
}
