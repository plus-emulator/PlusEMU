using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: the room flag furni (Invisible Furni Controller, :chooser disabler, hand item blocker).
public partial class PlacedFurniRoomTests
{
    // The way the Toggle State action writes a furni state.
    private static void WiredToggle(Item item) => GateTransitionService.ToggleState(item, current => current == "1" ? "0" : "1", GateCloseReason.Wired);

    // A switch by Wired sends the full set of flag bytes, like a use does.
    [Theory]
    [InlineData(InteractionType.HandItemBlocker, new byte[] { 1, 0, 0, 0, 0 })]
    [InlineData(InteractionType.ChooserDisabler, new byte[] { 0, 1, 0, 0, 0 })]
    [InlineData(InteractionType.InvisibleFurniControl, new byte[] { 0, 0, 0, 1, 0 })]
    public void AWiredToggleOfAFlagFurniSendsTheFlags(InteractionType type, byte[] on)
    {
        var item = PlaceConfiguration(80, type, on: false);

        WiredToggle(item);

        Assert.Equal(on, Assert.Single(StatesPackets()));

        _client.Packets.Clear();
        WiredToggle(item);

        Assert.Equal(new byte[5], Assert.Single(StatesPackets()));
    }

    [Fact]
    public void AWiredToggleOfTheInvisibleFurniControllerHidesTheInvisibleFurni()
    {
        var controller = PlaceConfiguration(70, InteractionType.InvisibleFurniControl, on: false);
        PlaceInvisible(71, 2);
        PlaceInvisible(72, 3, "room_invisible_sit_tile");
        _client.Packets.Clear();

        WiredToggle(controller);

        Assert.Equal([71u, 72u], RemovedIds(Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectRemoveMultipleComposer).Body));
    }

    [Fact]
    public void AWiredToggleOfTheWiredDisablerSendsNoFlags()
    {
        var item = PlaceConfiguration(81, InteractionType.WiredDisabler, on: false);

        WiredToggle(item);

        Assert.Empty(StatesPackets());
    }

    // Observed on AIR WIN63-202610201032-107964540: using the wired disabler sends one ObjectDataUpdate with the new state, no
    // ObjectUpdate and no room flags.
    [Theory]
    [InlineData(false, "1")]
    [InlineData(true, "0")]
    public void UsingTheWiredDisablerSendsOnlyADataUpdate(bool on, string expected)
    {
        var item = PlaceConfiguration(86, InteractionType.WiredDisabler, on);

        new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null)).Use(_room, _client, new(item.Id, 0));

        Assert.Equal(expected, item.LegacyDataString);
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer);
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
        Assert.Empty(StatesPackets());
    }

    // Four uses in the same millisecond switch the flag once: the first use counts.
    [Fact]
    public void FourUsesInTheSameMillisecondSwitchTheChooserFlagOnce()
    {
        var item = PlaceChooserDisabler(82, on: false);

        for (var use = 0; use < 4; use++) {
            item.Interactor.OnTrigger(_client, item, 0, true);
        }

        Assert.Equal(new byte[] { 0, 1, 0, 0, 0 }, StatesPacket());
        Assert.Equal("1", item.LegacyDataString);
    }

    [Fact]
    public void AUseALittleLaterSwitchesTheFlagAgain()
    {
        var item = PlaceChooserDisabler(83, on: false);
        item.Interactor.OnTrigger(_client, item, 0, true);
        _client.Packets.Clear();

        _interactionClock.Now += TimeSpan.FromMilliseconds(100);
        item.Interactor.OnTrigger(_client, item, 0, true);

        Assert.Equal(new byte[5], StatesPacket());
        Assert.Equal("0", item.LegacyDataString);
    }

    // A use without rights does nothing and does not hold back the next use.
    [Fact]
    public void AUseWithoutRightsDoesNotHoldBackTheNextUse()
    {
        var item = PlaceChooserDisabler(84, on: false);

        item.Interactor.OnTrigger(_client, item, 0, false);
        item.Interactor.OnTrigger(_client, item, 0, true);

        Assert.Equal(new byte[] { 0, 1, 0, 0, 0 }, StatesPacket());
    }

    // Wired switches are not held back by a use in the same millisecond.
    [Fact]
    public void AWiredToggleRightAfterAUseStillSwitches()
    {
        var item = PlaceChooserDisabler(85, on: false);
        item.Interactor.OnTrigger(_client, item, 0, true);
        _client.Packets.Clear();

        item.Interactor.OnWiredTrigger(item);

        Assert.Equal(new byte[5], StatesPacket());
    }
}
