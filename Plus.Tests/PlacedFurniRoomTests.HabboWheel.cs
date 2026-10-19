using Plus.Communication.Packets.Incoming.Rooms.Furni;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData("30 :w=1,1 l=0,0 l")]
    [InlineData("30 :w=1,1 l=0,0 l ")]
    public async Task WallPlacementAcceptsATrailingSeparator(string raw)
    {
        Inventory(new InventoryItem { Id = 30, Definition = Furni(30, InteractionType.HabboWheel, WiredBoxType.None, ItemType.Wall).Definition });

        await PlaceObject().Parse(_room, _client, ClientPacket(raw));

        Assert.Equal(":w=1,1 l=0,0 l", _room.GetRoomItemHandler().GetItem(30)?.WallCoordinates);
    }

    [Fact]
    public async Task UseHabboWheelSpinsOnlyAWheel()
    {
        var wheel = Furni(30, InteractionType.HabboWheel, WiredBoxType.None, ItemType.Wall);
        wheel.WallCoordinates = ":w=1,1 l=0,0 l";
        var poster = Furni(31, InteractionType.None, WiredBoxType.None, ItemType.Wall);
        poster.WallCoordinates = ":w=1,1 l=0,0 l";
        Assert.True(_room.GetRoomItemHandler().SetWallItem(_client, wheel));
        Assert.True(_room.GetRoomItemHandler().SetWallItem(_client, poster));
        wheel.ExtraData = new LegacyDataFormat { Data = "4" };
        poster.ExtraData = new LegacyDataFormat { Data = "0" };
        var use = new UseHabboWheelEvent(new FurnitureUseService(null!, Proxy<IQuestManager>((_, _) => null)));

        await use.Parse(_room, _client, ClientPacket(31));
        await use.Parse(_room, _client, ClientPacket(30));

        Assert.Equal(("-1", "0"), (wheel.LegacyDataString, poster.LegacyDataString));
    }
}
