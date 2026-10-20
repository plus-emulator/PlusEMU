using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Plus.HabboHotel.Users.Inventory;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const string TypeLimit = "${room.error.type_limit}";

    private Item PlaceChooserDisabler(uint id, bool on, int x = 1)
    {
        Viewer();
        var item = Furni(id, InteractionType.ChooserDisabler, WiredBoxType.None);
        item.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = on ? "1" : "0" };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, item, x, 1, 0, true, false, false));
        _client.Packets.Clear();
        _client.Sent.Clear();

        return item;
    }

    private byte[] StatesPacket() => Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ConfigurationItemStatesComposer).Body;

    // Observed on AIR WIN63-202610201032-107964540: each use of the disabler flips the flag, and so does a Wired toggle.
    [Fact]
    public void EachUseAndEachWiredToggleFlipsTheChooserFlag()
    {
        var item = PlaceChooserDisabler(40, on: false);

        item.Interactor.OnTrigger(_client, item, 0, true);
        Assert.Equal(new byte[] { 0, 1, 0, 0, 0 }, StatesPacket());

        _client.Packets.Clear();
        item.Interactor.OnWiredTrigger(item);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0 }, StatesPacket());
    }

    // Observed on AIR WIN63-202610201032-107964540: picking the disabler up while it is on sends the flag still on, and the next room
    // entry sends it off.
    [Fact]
    public async Task PickingUpTheChooserDisablerWhileOnSendsTheFlagStillOn()
    {
        var item = PlaceChooserDisabler(41, on: true);

        await PickupObject().Parse(_client, ClientPacket(0, 41));

        Assert.Null(_room.GetRoomItemHandler().GetItem(41));
        Assert.Equal(new byte[] { 0, 1, 0, 0, 0 }, StatesPacket());
        Assert.False(Plus.HabboHotel.Items.ConfigurationItemStates.ChooserDisabled(_room));
        Assert.NotNull(item);
    }

    // Observed on AIR WIN63-202610201032-107964540: picking the disabler up while it is off sends no flag packet.
    [Fact]
    public async Task PickingUpTheChooserDisablerWhileOffSendsNoFlag()
    {
        PlaceChooserDisabler(42, on: false);

        await PickupObject().Parse(_client, ClientPacket(0, 42));

        Assert.Null(_room.GetRoomItemHandler().GetItem(42));
        Assert.DoesNotContain(ServerPacketHeader.ConfigurationItemStatesComposer, _client.Sent);
    }

    // Observed on AIR WIN63-202610201032-107964540: a second disabler in a room is refused with the type limit text.
    [Fact]
    public async Task ASecondChooserDisablerInTheRoomIsRefusedWithTheTypeLimit()
    {
        PlaceChooserDisabler(43, on: false);
        Inventory(new InventoryItem { Id = 44, Definition = Furni(44, InteractionType.ChooserDisabler, WiredBoxType.None).Definition });

        await PlaceObject().Parse(_room, _client, ClientPacket("44 2 1 0"));

        Assert.Null(_room.GetRoomItemHandler().GetItem(44));
        Assert.NotNull(Assert.IsType<InventoryComponent>(_client.GetHabbo().Inventory).Furniture.GetItem(44));
        var notice = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.RoomNotificationComposer).Body };
        Assert.Equal("furni_placement_error", notice.ReadString());
        Assert.Equal(1, notice.ReadInt());
        Assert.Equal("message", notice.ReadString());
        Assert.Equal(TypeLimit, notice.ReadString());
    }

    [Fact]
    public void ASecondBuildersClubChooserDisablerIsRefusedWithTheTypeLimit()
    {
        Viewer();
        var (service, store, page) = BuildersClub();
        page.Offers[BuildersClubOfferId].Product.Definition!.InteractionType = InteractionType.ChooserDisabler;
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0, confirmed: true);
        Assert.Single(store.Created);
        _client.Packets.Clear();

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 2, 1, 0, confirmed: true);

        Assert.Single(store.Created);
        Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
        var notice = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.RoomNotificationComposer).Body };
        Assert.Equal("furni_placement_error", notice.ReadString());
        Assert.Equal(1, notice.ReadInt());
        Assert.Equal("message", notice.ReadString());
        Assert.Equal(TypeLimit, notice.ReadString());
    }
}
