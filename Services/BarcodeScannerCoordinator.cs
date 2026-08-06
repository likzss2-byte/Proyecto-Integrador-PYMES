using InventorySystem.Domain;

namespace InventorySystem.Services;

public sealed class BarcodeScannerCoordinator
{
    private readonly IServiceProvider _services;
    private readonly SemaphoreSlim _navigationLock = new(1, 1);
    private string? _lastCameraId;

    public BarcodeScannerCoordinator(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<BarcodeScanResult> ScanAsync(
        string context,
        string title,
        CancellationToken cancellationToken = default)
    {
        await _navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = new BarcodeScannerSession(context, title)
            {
                PreferredCameraId = _lastCameraId
            };
            var page = _services.GetRequiredService<AppPages.BarcodeScannerPage>();
            page.Configure(session);

            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(page));

            await using var registration = cancellationToken.Register(() => session.TryCancel());
            var result = await session.Completion.ConfigureAwait(false);
            _lastCameraId = session.PreferredCameraId;
            return result;
        }
        finally
        {
            _navigationLock.Release();
        }
    }
}
