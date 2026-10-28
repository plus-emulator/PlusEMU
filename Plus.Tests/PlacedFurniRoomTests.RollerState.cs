using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
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
}
