using Plus.Communication.Packets.Incoming.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public async Task DropCarryItemDropsTheHandItemWhileTheBlockerIsOff()
    {
        PlaceConfiguration(80, InteractionType.HandItemBlocker, on: false);
        var avatar = _room.GetRoomUserManager().GetRoomUserByHabbo(7)!;
        avatar.CarryItem(5);

        await new DropHandItemEvent().Parse(_room, _client, ClientPacket());

        Assert.Equal(0, avatar.CarryItemId);
    }

    // With the blocker on, DropCarryItem is ignored with no reply and the hand item stays.
    [Fact]
    public async Task DropCarryItemIsIgnoredWithoutAReplyWhileTheBlockerIsOn()
    {
        PlaceConfiguration(80, InteractionType.HandItemBlocker, on: true);
        var avatar = _room.GetRoomUserManager().GetRoomUserByHabbo(7)!;
        avatar.CarryItem(5);
        _client.Packets.Clear();

        await new DropHandItemEvent().Parse(_room, _client, ClientPacket());

        Assert.Equal(5, avatar.CarryItemId);
        Assert.Empty(_client.Packets);
    }

    // A vending machine still gives a hand item while the blocker is on.
    [Fact]
    public void AnAvatarStillGetsANewHandItemWhileTheBlockerIsOn()
    {
        PlaceConfiguration(80, InteractionType.HandItemBlocker, on: true);
        var avatar = _room.GetRoomUserManager().GetRoomUserByHabbo(7)!;
        avatar.CarryItem(5);

        avatar.CarryItem(7);

        Assert.Equal(7, avatar.CarryItemId);
    }
}
