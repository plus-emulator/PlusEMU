using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void AWiredMoveFromSeatToSeatDoesNotFireSitAgain()
    {
        Add(10, 2, 1, seat: true);
        Add(11, 1, 2, seat: true);
        var sat = PostureCounter(900, 3, 3, WiredAvatarAction.Sit);
        var actor = ExecutorActor(1, 1);
        actor.MoveTo(2, 1);
        WalkTicks();
        Assert.True(actor.HasStatus("sit"));
        Assert.Equal(1, sat.Calls);

        Assert.True(WiredRoomOperations.RelocateAvatar(_room, actor, 1, 2, false));
        WalkTicks();
        Assert.Equal((1, 2), (actor.X, actor.Y));
        Assert.True(actor.HasStatus("sit"));
        Assert.Equal(1, sat.Calls);
    }
}
