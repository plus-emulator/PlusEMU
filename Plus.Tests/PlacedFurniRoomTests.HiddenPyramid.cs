using System.Drawing;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Xunit;

namespace Plus.Tests;

// A hidden pyramid flips between 0 and 1 on its own. It stays 0 for 8 to 20 room ticks and 1 for 10 to 30 room ticks.
public partial class PlacedFurniRoomTests
{
    private sealed class FixedDraw(bool lowest) : Random
    {
        public override int Next(int minValue, int maxValue) => lowest ? minValue : maxValue - 1;
    }

    private Item HiddenPyramid(string state, bool lowest = true, Point? standingOn = null)
    {
        _room.GetGameItemHandler().Dice = new FixedDraw(lowest);
        ActorOn(new Point(3, 3));
        var pyramid = Furni(30, InteractionType.Gate, WiredBoxType.None);
        pyramid.Definition.ItemName = "wf_pyramid";
        pyramid.Definition.Height = 0;
        pyramid.Definition.Modes = 4;
        pyramid.Definition.Walkable = false;
        pyramid.Definition.Width = pyramid.Definition.Length = 1;
        pyramid.GroupId = 7;
        pyramid.UserId = 7;
        InitializeNativeState(pyramid);
        pyramid.LegacyDataString = state;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, pyramid, 1, 1, 0, true, false, false));

        if (standingOn is { } tile) {
            ActorOn(tile);
        }

        return pyramid;
    }

    private int TicksUntilItFlips(Item pyramid, int limit)
    {
        var before = pyramid.LegacyDataString;

        for (var tick = 1; tick <= limit; tick++) {
            ExecutorTick();

            if (pyramid.LegacyDataString != before) {
                return tick;
            }
        }

        return -1;
    }

    [Theory]
    [InlineData("0", "1", true, 8)]
    [InlineData("0", "1", false, 20)]
    [InlineData("1", "0", true, 10)]
    [InlineData("1", "0", false, 30)]
    public void HiddenPyramidFlipsAfterTheDrawnNumberOfRoomTicks(string state, string flipped, bool lowest, int ticks)
    {
        var pyramid = HiddenPyramid(state, lowest);

        Assert.Equal(ticks, TicksUntilItFlips(pyramid, 40));
        Assert.Equal(flipped, pyramid.LegacyDataString);
    }

    [Theory]
    [InlineData("0", 8, 20)]
    [InlineData("1", 10, 30)]
    public void HiddenPyramidFlipsWithinItsOwnRangeOfRoomTicks(string state, int fewest, int most)
    {
        var pyramid = HiddenPyramid(state);
        _room.GetGameItemHandler().Dice = Random.Shared;
        var before = pyramid.LegacyDataString;

        Assert.InRange(TicksUntilItFlips(pyramid, most), fewest, most);
        Assert.NotEqual(before, pyramid.LegacyDataString);
    }

    [Fact]
    public void HiddenPyramidKeepsFlippingBetweenTheTwoStates()
    {
        var pyramid = HiddenPyramid("0");

        Assert.Equal(8, TicksUntilItFlips(pyramid, 20));
        Assert.Equal("1", pyramid.LegacyDataString);
        Assert.Equal(10, TicksUntilItFlips(pyramid, 30));
        Assert.Equal("0", pyramid.LegacyDataString);
        Assert.Equal(8, TicksUntilItFlips(pyramid, 20));
        Assert.Equal("1", pyramid.LegacyDataString);
    }

    [Fact]
    public void HiddenPyramidFlipGoesOutAsOneObjectDataUpdateWithTheNewState()
    {
        var pyramid = HiddenPyramid("0");

        Assert.Equal(8, TicksUntilItFlips(pyramid, 20));

        var headers = _client.Packets.Select(packet => packet.Header).ToArray();
        var update = new FlashIncomingPacket { Buffer = _client.Packets.Single(packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer).Body };
        Assert.Equal("30", update.ReadString());
        Assert.Equal(0, update.ReadInt());
        Assert.Equal("1", update.ReadString());
        Assert.DoesNotContain(ServerPacketHeader.ObjectUpdateComposer, headers);
        Assert.DoesNotContain(ServerPacketHeader.ObjectsDataUpdateComposer, headers);
    }

    [Fact]
    public void HiddenPyramidWithSomeoneOnItDoesNotRiseUntilTheTileIsFree()
    {
        var pyramid = HiddenPyramid("1", standingOn: new Point(1, 1));

        for (var tick = 0; tick < 60; tick++) {
            ExecutorTick();
            Assert.Equal("1", pyramid.LegacyDataString);
        }
    }

    [Fact]
    public void HiddenPyramidKeepsItsTimerWhenItMoves()
    {
        var pyramid = HiddenPyramid("0");

        for (var tick = 1; tick < 8; tick++) {
            _room.GetGameMap().UpdateMapForItem(pyramid);
            ExecutorTick();
        }

        _room.GetGameMap().UpdateMapForItem(pyramid);

        Assert.Equal(1, TicksUntilItFlips(pyramid, 3));
    }

    [Fact]
    public void PickedUpHiddenPyramidStopsFlipping()
    {
        var pyramid = HiddenPyramid("0");
        _room.GetRoomItemHandler().RemoveFurniture(null!, pyramid.Id);

        Assert.Equal(-1, TicksUntilItFlips(pyramid, 40));
    }
}
