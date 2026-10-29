using System.Buffers.Binary;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private Item PlaceForMove(uint id, InteractionType interaction, WiredBoxType type)
    {
        var box = Furni(id, interaction, type);
        box.UserId = 7;
        box.Definition.Width = 1;
        box.Definition.Length = 1;
        box.ExtraData = new LegacyDataFormat { Data = "0" };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, box, 1, 1, 0, true, false, false));

        if (type != WiredBoxType.None) {
            _room.GetWired().LoadWiredBox(box);
        }

        return box;
    }

    private string MoveUpdateState(uint id)
    {
        var update = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer
            && BinaryPrimitives.ReadUInt32BigEndian(packet.Body) == id);
        var data = update.Body.AsSpan(..^12);
        var length = BinaryPrimitives.ReadInt16BigEndian(data[^3..]);

        return System.Text.Encoding.UTF8.GetString(data[^length..]);
    }

    [Fact]
    public async Task MovingAFlashedWiredTriggerSendsItsRestingStateAndKeepsFlipping()
    {
        EnterAsOwner();
        var trigger = PlaceForMove(50, InteractionType.WiredTrigger, WiredBoxType.TriggerWalkOnFurni);
        _room.GetWired().OnEvent(trigger);
        _room.FlushWiredFlashes();
        Assert.Equal("1", trigger.LegacyDataString);
        _client.Packets.Clear();

        await MoveObject().Parse(_room, _client, ClientPacket(50, 1, 1, 2));

        Assert.Equal("0", MoveUpdateState(50));
        Assert.Equal("1", trigger.LegacyDataString);
    }

    [Fact]
    public async Task MovingOrdinaryFurnitureKeepsItsState()
    {
        EnterAsOwner();
        var chair = PlaceForMove(51, InteractionType.None, WiredBoxType.None);
        chair.LegacyDataString = "1";
        _client.Packets.Clear();

        await MoveObject().Parse(_room, _client, ClientPacket(51, 1, 1, 2));

        Assert.Equal("1", MoveUpdateState(51));
    }
}
