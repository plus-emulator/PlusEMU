using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.Communication.Packets.Outgoing.Notifications;
using Plus.HabboHotel.GameClients;
using Plus.Utilities.DependencyInjection;

namespace Plus.Core;

[Singleton]
public interface IPlannedShutdown
{
    bool IsPending { get; }

    bool TrySchedule(int minutes, int durationMinutes);
}

/// <summary>
/// Warns every user with Habbo's maintenance alert (text maintenance.shutdown), again when one minute is left, and then shuts the server down.
/// </summary>
public sealed class PlannedShutdown : IPlannedShutdown
{
    // The client reads 15 when the packet has no duration.
    public const int DefaultDurationMinutes = 15;
    public const int MaxMinutes = 24 * 60;

    private readonly IGameClientManager _clients;
    private readonly TimeProvider _clock;
    private readonly Action _shutdown;
    private readonly List<ITimer> _timers = [];
    private int _scheduled;

    public PlannedShutdown(IGameClientManager clients, TimeProvider clock) : this(clients, clock, PlusEnvironment.PerformShutDown) { }

    internal PlannedShutdown(IGameClientManager clients, TimeProvider clock, Action shutdown)
    {
        _clients = clients;
        _clock = clock;
        _shutdown = shutdown;
    }

    public bool IsPending => Volatile.Read(ref _scheduled) == 1;

    public static bool TryParse(IReadOnlyList<string> arguments, out int minutes, out int durationMinutes)
    {
        durationMinutes = DefaultDurationMinutes;

        if (arguments.Count == 0 || !int.TryParse(arguments[0], out minutes)) {
            minutes = 0;

            return false;
        }

        return arguments.Count < 2 || string.IsNullOrEmpty(arguments[1]) || int.TryParse(arguments[1], out durationMinutes);
    }

    public bool TrySchedule(int minutes, int durationMinutes)
    {
        if (minutes is < 1 or > MaxMinutes || durationMinutes < 0 || Interlocked.Exchange(ref _scheduled, 1) == 1) {
            return false;
        }

        // The client hides the trade button while onShutDown is set.
        _clients.SendPacket(new AvailabilityStatusComposer(true));
        Warn(minutes, durationMinutes);

        if (minutes > 1) {
            Start(TimeSpan.FromMinutes(minutes - 1), () => Warn(1, durationMinutes));
        }

        Start(TimeSpan.FromMinutes(minutes), _shutdown);

        return true;
    }

    private void Warn(int minutes, int durationMinutes) => _clients.SendPacket(new MaintenanceStatusComposer(true, minutes, durationMinutes));

    private void Start(TimeSpan due, Action action) => _timers.Add(_clock.CreateTimer(_ => action(), null, due, Timeout.InfiniteTimeSpan));
}
