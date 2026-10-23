using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // After move_rotate_user moves the owner, the next UserUpdate has status "/flatctrl 4/wf//" and the one after it (about 500
    // ms later) has the status without "wf".
    [Fact]
    public void WiredUserMoveShowsTheWfStatusForOneStatusUpdate()
    {
        var (f, rider, _) = RotateUserRoom(-1, 2);
        var users = f.MovementContext().Room.GetRoomUserManager();

        rider.MarkWiredMove();
        Assert.True(rider.HasStatus("wf"));
        Assert.True(rider.UpdateNeeded);

        users.SerializeStatusUpdates();
        Assert.True(rider.HasStatus("wf"));
        Assert.True(rider.WiredMoveStatusSent);

        users.SerializeStatusUpdates();
        Assert.False(rider.HasStatus("wf"));
    }
}
