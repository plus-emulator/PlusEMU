using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private Item PlaceSpeedController(string state)
    {
        PrepareSpeech(new SpeechClock());
        var item = Furni(90, InteractionType.RollerSpeedController, WiredBoxType.None);
        item.RoomId = RoomId;
        item.Definition.Modes = 12;
        item.ExtraData = new LegacyDataFormat { Data = state };
        item.SetState(1, 1, 0);
        _room.GetRoomItemHandler().LoadFurniture([item]);
        _client.Packets.Clear();

        return item;
    }

    // Observed on AIR WIN63-202610201032-107964540: a room without the controller steps its rollers every 2000 ms.
    [Fact]
    public void ARoomWithoutTheSpeedControllerStepsEvery2000Milliseconds()
    {
        Assert.Equal(2000, ConfigurationItemStates.RollerStepMilliseconds(_room));
        Assert.Equal(4, _room.GetRoomItemHandler().RollerStepTicks);
    }

    // Observed on AIR WIN63-202610201032-107964540: the 12 states form four groups of three: 500, 1000, 1500 and 2000 ms.
    [Theory]
    [InlineData("0", 500)]
    [InlineData("2", 500)]
    [InlineData("3", 1000)]
    [InlineData("5", 1000)]
    [InlineData("6", 1500)]
    [InlineData("8", 1500)]
    [InlineData("9", 2000)]
    [InlineData("11", 2000)]
    public void TheStateOfTheSpeedControllerSetsTheRollerStepTime(string state, int milliseconds)
    {
        PlaceSpeedController(state);

        Assert.Equal(milliseconds, ConfigurationItemStates.RollerStepMilliseconds(_room));
        Assert.Equal(milliseconds / 500, _room.GetRoomItemHandler().RollerStepTicks);
    }

    // Observed on AIR WIN63-202610201032-107964540: picking the controller up brings the step time back to 2000 ms at once.
    [Fact]
    public async Task PickingTheSpeedControllerUpBringsTheStepTimeBackTo2000()
    {
        PlaceSpeedController("0");

        await PickupObject().Parse(_client, ClientPacket(0, 90));

        Assert.Equal(2000, ConfigurationItemStates.RollerStepMilliseconds(_room));
    }

    // Observed on AIR WIN63-202610201032-107964540: each use jumps to the first state of the next group: 0, 3, 6, 9, 0. The answer is
    // one ObjectDataUpdate with the state.
    [Fact]
    public void EachUseOfTheSpeedControllerMovesToTheNextGroup()
    {
        var item = PlaceSpeedController("0");
        var seen = new List<string>();

        for (var i = 0; i < 5; i++) {
            item.Interactor.OnTrigger(_client, item, 0, true);
            seen.Add(item.LegacyDataString);
        }

        Assert.Equal(new[] { "3", "6", "9", "0", "3" }, seen);
        Assert.Equal(5, _client.Packets.Count(packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer));
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }

    [Fact]
    public void AUserWithoutRightsCannotUseTheSpeedController()
    {
        var item = PlaceSpeedController("0");

        item.Interactor.OnTrigger(_client, item, 0, false);

        Assert.Equal("0", item.LegacyDataString);
    }

    // Observed on AIR WIN63-202610201032-107964540: on every roller step the controller sends a data update that alternates between the
    // two frames after its group state, and it keeps its group state. Steps come every 500, 1000, 1500 or 2000 ms.
    [Theory]
    [InlineData("0", 1, new[] { "1", "2", "1", "2" })]
    [InlineData("3", 2, new[] { "4", "5", "4", "5" })]
    [InlineData("6", 3, new[] { "7", "8", "7", "8" })]
    [InlineData("9", 4, new[] { "10", "11", "10", "11" })]
    public void TheSpeedControllerAnimatesBetweenItsTwoFramesOnEveryRollerStep(string state, int ticksPerStep, string[] frames)
    {
        var item = PlaceSpeedController(state);
        var handler = _room.GetRoomItemHandler();

        for (var tick = 0; tick < ticksPerStep * frames.Length; tick++) {
            handler.OnCycle();
        }

        var sent = _client.Packets.Where(packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer)
            .Select(packet => System.Text.Encoding.ASCII.GetString(packet.Body).Split('\0', StringSplitOptions.RemoveEmptyEntries).Last()).ToList();
        Assert.Equal(frames.Length, sent.Count);
        Assert.All(sent.Zip(frames), pair => Assert.EndsWith(pair.Second, pair.First));
        Assert.Equal(state, item.LegacyDataString);
    }
}
