using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void PlacingFirstLegacyRollerStartsTransportWithoutReloadingRoom()
    {
        ExecutorRoller(10, 0, 1);
        ExecutorRoller(11, 1, 1);
        var actor = RollerChainActor(1, 0, .5);
        var handler = _room.GetRoomItemHandler();
        handler.SetSpeed(0);

        handler.OnCycle();

        Assert.Equal((1, 1, .5), (actor.X, actor.Y, actor.Z));
        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.SlideObjectBundleComposer);
    }

    [Fact]
    public void LoadingLegacyRollerRoomStartsTransportAndKeepsHeightAcrossRollers()
    {
        Item Roller(uint id, int x)
        {
            var item = Furni(id, InteractionType.Roller, WiredBoxType.None);
            item.RoomId = RoomId;
            item.Definition.Walkable = true;
            item.Definition.Height = .5;
            item.Definition.Width = item.Definition.Length = 1;
            item.GetX = x;
            item.GetY = 1;
            item.Rotation = 2;
            return item;
        }

        var handler = _room.GetRoomItemHandler();
        handler.LoadFurniture([Roller(10, 0), Roller(11, 1)]);
        _room.GetGameMap().GenerateMaps();
        var actor = RollerChainActor(1, 0, .5);
        handler.SetSpeed(0);

        handler.OnCycle();

        Assert.Equal((1, 1, .5), (actor.X, actor.Y, actor.Z));
        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.SlideObjectBundleComposer);
    }
}
