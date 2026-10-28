using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private Task ClickItem(Item item) => UseEvent().Parse(_room, _client, ClientPacket((int)item.Id, 0));

    private void OpenLargeRoom()
    {
        Set("_gamemap", new Gamemap(_room, new RoomModel("wide", 0, 11, 0, 0, string.Join('\r', Enumerable.Repeat("000000000000", 12)), 0, 0, false),
            TestLogging.Navigation, _roomSettings, TestGroupManager.Empty, TestNavigationDatabase.Instance, TestNavigationRewards.Instance));
        _room.GetGameMap().GenerateMaps();
    }

    private Item FloorSwitchFixture(int x = 0, int y = 0)
    {
        var item = InteractionItem(10, x, y, InteractionType.WfFloorSwitch1);
        item.Definition.Modes = 2;
        item.LegacyDataString = "0";

        return item;
    }

    // A click from a distance walks the user to the nearest tile touching the switch and switches it once the user arrives.
    [Fact]
    public async Task DistantClickOnAFloorSwitchSwitchesItOnArrival()
    {
        var item = FloorSwitchFixture();
        var actor = ApproachActor(3, 3);

        await ClickItem(item);
        Assert.Equal("0", item.LegacyDataString);
        WalkToLanding(actor, 5);

        Assert.True(Gamemap.TilesTouching(item.GetX, item.GetY, actor.X, actor.Y));
        Assert.Equal("1", item.LegacyDataString);
        Assert.Null(Approaches.Peek(actor));
    }

    // Clicks made while the user is still walking do not add up: the last one is the use on arrival.
    [Fact]
    public async Task ClicksWhileWalkingToAFloorSwitchSwitchItOnce()
    {
        var item = FloorSwitchFixture();
        var actor = ApproachActor(3, 3);

        await ClickItem(item);
        ExecutorTick();
        await ClickItem(item);
        WalkToLanding(actor, 6);

        Assert.True(Gamemap.TilesTouching(item.GetX, item.GetY, actor.X, actor.Y));
        Assert.Equal("1", item.LegacyDataString);
    }

    // A click from next to the switch switches it at once, also on the diagonal.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    public async Task ClickNextToAFloorSwitchSwitchesItAtOnce(int x, int y)
    {
        var item = FloorSwitchFixture();
        ApproachActor(x, y);

        await ClickItem(item);
        Assert.Equal("1", item.LegacyDataString);
        await ClickItem(item);
        Assert.Equal("0", item.LegacyDataString);
    }

    // The Wired state changes trigger fires once, when the switch is used on arrival, and not at the distant click.
    [Fact]
    public async Task FloorSwitchUsedOnArrivalRaisesTheStateChangeOnce()
    {
        var item = FloorSwitchFixture();
        WatchState(item);
        var actor = ApproachActor(3, 3);

        await ClickItem(item);
        _room.GetWired().OnFastCycle();
        _room.GetWired().OnCycle();
        Assert.Equal((0, 0), StateLines());
        WalkToLanding(actor, 5);
        _room.GetWired().OnFastCycle();
        _room.GetWired().OnCycle();

        Assert.Equal("1", item.LegacyDataString);
        Assert.Equal((1, 1), StateLines());
    }

    // The user walks to the touching tile nearest to where the click was made.
    [Fact]
    public async Task DistantClickOnAFloorSwitchWalksToTheNearestTouchingTile()
    {
        OpenLargeRoom();
        var item = FloorSwitchFixture(7, 6);
        var actor = ApproachActor(9, 8);

        await ClickItem(item);
        WalkToLanding(actor, 6);

        Assert.Equal((8, 7), (actor.X, actor.Y));
        Assert.Equal("1", item.LegacyDataString);
    }
}
