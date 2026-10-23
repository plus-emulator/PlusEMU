using System.Collections.Concurrent;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// User Performs Action fires Sit or Lay in the same tick as the step onto a sit or lay tile, and Stand in the same tick as the first step off it.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(false, WiredAvatarAction.Sit)]
    [InlineData(true, WiredAvatarAction.Lay)]
    public void WalkingOntoASeatOrABedFiresItsActionOnceAndWalkingOffFiresStandOnce(bool bed, WiredAvatarAction arrival)
    {
        Add(10, 2, 1, type: bed ? InteractionType.Bed : InteractionType.None, seat: !bed);
        var arrived = PostureCounter(900, 3, 3, arrival);
        var stood = PostureCounter(902, 3, 2, WiredAvatarAction.Stand);
        var actor = ExecutorActor(1, 1);
        actor.MoveTo(2, 1);
        WalkTicks();
        Assert.True(actor.HasStatus(bed ? "lay" : "sit"));
        Assert.Equal(1, arrived.Calls);
        Assert.Equal(0, stood.Calls);

        actor.MoveTo(1, 1);
        WalkTicks();
        Assert.Equal((1, 1), (actor.X, actor.Y));
        Assert.Equal(1, arrived.Calls);
        Assert.Equal(1, stood.Calls);
    }

    private void WalkTicks()
    {
        for (var tick = 0; tick < 10; tick++) {
            ExecutorTick();
            _room.GetWired().OnFastCycle();
        }
    }

    private LandingActionCounter PostureCounter(uint triggerId, int x, int y, WiredAvatarAction action)
    {
        var triggerItem = Add(triggerId, x, y, type: InteractionType.WiredTrigger);
        triggerItem.Definition.InteractionName = "wf_trg_user_performs_action";
        var wired = _room.GetWired();
        var trigger = wired.CreateConfiguredBox(triggerItem)!;
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(trigger, new() { IntParams = [(int)action, 0, 0, 0, 1] }, out var config, out var error), error);
        trigger.ApplyConfiguration(config);
        Assert.True(wired.AddBox(trigger));
        var item = Add(triggerId + 1, x, y, type: InteractionType.WiredEffect);
        var counter = new LandingActionCounter(_room, item);
        Assert.True(wired.AddBox(counter));

        return counter;
    }
}
