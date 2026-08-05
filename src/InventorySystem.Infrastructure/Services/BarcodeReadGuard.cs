using System.Collections.Concurrent;
using InventorySystem.Domain;

namespace InventorySystem.Infrastructure.Services;

public sealed class BarcodeReadGuard
{
    private readonly ConcurrentDictionary<string, AcceptedRead> _lastReads = new(StringComparer.Ordinal);
    private readonly TimeSpan _duplicateWindow;
    private readonly TimeProvider _timeProvider;

    public BarcodeReadGuard(TimeSpan? duplicateWindow = null, TimeProvider? timeProvider = null)
    {
        _duplicateWindow = duplicateWindow ?? TimeSpan.FromSeconds(1.5);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool TryAccept(string context, string? rawCode, out string normalizedCode)
    {
        normalizedCode = InventoryRules.NormalizeScannedCode(rawCode);
        if (string.IsNullOrWhiteSpace(context) || normalizedCode.Length == 0)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        while (true)
        {
            if (!_lastReads.TryGetValue(context, out var previous))
            {
                if (_lastReads.TryAdd(context, new AcceptedRead(normalizedCode, now)))
                {
                    return true;
                }

                continue;
            }

            if (string.Equals(previous.Code, normalizedCode, StringComparison.OrdinalIgnoreCase) &&
                now - previous.AcceptedAt < _duplicateWindow)
            {
                return false;
            }

            if (_lastReads.TryUpdate(context, new AcceptedRead(normalizedCode, now), previous))
            {
                return true;
            }
        }
    }

    public void Reset(string context) => _lastReads.TryRemove(context, out _);

    private sealed record AcceptedRead(string Code, DateTimeOffset AcceptedAt);
}
