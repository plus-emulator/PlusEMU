using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plus.Communication.Abstractions;

namespace Plus.HabboHotel.Users.Authentication;

/// <summary>Failed game logins allowed per visitor address within the window before it is blocked. 0 turns a value off.</summary>
public class LoginThrottleConfiguration
{
    public int MaxFailuresPerIp { get; set; } = 10;
    public int WindowMinutes { get; set; } = 5;
    public int BlockMinutes { get; set; } = 10;
}

/// <summary>Counts failed game logins per visitor address and blocks an address that fails too often.</summary>
public interface ILoginThrottle
{
    bool IsBlocked(ClientAddress? address);

    void RecordFailure(ClientAddress? address);
}

internal sealed class LoginThrottle : ILoginThrottle
{
    private readonly int _maxFailures;
    private readonly TimeSpan _window;
    private readonly TimeSpan _block;
    private readonly int _capacity;
    private readonly TimeProvider _clock;
    private readonly ILogger<LoginThrottle> _logger;
    private readonly object _lock = new();
    private readonly Dictionary<IPAddress, Entry> _entries = new();

    public LoginThrottle(IOptions<LoginThrottleConfiguration> options, TimeProvider clock, ILogger<LoginThrottle> logger) : this(options, clock, logger, 10_000) { }

    internal LoginThrottle(IOptions<LoginThrottleConfiguration> options, TimeProvider clock, ILogger<LoginThrottle> logger, int capacity)
    {
        _maxFailures = options.Value.MaxFailuresPerIp;
        _window = TimeSpan.FromMinutes(options.Value.WindowMinutes);
        _block = TimeSpan.FromMinutes(options.Value.BlockMinutes);
        _capacity = capacity;
        _clock = clock;
        _logger = logger;
    }

    internal int TrackedAddresses
    {
        get
        {
            lock (_lock) {
                return _entries.Count;
            }
        }
    }

    private bool Enabled => _maxFailures > 0 && _window > TimeSpan.Zero && _block > TimeSpan.Zero;

    public bool IsBlocked(ClientAddress? address)
    {
        if (!Enabled || address is not { IsVisitor: true } visitor) {
            return false;
        }

        lock (_lock) {
            return _entries.TryGetValue(visitor.Address, out var entry) && entry.BlockedUntil > _clock.GetUtcNow();
        }
    }

    public void RecordFailure(ClientAddress? address)
    {
        if (!Enabled || address is not { IsVisitor: true } visitor) {
            return;
        }

        var now = _clock.GetUtcNow();

        lock (_lock) {
            if (!_entries.TryGetValue(visitor.Address, out var entry)) {
                if (_entries.Count >= _capacity) {
                    RemoveExpired(now);
                }

                // A table full of live entries leaves new addresses uncounted rather than growing without bound.
                if (_entries.Count >= _capacity) {
                    return;
                }

                entry = new();
                _entries[visitor.Address] = entry;
            }

            if (entry.BlockedUntil > now) {
                return;
            }

            entry.Failures.Enqueue(now);

            while (entry.Failures.Peek() <= now - _window) {
                entry.Failures.Dequeue();
            }

            if (entry.Failures.Count < _maxFailures) {
                return;
            }

            entry.Failures.Clear();
            entry.BlockedUntil = now + _block;
        }

        _logger.LogWarning("Blocked game logins from {Address} for {BlockMinutes} minutes after {Failures} failed logins", visitor.Address, _block.TotalMinutes, _maxFailures);
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var expired in _entries.Where(pair => pair.Value.BlockedUntil <= now && (pair.Value.Failures.Count == 0 || pair.Value.Failures.Last() <= now - _window))
                     .Select(pair => pair.Key).ToList()) {
            _entries.Remove(expired);
        }
    }

    private sealed class Entry
    {
        public Queue<DateTimeOffset> Failures { get; } = new();
        public DateTimeOffset BlockedUntil { get; set; } = DateTimeOffset.MinValue;
    }
}
