using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private Item FloorSwitchFixture()
    {
        var item = InteractionItem(10, 0, 0, InteractionType.WfFloorSwitch1);
        item.Definition.Modes = 2;
        item.LegacyDataString = "0";

        return item;
    }

    // A click from a distance walks the user to the nearest tile touching the switch and switches it once the user arrives.
    [Fact]
    public void DistantClickOnAFloorSwitchSwitchesItOnArrival()
    {
        var item = FloorSwitchFixture();
        var actor = ApproachActor(3, 3);

        item.Interactor.OnTrigger(_client, item, 0, true);
        Assert.Equal("0", item.LegacyDataString);
        WalkToLanding(actor, 5);

        Assert.True(Gamemap.TilesTouching(item.GetX, item.GetY, actor.X, actor.Y));
        Assert.Equal("1", item.LegacyDataString);
        Assert.Null(Approaches.Peek(actor));
    }

    // Clicks made while the user is still walking do not add up: the last one is the use on arrival.
    [Fact]
    public void ClicksWhileWalkingToAFloorSwitchSwitchItOnce()
    {
        var item = FloorSwitchFixture();
        var actor = ApproachActor(3, 3);

        item.Interactor.OnTrigger(_client, item, 0, true);
        ExecutorTick();
        item.Interactor.OnTrigger(_client, item, 0, true);
        WalkToLanding(actor, 6);

        Assert.True(Gamemap.TilesTouching(item.GetX, item.GetY, actor.X, actor.Y));
        Assert.Equal("1", item.LegacyDataString);
    }

    // A click from next to the switch switches it at once, also on the diagonal.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    public void ClickNextToAFloorSwitchSwitchesItAtOnce(int x, int y)
    {
        var item = FloorSwitchFixture();
        ApproachActor(x, y);

        item.Interactor.OnTrigger(_client, item, 0, true);
        Assert.Equal("1", item.LegacyDataString);
        item.Interactor.OnTrigger(_client, item, 0, true);
        Assert.Equal("0", item.LegacyDataString);
    }
}
