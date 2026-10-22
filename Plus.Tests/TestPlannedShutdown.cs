using Plus.Core;

namespace Plus.Tests;

internal sealed class TestPlannedShutdown(bool isPending) : IPlannedShutdown
{
    internal static TestPlannedShutdown Pending { get; } = new(true);
    internal static TestPlannedShutdown None { get; } = new(false);

    public bool IsPending => isPending;

    public bool TrySchedule(int minutes, int durationMinutes) => throw new NotSupportedException();
}
