using InventorySystem.Controls;
using InventorySystem.Domain;

namespace InventorySystem.Services;

public sealed class UnsupportedBarcodeCameraScannerService : IBarcodeCameraScannerService
{
    public Task<BarcodeScannerAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(BarcodeScannerAvailability.UnsupportedPlatform);

    public Task<IReadOnlyList<BarcodeCameraDevice>> GetAvailableCamerasAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BarcodeCameraDevice>>([]);

    public Task<BarcodeScannerPermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(BarcodeScannerPermissionStatus.Disabled);

    public Task StartAsync(
        BarcodeCameraPreview preview,
        string cameraId,
        Action<BarcodeScanResult> onDetected,
        Action<string> onStatus,
        CancellationToken cancellationToken = default)
    {
        onStatus("El escaneo con cámara no está disponible en esta plataforma. Puedes escribir el código manualmente.");
        return Task.CompletedTask;
    }

    public Task StopAsync() => Task.CompletedTask;
}
