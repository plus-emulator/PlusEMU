using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Quests;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private static ItemDefinition StateHeightBlock()
    {
        // As the furniture table defines it: five states, 1.0 high at state 0 and 0.25 lower per state.
        var definition = RecolorableFurniTests.Block();
        definition.AdjustableHeights = [1.0, 0.75, 0.5, 0.25, 0.0];

        return definition;
    }

    // Each double-click on a free block sends ObjectDataUpdate, then ObjectUpdate with the new height, then HeightMapUpdate
    // for its tile, and state 4 goes back to 0.
    [Fact]
    public async Task DoubleClickingAFreeBlockSendsItsStateThenItsNewHeightThenTheHeightMap()
    {
        AdmitOwner();
        var block = PlacedRecolorable(250, definition: StateHeightBlock());
        var use = new UseFurnitureEvent(new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null)));

        foreach (var (state, height, tile) in new[] { ("1", "0.75", 0x00C0), ("2", "0.5", 0x0080), ("3", "0.25", 0x0040), ("4", "0.0", 0x0000), ("0", "1.0", 0x0100) }) {
            _client.Packets.Clear();

            await use.Parse(_room, _client, ClientPacket((int)block.Id, 0));

            var sent = StateHeightPackets();
            Assert.Equal([ServerPacketHeader.ObjectDataUpdateComposer, ServerPacketHeader.ObjectUpdateComposer, ServerPacketHeader.HeightMapUpdateComposer],
                sent.Select(packet => packet.Header));
            Assert.Equal(("250", state), ReadDataUpdate(sent[0].Body));
            Assert.Equal((250u, "0.0", height, state), ReadFloorUpdate(sent[1].Body));
            Assert.Equal(new[] { (1, 1, tile) }, ReadHeightMapUpdate(sent[2].Body));
        }
    }

    // A double-click on a block with another block on top sends nothing and keeps its state; the top block changes as usual
    // and stays at Z 1.0.
    [Fact]
    public async Task DoubleClickingABlockWithFurniOnTopDoesNothing()
    {
        AdmitOwner();
        var lower = PlacedRecolorable(251, definition: StateHeightBlock());
        var upper = PlacedRecolorable(252, definition: StateHeightBlock());
        Assert.Equal(1.0, upper.GetZ);
        var use = new UseFurnitureEvent(new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null)));
        _client.Packets.Clear();

        await use.Parse(_room, _client, ClientPacket((int)lower.Id, 0));

        Assert.Equal("0", lower.LegacyDataString);
        Assert.Empty(StateHeightPackets());

        await use.Parse(_room, _client, ClientPacket((int)upper.Id, 0));

        var update = Assert.Single(StateHeightPackets(), packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
        Assert.Equal((252u, "1.0", "0.75", "1"), ReadFloorUpdate(update.Body));
    }

    // On room entry Objects lists a block in state 3 with height 0.25; the state is kept.
    [Theory]
    [InlineData("state\t3\nr\t255\ng\t255\nb\t255", "3", "0.25")]
    [InlineData("3", "3", "0.25")]
    [InlineData("255,255,255", "0", "1.0")]
    public void RoomEntryListsAStoredBlockWithItsStateHeight(string stored, string state, string height)
    {
        AdmitOwner();
        _room.GetRoomItemHandler().LoadFurniture([StoredBlock(253, stored)]);
        _client.Packets.Clear();

        _room.SendObjects(_client);

        var objects = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectsComposer).Body };
        var owners = objects.ReadInt();

        for (var index = 0; index < owners; index++) {
            objects.ReadInt();
            objects.ReadString();
        }

        Assert.Equal(1, objects.ReadInt());
        Assert.Equal((253u, "0.0", height, state), ReadFloorItem(objects));
    }

    private Item StoredBlock(uint id, string stored)
    {
        var definition = StateHeightBlock();
        var item = new Item { Id = id, OwnerId = 7, RoomId = RoomId, Definition = definition, ExtraData = FurniExtraData.Load(definition, stored, keepLegacy: true) };
        item.SetState(1, 1, 0);

        return item;
    }

    private (uint Header, byte[] Body)[] StateHeightPackets() =>
    [
        .. _client.Packets.Where(packet => packet.Header is ServerPacketHeader.ObjectDataUpdateComposer or ServerPacketHeader.ObjectUpdateComposer
            or ServerPacketHeader.HeightMapUpdateComposer or ServerPacketHeader.ObjectsDataUpdateComposer)
    ];

    private static (string Id, string State) ReadDataUpdate(byte[] body)
    {
        var input = new FlashIncomingPacket { Buffer = body };
        var id = input.ReadString();
        Assert.Equal((int)Plus.HabboHotel.Items.DataFormat.FurniDataStructure.Map, input.ReadInt());

        return (id, ReadMapState(input));
    }

    private static (uint Id, string Z, string Height, string State) ReadFloorUpdate(byte[] body) =>
        ReadFloorItem(new FlashIncomingPacket { Buffer = body });

    // id, sprite, x, y, rotation, z, height, extra, then the map data with the state.
    private static (uint Id, string Z, string Height, string State) ReadFloorItem(FlashIncomingPacket input)
    {
        var id = (uint)input.ReadInt();
        input.ReadInt();
        input.ReadInt();
        input.ReadInt();
        input.ReadInt();
        var z = input.ReadString();
        var height = input.ReadString();
        input.ReadInt();
        Assert.Equal((int)Plus.HabboHotel.Items.DataFormat.FurniDataStructure.Map, input.ReadInt() & 0xFF);

        return (id, z, height, ReadMapState(input));
    }

    private static string ReadMapState(FlashIncomingPacket input)
    {
        var state = "";

        for (var pairs = input.ReadInt(); pairs > 0; pairs--) {
            var key = input.ReadString();
            var value = input.ReadString();
            state = key == "state" ? value : state;
        }

        return state;
    }

    private static (int X, int Y, int Value)[] ReadHeightMapUpdate(byte[] body) =>
    [
        .. Enumerable.Range(0, body[0]).Select(index => ((int)body[1 + index * 4], (int)body[2 + index * 4],
            (int)System.Buffers.Binary.BinaryPrimitives.ReadInt16BigEndian(body.AsSpan(3 + index * 4))))
    ];
}
