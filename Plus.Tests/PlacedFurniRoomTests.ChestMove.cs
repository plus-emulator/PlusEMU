using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(InteractionType.None)]
    [InlineData(InteractionType.WiredChestFurni)]
    public async Task MovingFurniSendsOneObjectUpdate(InteractionType type)
    {
        Viewer();
        Add(10, 1, 1, type: type);
        _client.Sent.Clear();

        await MoveObject().Parse(_room, _client, ClientPacket(10, 2, 2, 2));

        Assert.Single(_client.Sent, id => id == ServerPacketHeader.ObjectUpdateComposer);
    }
}
