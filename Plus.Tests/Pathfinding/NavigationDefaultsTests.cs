using Plus.Core.Settings;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using System.Runtime.CompilerServices;
using Xunit;

namespace Plus.Tests.Pathfinding;

public class NavigationDefaultsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingConfigurationKeepsBothFloorAndBridgeRoutes(bool loadSettings)
    {
        var settings = loadSettings ? PathfindingSettings.Load(TestRoomSettings.Empty) : new PathfindingSettings();
        var (grid, inputs, compiler) = NavTest.Create(3, 1, settings);
        inputs.Publish(NavTest.Record(10, 1, [1], z: 2));
        compiler.ApplyNow();

        Assert.True(grid.SlotOf(new SurfaceRef(1, 0, SurfaceKind.Floor)) >= 0);
        Assert.True(grid.SlotOf(new SurfaceRef(1, 10, SurfaceKind.Top)) >= 0);
        var route = new Route();
        using var lease = PathWorkspacePool.Rent(grid.SlotCapacity, grid.ActiveNodeCount);
        Assert.Equal(PathOutcome.Found, new PathSearch(grid, settings).Find(
            new(new(), new(0, 0, 0), 2, 0), lease.Workspace, route));
        Assert.Equal(new SurfaceRef(1, 0, SurfaceKind.Floor), route.Steps[0]);
    }

    [Fact]
    public void RoomWithoutEngineSettingsQueuesMovementForItsRoomOwner()
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        var map = new Gamemap(room, new RoomModel("default", 0, 0, 0, 0, "000\r000", 0, 0, false),
            TestLogging.Navigation, TestRoomSettings.Empty, TestGroupManager.Empty,
            TestNavigationDatabase.Instance, TestNavigationRewards.Instance);
        typeof(Room).GetField("_gamemap", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(room, map);
        var actor = new RoomUser(1, 1, 1, room, null, TestChatEmotions.Unused, TestNavigationRewards.Instance);
        actor.InitializePosition(0, 1, 0);
        var before = (actor.GoalX, actor.GoalY, actor.PathRecalcNeeded);

        actor.MoveTo(2, 1);

        Assert.NotNull(map.Navigation);
        Assert.Equal(before, (actor.GoalX, actor.GoalY, actor.PathRecalcNeeded));
        Assert.NotNull(actor.Movement.Commands.Read());
    }
}
