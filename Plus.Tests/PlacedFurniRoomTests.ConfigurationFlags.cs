using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private Item PlaceConfiguration(uint id, InteractionType type, bool on, int x = 1)
    {
        Viewer();
        var item = Furni(id, type, WiredBoxType.None);
        item.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = on ? "1" : "0" };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, item, x, 1, 0, true, false, false));
        _client.Packets.Clear();
        _client.Sent.Clear();

        return item;
    }

    private List<byte[]> StatesPackets() => _client.Packets.Where(packet => packet.Header == ServerPacketHeader.ConfigurationItemStatesComposer)
        .Select(packet => packet.Body).ToList();

    // Observed on AIR WIN63-202610201032-107964540: the first byte of the room flags is the hand item block, then the chooser, free
    // furni movement, invisible furni and a last byte that is always 0. The flags of the room are OR-ed.
    [Fact]
    public void TheRoomFlagsPacketHoldsTheHandItemBlockFirstAndOrsTheFlags()
    {
        PlaceConfiguration(60, InteractionType.HandItemBlocker, on: true, x: 1);
        PlaceConfiguration(61, InteractionType.ChooserDisabler, on: true, x: 2);
        PlaceConfiguration(62, InteractionType.InvisibleFurniControl, on: true, x: 3);

        ConfigurationItemStates.SendTo(_client, _room);

        Assert.Equal(new byte[] { 1, 1, 0, 1, 0 }, Assert.Single(StatesPackets()));
    }

    [Fact]
    public void ARoomEntryWithoutFlagFurniSendsAllOff()
    {
        Viewer();

        ConfigurationItemStates.SendTo(_client, _room);

        Assert.Equal(new byte[] { 0, 0, 0, 0, 0 }, Assert.Single(StatesPackets()));
    }

    // Observed on AIR WIN63-202610201032-107964540: each use of a flag furni sends the full set of flag bytes.
    [Theory]
    [InlineData(InteractionType.HandItemBlocker, new byte[] { 1, 0, 0, 0, 0 })]
    [InlineData(InteractionType.InvisibleFurniControl, new byte[] { 0, 0, 0, 1, 0 })]
    public void EachUseOfAFlagFurniFlipsItsOwnByte(InteractionType type, byte[] on)
    {
        var item = PlaceConfiguration(63, type, on: false);

        item.Interactor.OnTrigger(_client, item, 0, true);
        Assert.Equal(on, Assert.Single(StatesPackets()));

        _client.Packets.Clear();
        item.Interactor.OnWiredTrigger(item);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0 }, Assert.Single(StatesPackets()));
    }

    // Observed on AIR WIN63-202610201032-107964540: placing a furni sends no flags, except the Invisible Furni Controller.
    [Theory]
    [InlineData(InteractionType.HandItemBlocker, false)]
    [InlineData(InteractionType.ChooserDisabler, false)]
    [InlineData(InteractionType.InvisibleFurniControl, true)]
    public void OnlyPlacingTheInvisibleFurniControllerSendsTheFlags(InteractionType type, bool sends)
    {
        Viewer();
        var item = Furni(64, type, WiredBoxType.None);
        item.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        _client.Packets.Clear();

        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, item, 1, 1, 0, true, false, false));

        Assert.Equal(sends ? 1 : 0, StatesPackets().Count);
    }

    // Observed on AIR WIN63-202610201032-107964540: picking a flag furni up while it is on sends the flag still on; the client keeps
    // it until the next room entry. Picking it up while it is off sends nothing.
    [Theory]
    [InlineData(InteractionType.HandItemBlocker, new byte[] { 1, 0, 0, 0, 0 })]
    [InlineData(InteractionType.ChooserDisabler, new byte[] { 0, 1, 0, 0, 0 })]
    public async Task PickingUpAFlagFurniWhileOnSendsTheFlagStillOn(InteractionType type, byte[] stale)
    {
        PlaceConfiguration(65, type, on: true);

        await PickupObject().Parse(_client, ClientPacket(0, 65));

        Assert.Null(_room.GetRoomItemHandler().GetItem(65));
        Assert.Equal(stale, Assert.Single(StatesPackets()));
        ConfigurationItemStates.SendTo(_client, _room);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0 }, StatesPackets().Last());
    }

    [Theory]
    [InlineData(InteractionType.HandItemBlocker)]
    [InlineData(InteractionType.InvisibleFurniControl)]
    [InlineData(InteractionType.WiredDisabler)]
    [InlineData(InteractionType.RollerSpeedController)]
    public async Task PickingUpAFurniWhileOffSendsNoFlags(InteractionType type)
    {
        PlaceConfiguration(66, type, on: false);

        await PickupObject().Parse(_client, ClientPacket(0, 66));

        Assert.Null(_room.GetRoomItemHandler().GetItem(66));
        Assert.Empty(StatesPackets());
    }

    // Observed on AIR WIN63-202610201032-107964540: the wired disabler and the roller speed controller are no room flags.
    [Theory]
    [InlineData(InteractionType.WiredDisabler)]
    [InlineData(InteractionType.RollerSpeedController)]
    public async Task TheWiredDisablerAndTheSpeedControllerSendNoFlagPackets(InteractionType type)
    {
        var item = PlaceConfiguration(67, type, on: true);

        item.Interactor.OnTrigger(_client, item, 0, true);
        await PickupObject().Parse(_client, ClientPacket(0, 67));

        Assert.Empty(StatesPackets());
    }

    // Observed on AIR WIN63-202610201032-107964540: a room holds one of each configuration furni. A second one is refused with the
    // type limit text.
    [Theory]
    [InlineData(InteractionType.InvisibleFurniControl)]
    [InlineData(InteractionType.RollerSpeedController)]
    [InlineData(InteractionType.HandItemBlocker)]
    [InlineData(InteractionType.WiredDisabler)]
    public async Task ASecondConfigurationFurniInTheRoomIsRefusedWithTheTypeLimit(InteractionType type)
    {
        PlaceConfiguration(68, type, on: false);
        Inventory(new InventoryItem { Id = 69, Definition = Furni(69, type, WiredBoxType.None).Definition });

        await PlaceObject().Parse(_room, _client, ClientPacket("69 2 1 0"));

        Assert.Null(_room.GetRoomItemHandler().GetItem(69));
        Assert.NotNull(Assert.IsType<InventoryComponent>(_client.GetHabbo().Inventory).Furniture.GetItem(69));
        var notice = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.RoomNotificationComposer).Body };
        Assert.Equal("furni_placement_error", notice.ReadString());
        Assert.Equal(1, notice.ReadInt());
        Assert.Equal("message", notice.ReadString());
        Assert.Equal(TypeLimit, notice.ReadString());
    }

    [Theory]
    [InlineData(InteractionType.InvisibleFurniControl)]
    [InlineData(InteractionType.RollerSpeedController)]
    public void ASecondBuildersClubConfigurationFurniIsRefusedWithTheTypeLimit(InteractionType type)
    {
        Viewer();
        var (service, store, page) = BuildersClub();
        page.Offers[BuildersClubOfferId].Product.Definition!.InteractionType = type;
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0, confirmed: true);
        Assert.Single(store.Created);
        _client.Packets.Clear();

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 2, 1, 0, confirmed: true);

        Assert.Single(store.Created);
        var notice = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.RoomNotificationComposer).Body };
        Assert.Equal("furni_placement_error", notice.ReadString());
        Assert.Equal(1, notice.ReadInt());
        Assert.Equal("message", notice.ReadString());
        Assert.Equal(TypeLimit, notice.ReadString());
    }
}
