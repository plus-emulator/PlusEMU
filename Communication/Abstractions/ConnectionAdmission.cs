using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Plus.Communication.Abstractions;

/// <summary>
/// Finds the visitor address of a game connection and limits the open connections per address.
/// </summary>
public sealed class ConnectionAdmission
{
    private const int MaxRefusalLogEntries = 4096;
    private static readonly TimeSpan RefusalLogInterval = TimeSpan.FromMinutes(1);
    private static readonly IPAddress[] LoopbackProxies = [IPAddress.Loopback, IPAddress.IPv6Loopback];

    private readonly HashSet<IPAddress> _trustedProxies;
    private readonly int _maxConnectionsPerAddress;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private readonly Dictionary<IPAddress, int> _connections = new();
    private readonly Dictionary<IPAddress, DateTimeOffset> _refusalsLogged = new();

    public ConnectionAdmission(IWebsocketServerOptions options, TimeProvider clock, ILogger logger)
    {
        _clock = clock;
        _logger = logger;
        _maxConnectionsPerAddress = options.MaxConnectionsPerIp;
        _trustedProxies = new(options.TrustedProxies == null ? LoopbackProxies : ParseTrustedProxies(options.TrustedProxies));
    }

    /// <summary>
    /// A trusted proxy names its visitor in X-Real-IP, or appends it as the last X-Forwarded-For entry. Earlier entries
    /// come from the client and are never used. Headers from any other peer are ignored.
    /// </summary>
    public ClientAddress Resolve(IPAddress peer, IEnumerable<(string Name, string Value)> headers)
    {
        peer = Normalize(peer);

        if (!_trustedProxies.Contains(peer)) {
            return new(peer, true);
        }

        string? realIp = null;
        string? forwardedFor = null;

        foreach (var (name, value) in headers) {
            if (name.Equals("X-Real-IP", StringComparison.OrdinalIgnoreCase)) {
                realIp = value;
            }
            else if (name.Equals("X-Forwarded-For", StringComparison.OrdinalIgnoreCase)) {
                forwardedFor = value;
            }
        }

        var visitor = ParseVisitor(realIp) ?? ParseVisitor(forwardedFor?[(forwardedFor.LastIndexOf(',') + 1)..]);

        return visitor == null ? new(peer, false) : new(visitor, true);
    }

    /// <summary>A lease to dispose when the connection closes, or null when the address already has its limit.</summary>
    public IDisposable? TryAdmit(ClientAddress address)
    {
        if (!address.IsVisitor || _maxConnectionsPerAddress <= 0) {
            return NoLease.Instance;
        }

        lock (_lock) {
            var open = _connections.GetValueOrDefault(address.Address);

            if (open < _maxConnectionsPerAddress) {
                _connections[address.Address] = open + 1;

                return new Lease(this, address.Address);
            }

            if (!ShouldLogRefusal(address.Address)) {
                return null;
            }
        }

        _logger.LogWarning("Refused a game connection from {Address}: it already has {Limit} open connections", address.Address, _maxConnectionsPerAddress);

        return null;
    }

    private bool ShouldLogRefusal(IPAddress address)
    {
        var now = _clock.GetUtcNow();

        if (_refusalsLogged.TryGetValue(address, out var logged) && now - logged < RefusalLogInterval) {
            return false;
        }

        if (_refusalsLogged.Count >= MaxRefusalLogEntries) {
            foreach (var stale in _refusalsLogged.Where(entry => now - entry.Value >= RefusalLogInterval).Select(entry => entry.Key).ToList()) {
                _refusalsLogged.Remove(stale);
            }

            if (_refusalsLogged.Count >= MaxRefusalLogEntries) {
                _refusalsLogged.Clear();
            }
        }

        _refusalsLogged[address] = now;

        return true;
    }

    private void Release(IPAddress address)
    {
        lock (_lock) {
            var open = _connections.GetValueOrDefault(address) - 1;

            if (open > 0) {
                _connections[address] = open;
            }
            else {
                _connections.Remove(address);
            }
        }
    }

    private IEnumerable<IPAddress> ParseTrustedProxies(IEnumerable<string> configured)
    {
        foreach (var entry in configured) {
            if (IPAddress.TryParse(entry.Trim(), out var address)) {
                yield return Normalize(address);
            }
            else {
                _logger.LogError("Ignored trusted proxy {Entry}: it is not an IP address", entry);
            }
        }
    }

    /// <summary>
    /// Accepts an address with an optional port, as "203.0.113.7:80" or "[2001:db8::5]:443". A loopback or unspecified
    /// address names another local hop, not a visitor.
    /// </summary>
    private static IPAddress? ParseVisitor(string? value)
    {
        var text = value.AsSpan().Trim();

        if (text.StartsWith("[")) {
            var end = text.IndexOf(']');

            if (end < 0) {
                return null;
            }

            text = text[1..end];
        }
        else if (text.Count(':') == 1) {
            text = text[..text.IndexOf(':')];
        }

        if (!IPAddress.TryParse(text, out var address)) {
            return null;
        }

        // IPAddress also parses shorthands such as "1234" or "127.1", which no proxy sends.
        if (address.AddressFamily == AddressFamily.InterNetwork && text.Count('.') != 3) {
            return null;
        }

        address = Normalize(address);

        return IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ? null : address;
    }

    private static IPAddress Normalize(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) {
            return address.MapToIPv4();
        }

        // A scope id would make one address count as many.
        return address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0 ? new IPAddress(address.GetAddressBytes()) : address;
    }

    private sealed class Lease(ConnectionAdmission admission, IPAddress address) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) {
                admission.Release(address);
            }
        }
    }

    private sealed class NoLease : IDisposable
    {
        public static NoLease Instance { get; } = new();

        public void Dispose() { }
    }
}
