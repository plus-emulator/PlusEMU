using System.Reflection;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.Tests.Performance;
using Xunit;

namespace Plus.Tests.Pathfinding;

[Collection("Pathfinding room adapter")]
public class MovementIntakeTests
{
    [Fact]
    public void MoveToDefersGoalWritesOnlyForTheV2Executor()
    {
        var actor = CreateActor();
        var before = (actor.GoalX, actor.GoalY, actor.PathRecalcNeeded);
        actor.MoveTo(3, 2);
        Assert.Equal(before,
            (actor.GoalX, actor.GoalY, actor.PathRecalcNeeded));
    }

    [Fact]
    public void SetPosDefersPhysicalWritesOnlyForTheV2Executor()
    {
        var actor = CreateActor();
        var before = (actor.X, actor.Y, actor.Z);
        actor.SetPos(2, 2, 1.5004);
        Assert.Equal(before,
            (actor.X, actor.Y, actor.Z));
    }

    [Fact]
    public void ClearMovementDefersCancellationOnlyForTheV2Executor()
    {
        var actor = CreateActor();
        actor.IsWalking = true;
        actor.SetStatus("mv", "2,1,0");
        actor.ClearMovement(true);
        Assert.Equal(true, actor.IsWalking);
        Assert.Equal(true, actor.Statusses.ContainsKey("mv"));
    }

    private static RoomUser CreateActor()
    {
        var fixture = RoomPerformanceFixture.Create(0, 1);
        var actor = fixture.Users[0];
        actor.ClearMovement(true);
        RoomPerformanceFixture.SetField(fixture.Room, "_roomItemHandling", new RoomItemHandling(fixture.Room, TestRoomItemStore.Instance, TestRoomItemMetadataStore.Instance, TestGameClientManager.Empty, TestLanguageManager.RoomItems, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards));

        var navigation = new RoomNavigation(fixture.Room, fixture.Map.StaticModel, new() { }, TestLogging.Navigation, TestGroupManager.Empty, TestNavigationDatabase.Instance, TestNavigationRewards.Instance);
        typeof(Gamemap).GetField("<Navigation>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fixture.Map, navigation);

        return actor;
    }
}
