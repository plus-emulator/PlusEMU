using Plus.HabboHotel.GameClients;
using Plus.Utilities.DependencyInjection;

namespace Plus.Core;

[Singleton]
public interface IPlannedShutdown
{
    bool TrySchedule(int minutes, int durationMinutes);
}

public sealed class PlannedShutdown : IPlannedShutdown
{
    public const int DefaultDurationMinutes = 15;

    public PlannedShutdown(IGameClientManager clients, TimeProvider clock) : this(clients, clock, PlusEnvironment.PerformShutDown) { }

    internal PlannedShutdown(IGameClientManager clients, TimeProvider clock, Action shutdown) { }

    public static bool TryParse(IReadOnlyList<string> arguments, out int minutes, out int durationMinutes) => throw new NotImplementedException();

    public bool TrySchedule(int minutes, int durationMinutes) => throw new NotImplementedException();
}
