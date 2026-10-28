using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // After move_rotate_user moves the owner, the status updates inside the animation have status "/flatctrl 4/wf//". The first update after the
    // animation has the status without "wf".
    [Fact]
    public void WiredUserMoveShowsTheWfStatusThroughItsAnimation()
    {
        var (f, rider, _) = RotateUserRoom(-1, 2);
        var users = f.MovementContext().Room.GetRoomUserManager();

        users.MarkWiredMove(rider, 500);
        Assert.True(rider.HasStatus("wf"));
        Assert.True(rider.UpdateNeeded);

        users.SerializeStatusUpdates();
        Assert.True(rider.HasStatus("wf"));
        Assert.False(rider.UpdateNeeded);

        users.SerializeStatusUpdates();
        Assert.True(rider.HasStatus("wf"));
        Assert.False(rider.UpdateNeeded);

        rider.WiredStatusUntil = DateTimeOffset.UtcNow.AddSeconds(-1);
        users.SerializeStatusUpdates();
        Assert.False(rider.HasStatus("wf"));
    }

    [Fact]
    public void AWiredMoveWithoutAnimationLeavesNoWfStatus()
    {
        var (f, rider, _) = RotateUserRoom(-1, 2);
        var users = f.MovementContext().Room.GetRoomUserManager();

        users.MarkWiredMove(rider, 0);

        Assert.False(rider.HasStatus("wf"));
    }
}
