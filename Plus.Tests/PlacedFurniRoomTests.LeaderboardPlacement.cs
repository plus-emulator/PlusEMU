using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// Placing a Text Connector on a published board's tile, or moving one onto or off that tile, republishes the board at the next tick of
// the push grid, like a save. Picking up the board sends the snapshot without it at the next tick, not at once.
public partial class PlacedFurniRoomTests
{
    private int[] SnapshotDescriptorCounts() => [.. _client.Packets.Where(packet => packet.Header == ServerPacketHeader.WiredVariableLeaderboardSnapshotComposer)
        .Select(packet => new FlashIncomingPacket { Buffer = packet.Body }.ReadInt())];

    [Fact]
    public async Task PlacingAConnectorOnTheBoardTileRepublishesAtTheNextGridTick()
    {
        var (clock, _, revision) = await PublishedBoard();

        clock.Now = BoardOrigin.AddMilliseconds(20535);
        _client.Packets.Clear();
        var item = Furni(1110, InteractionType.None, WiredBoxType.None);
        item.Definition.InteractionName = item.Definition.ItemName = "wf_xtra_var_text_connector";
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, item, 0, 3, 0, true, false, true));
        Assert.NotNull(_room.GetWired().LoadWiredBox(item));
        CycleUntil(clock, 20535, 24686);
        Assert.Empty(BoardSnapshots());

        CycleAt(clock, 24687);
        Assert.Equal(revision + 1, Assert.Single(BoardSnapshots()));
    }

    [Fact]
    public async Task MovingTheBoardConnectorOffAndBackRepublishesEachTime()
    {
        var (clock, service, revision) = await PublishedBoard();
        await SaveBoardConnector(clock, service, 34373, "5=five");
        CycleUntil(clock, 34373, 44687);
        Assert.Equal(++revision, Assert.Single(BoardSnapshots()));
        var connector = _room.GetRoomItemHandler().GetItem(BoardConnector)!;

        clock.Now = BoardOrigin.AddMilliseconds(58233);
        _client.Packets.Clear();
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, connector, 1, 0, 0, false, false, true));
        CycleUntil(clock, 58233, 64687);
        Assert.Equal(revision + 1, Assert.Single(BoardSnapshots()));
        await Resubscribe(clock, 64840, revision + 1);
        Assert.Equal((revision + 1, "5", (string?)null), Assert.Single(BoardChunks()));

        clock.Now = BoardOrigin.AddMilliseconds(82235);
        _client.Packets.Clear();
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, connector, 0, 3, 0, false, false, true));
        CycleUntil(clock, 82235, 84687);
        Assert.Equal(revision + 2, Assert.Single(BoardSnapshots()));
        await Resubscribe(clock, 84840, revision + 2);
        Assert.Equal((revision + 2, "5", "five"), Assert.Single(BoardChunks()));
    }

    [Fact]
    public async Task PickingUpTheBoardSendsTheEmptySnapshotAtTheNextGridTick()
    {
        var (clock, _, _) = await PublishedBoard();
        _client.GetHabbo().Inventory = new InventoryComponent { Furniture = new FurnitureInventoryComponent([], []) };

        clock.Now = BoardOrigin.AddMilliseconds(130236);
        _client.Packets.Clear();
        await PickupObject().Parse(_client, ClientPacket(0, (int)BoardBox));
        Assert.Null(_room.GetRoomItemHandler().GetItem(BoardBox));
        Assert.Empty(SnapshotDescriptorCounts());
        CycleUntil(clock, 130236, 134686);
        Assert.Empty(SnapshotDescriptorCounts());

        CycleAt(clock, 134687);
        Assert.Equal([0], SnapshotDescriptorCounts());
    }
}
