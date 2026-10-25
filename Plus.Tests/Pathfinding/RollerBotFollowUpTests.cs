using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// After a roller slide a bot gets the same two follow-up updates as a user.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public void ARollerSlidBotGetsUpdatesOneAndTwoTicksAfterTheSlide()
    {
        ExecutorRoller(10, 0, 1);
        ExecutorActor(3, 3);
        var bot = StepOrderBot(0, 1, 0.5);
        EnableExecutorRollers();

        Assert.Equal([false, true, true, false], RollerFollowUpUpdates(bot));
    }

    [Fact]
    public void ARollerSlidUserStillGetsUpdatesOneAndTwoTicksAfterTheSlide()
    {
        ExecutorRoller(10, 0, 1);
        var user = ExecutorRollerActor(0, 1, 0.5);
        EnableExecutorRollers();

        Assert.Equal([false, true, true, false], RollerFollowUpUpdates(user));
    }

    private bool[] RollerFollowUpUpdates(RoomUser actor)
    {
        var updates = new List<bool>();

        for (var i = 0; i < 4; i++) {
            ExecutorTick();
            updates.Add(ExecutorHasUpdate(actor));
        }

        return updates.ToArray();
    }
}
