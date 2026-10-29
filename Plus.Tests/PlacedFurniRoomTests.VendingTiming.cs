using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Xunit;

namespace Plus.Tests;

// A vending machine runs on its own timer, not on the 500 ms room tick: the hand item 1250 ms after the use and state 0 at 1500 ms.
// A machine without a hand item goes back to 0 at 1000 ms. Both states go out as ObjectDataUpdate.
public partial class PlacedFurniRoomTests
{
    private const uint VendingId = 90;

    [Fact]
    public async Task AVendingMachineHandsItsItemOverAt1250MsAndGoesBackToZeroAt1500MsAfterTheUse()
    {
        var (clock, manager) = GridRoom();
        var vending = VendingMachine(148);
        var actor = ExecutorActor(2, 1);
        clock.Ms += 130;
        var usedAt = clock.Ms;

        await UseVending(vending);
        var passes = RunRoom(manager, clock, usedAt + 2500, period: 25);

        var carry = Assert.Single(passes, pass => pass.Packets.Any(packet => packet.Header == ServerPacketHeader.CarryObjectComposer));
        Assert.InRange(carry.At - usedAt, 1250, 1299);
        var reset = Assert.Single(passes, pass => VendingStates(pass.Packets).Contains("0"));
        Assert.InRange(reset.At - usedAt, 1500, 1549);
        Assert.Equal(148, actor.CarryItemId);
        Assert.Equal("0", vending.LegacyDataString);
        Assert.True(actor.CanWalk);
    }

    [Fact]
    public async Task AVendingMachineWithoutAHandItemGoesBackToZeroAt1000MsAfterTheUse()
    {
        var (clock, manager) = GridRoom();
        var vending = VendingMachine();
        var actor = ExecutorActor(2, 1);
        clock.Ms += 130;
        var usedAt = clock.Ms;

        await UseVending(vending);
        var passes = RunRoom(manager, clock, usedAt + 2000, period: 25);

        var reset = Assert.Single(passes, pass => VendingStates(pass.Packets).Contains("0"));
        Assert.InRange(reset.At - usedAt, 1000, 1049);
        Assert.DoesNotContain(passes, pass => pass.Packets.Any(packet => packet.Header == ServerPacketHeader.CarryObjectComposer));
        Assert.Equal(0, actor.CarryItemId);
        Assert.True(actor.CanWalk);
    }

    [Fact]
    public async Task AVendingMachineSendsItsStatesAsObjectDataUpdatesOnly()
    {
        var (clock, manager) = GridRoom();
        var vending = VendingMachine(148);
        ExecutorActor(2, 1);
        _client.Packets.Clear();

        await UseVending(vending);
        var atUse = _client.Packets.ToArray();
        var passes = RunRoom(manager, clock, clock.Ms + 2500, period: 25);
        var sent = atUse.Concat(passes.SelectMany(pass => pass.Packets)).ToArray();

        Assert.Equal(["1", "0"], VendingStates(sent));
        Assert.DoesNotContain(sent, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }

    private Item VendingMachine(params int[] handItems)
    {
        var vending = Furni(VendingId, InteractionType.VendingMachine, WiredBoxType.None);
        vending.Definition.Modes = 2;
        vending.Definition.VendingIds.AddRange(handItems);
        InitializeNativeState(vending);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, vending, 2, 0, 0, true, false, false));

        return vending;
    }

    private async Task UseVending(Item vending)
    {
        var use = new UseFurnitureEvent(new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null)));
        await use.Parse(_room, _client, ClientPacket((int)vending.Id, 0));
        Assert.Equal("1", vending.LegacyDataString);
    }

    private static string[] VendingStates(IEnumerable<(uint Header, byte[] Body)> packets) => [.. packets
        .Where(packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer)
        .Select(packet => new FlashIncomingPacket { Buffer = packet.Body })
        .Select(body => (Id: body.ReadString(), Type: body.ReadInt(), State: body.ReadString()))
        .Where(update => update.Id == VendingId.ToString(System.Globalization.CultureInfo.InvariantCulture))
        .Select(update => update.State)];
}
