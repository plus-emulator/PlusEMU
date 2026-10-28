using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public async Task APlacedAndMovedRollerIsSentWithExtraOneAndStateOne()
    {
        Viewer();
        Assert.NotNull(await Drop(20, 1, 1, InteractionType.Roller));
        Assert.Equal((1, 1, "1"), RollerWire(ServerPacketHeader.ObjectAddComposer, 1));

        _client.Packets.Clear();
        await MoveObject().Parse(_room, _client, ClientPacket(20, 2, 2, 4));
        Assert.Equal((2, 1, "1"), RollerWire(ServerPacketHeader.ObjectUpdateComposer, 2));
    }

    // A roller has three states in the furniture table, but nobody may use it: usage policy 0, and a use changes nothing.
    [Fact]
    public async Task ARollerIsSentWithUsagePolicyNoneAndIgnoresAUse()
    {
        Viewer();
        var definition = Furni(21, InteractionType.Roller, WiredBoxType.None).Definition;
        definition.Modes = 3;
        Inventory(new InventoryItem { Id = 21, OwnerId = 7, Definition = definition });
        await PlaceObject().Parse(_room, _client, ClientPacket("21 1 1 0"));
        var roller = _room.GetRoomItemHandler().GetItem(21);
        Assert.NotNull(roller);
        Assert.Equal(0, RollerUsage(ServerPacketHeader.ObjectAddComposer));

        _client.Packets.Clear();
        await MoveObject().Parse(_room, _client, ClientPacket(21, 2, 1, 2));
        Assert.Equal(0, RollerUsage(ServerPacketHeader.ObjectUpdateComposer));

        var stored = roller.LegacyDataString;
        _client.Packets.Clear();
        await new UseFurnitureEvent(new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null)))
            .Parse(_room, _client, ClientPacket(21, 0));
        _room.ProcessRoom();

        Assert.DoesNotContain(_client.Packets, packet => packet.Header is ServerPacketHeader.ObjectDataUpdateComposer
            or ServerPacketHeader.ObjectUpdateComposer or ServerPacketHeader.ObjectsDataUpdateComposer);
        Assert.Equal(stored, roller.LegacyDataString);
    }

    private (int X, int Extra, string State) RollerWire(uint header, int x)
    {
        var sent = _client.Packets.Last(packet => packet.Header == header);
        var body = new FlashIncomingPacket { Buffer = sent.Body.ToArray() };
        Assert.Equal(20u, body.ReadUInt());
        body.ReadInt();
        var readX = body.ReadInt();
        body.ReadInt();
        body.ReadInt();
        body.ReadString();
        body.ReadString();
        var extra = body.ReadInt();
        Assert.Equal(0, body.ReadInt());
        Assert.Equal(x, readX);

        return (readX, extra, body.ReadString());
    }

    private int RollerUsage(uint header)
    {
        var sent = _client.Packets.Last(packet => packet.Header == header);
        var body = new FlashIncomingPacket { Buffer = sent.Body.ToArray() };
        body.ReadUInt();
        body.ReadInt();
        body.ReadInt();
        body.ReadInt();
        body.ReadInt();
        body.ReadString();
        body.ReadString();
        body.ReadInt();
        Assert.Equal(0, body.ReadInt());
        Assert.Equal("1", body.ReadString());
        body.ReadInt();

        return body.ReadInt();
    }
}
