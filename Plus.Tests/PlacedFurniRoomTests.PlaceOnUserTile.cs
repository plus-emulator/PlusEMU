using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Observed on hhus (AIR WIN63-202610131318-860371703): the owner places a floor item on the tile they stand on, and moves one onto it.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public void AUserPlacesAndMovesFurniOntoTheTileTheyStandOn()
    {
        var user = new RoomUser(7, RoomId, 1, _room, _client, TestChatEmotions.Unused, TestRewardProgress.Unused) { X = 1, Y = 1 };
        _room.GetGameMap().AddUserToMap(user, new(1, 1));

        var placed = Furni(300, InteractionType.None, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, placed, 1, 1, 0, true, false, false));

        var moved = Furni(301, InteractionType.None, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, moved, 2, 2, 0, true, false, false));
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, moved, 1, 1, 0, false, false, false));

        // A server-side placement (no session) still treats the avatar as blocking.
        var server = Furni(302, InteractionType.None, WiredBoxType.None);
        Assert.False(_room.GetRoomItemHandler().SetFloorItem(null!, server, 1, 1, 0, true, false, false));
    }
}
