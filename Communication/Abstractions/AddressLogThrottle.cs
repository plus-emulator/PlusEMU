using System.Net;

namespace Plus.Communication.Abstractions;

/// <summary>Allows one log line per address per minute, so one address cannot flood the log.</summary>
internal sealed class AddressLogThrottle(TimeProvider clock)
{
    private const int MaxEntries = 4096;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly Dictionary<IPAddress, DateTimeOffset> _logged = new();

    public bool ShouldLog(IPAddress address)
    {
        lock (_logged) {
            var now = clock.GetUtcNow();

            if (_logged.TryGetValue(address, out var logged) && now - logged < Interval) {
                return false;
            }

            if (_logged.Count >= MaxEntries) {
                foreach (var stale in _logged.Where(entry => now - entry.Value >= Interval).Select(entry => entry.Key).ToList()) {
                    _logged.Remove(stale);
                }

                if (_logged.Count >= MaxEntries) {
                    _logged.Clear();
                }
            }

            _logged[address] = now;

            return true;
        }
    }
}
