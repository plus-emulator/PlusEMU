using System.Buffers.Binary;
using System.Text;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Incoming.Rooms.Furni.MysteryBoxes;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.MysteryBoxes;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Plus.HabboHotel.Permissions;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint MysteryBoxId = 60;
    private const int MysteryPrizeSprite = 4321;

    // Box states: 0 inactive, then three states per colour in the order purple, blue, green, yellow, lilac, orange, turquoise, red.
    // Blue is the second colour: 4 locked, 5 unlocked by a guest key, 6 opened.
    private const string BlueLocked = "4";
    private const string BlueUnlocked = "5";
    private const string BlueOpened = "6";

    [Fact]
    public async Task TheOwnerWaitsForAKeyAndTheBoxStaysLocked()
    {
        var world = PrepareMysteryBox(BlueLocked);

        await world.Use(_client);

        Assert.Empty(MysteryBody(_client, ServerPacketHeader.ShowMysteryBoxWaitComposer));
        Assert.Equal(BlueLocked, world.Box.LegacyDataString);
        Assert.DoesNotContain(ServerPacketHeader.GotMysteryBoxPrizeComposer, _client.Sent);
        Assert.Empty(world.Store.Opened);
    }

    [Fact]
    public async Task AGuestWithTheRightKeyUnlocksTheBoxAndWaitsForTheOwner()
    {
        var world = PrepareMysteryBox(BlueLocked);

        await world.Use(world.Guest);

        Assert.Empty(MysteryBody(world.Guest, ServerPacketHeader.ShowMysteryBoxWaitComposer));
        Assert.Equal(BlueUnlocked, world.Box.LegacyDataString);
        Assert.Contains(ServerPacketHeader.ObjectDataUpdateComposer, _client.Sent);
        Assert.DoesNotContain(ServerPacketHeader.ShowMysteryBoxWaitComposer, _client.Sent);
        Assert.DoesNotContain(ServerPacketHeader.GotMysteryBoxPrizeComposer, world.Guest.Sent);
    }

    [Fact]
    public async Task TheOwnerOpensABoxAGuestUnlocked()
    {
        var world = PrepareMysteryBox(BlueLocked);

        await world.Use(world.Guest);
        await world.Use(_client);

        AssertOpened(world);
    }

    [Fact]
    public async Task AGuestWithTheRightKeyOpensTheBoxTheOwnerWaitsAt()
    {
        var world = PrepareMysteryBox(BlueLocked);

        await world.Use(_client);
        await world.Use(world.Guest);

        AssertOpened(world);
    }

    [Theory]
    [InlineData(true, "", "", "0", "inactive.owner")]
    [InlineData(false, "", "blue", "0", "inactive.guest")]
    [InlineData(false, "blue", "", BlueLocked, "no_key")]
    [InlineData(false, "blue", "red", BlueLocked, "wrong_key")]
    [InlineData(true, "blue", "blue", BlueOpened, "already_opened.owner")]
    [InlineData(false, "blue", "blue", BlueOpened, "already_opened.guest")]
    public async Task AMysteryBoxThatCannotBeUsedTellsWhy(bool byOwner, string ownerBox, string guestKey, string state, string error)
    {
        var world = PrepareMysteryBox(state, ownerBox, guestKey);
        var actor = byOwner ? _client : world.Guest;

        await world.Use(actor);

        Assert.Equal(Utf("mysterybox.error." + error).Concat(Ints(0)), MysteryBody(actor, ServerPacketHeader.RoomNotificationComposer));
        Assert.DoesNotContain(ServerPacketHeader.ShowMysteryBoxWaitComposer, actor.Sent);
        Assert.Equal(state, world.Box.LegacyDataString);
        Assert.Empty(world.Store.Opened);
    }

    [Fact]
    public async Task ASecondGuestCannotUseABoxAnotherGuestUnlocked()
    {
        var world = PrepareMysteryBox(BlueLocked);
        var second = AddMysteryGuest(9, "second");
        world.Store.Users[9] = new("", "blue");

        await world.Use(world.Guest);
        await world.Use(second);

        Assert.Equal(Utf("mysterybox.error.already_unlocked.guest").Concat(Ints(0)), MysteryBody(second, ServerPacketHeader.RoomNotificationComposer));
        Assert.DoesNotContain(ServerPacketHeader.ShowMysteryBoxWaitComposer, second.Sent);
        Assert.Equal(BlueUnlocked, world.Box.LegacyDataString);
    }

    [Fact]
    public async Task AGuestWhoCancelsTakesTheKeyOutAgain()
    {
        var world = PrepareMysteryBox(BlueLocked);

        await world.Use(world.Guest);
        await world.Cancel(world.Guest, 7);
        Assert.Equal(BlueLocked, world.Box.LegacyDataString);

        await world.Use(_client);

        Assert.Contains(ServerPacketHeader.ShowMysteryBoxWaitComposer, _client.Sent);
        Assert.DoesNotContain(ServerPacketHeader.GotMysteryBoxPrizeComposer, _client.Sent);
        Assert.Empty(world.Store.Opened);
    }

    [Fact]
    public async Task AnOwnerWhoCancelsNoLongerWaits()
    {
        var world = PrepareMysteryBox(BlueLocked);

        await world.Use(_client);
        await world.Cancel(_client, 7);
        await world.Use(world.Guest);

        Assert.Contains(ServerPacketHeader.ShowMysteryBoxWaitComposer, world.Guest.Sent);
        Assert.Equal(BlueUnlocked, world.Box.LegacyDataString);
        Assert.Empty(world.Store.Opened);
    }

    [Fact]
    public async Task AGuestWhoLeftTheRoomNoLongerHoldsTheBoxOpen()
    {
        var world = PrepareMysteryBox(BlueLocked);

        await world.Use(world.Guest);
        _room.GetRoomUserManager().RemoveUserFromRoom(world.Guest, true, false);
        _client.Sent.Clear();
        await world.Use(_client);

        Assert.Contains(ServerPacketHeader.ShowMysteryBoxWaitComposer, _client.Sent);
        Assert.DoesNotContain(ServerPacketHeader.GotMysteryBoxPrizeComposer, _client.Sent);
        Assert.Equal(BlueLocked, world.Box.LegacyDataString);
        Assert.Empty(world.Store.Opened);
    }

    [Fact]
    public async Task ABoxWithoutPrizesIsNotOpened()
    {
        var world = PrepareMysteryBox(BlueLocked);
        world.Store.PrizeTable.Clear();

        await world.Use(world.Guest);
        await world.Use(_client);

        var unexpected = Utf("mysterybox.error.unexpected").Concat(Ints(0));
        Assert.Equal(unexpected, MysteryBody(_client, ServerPacketHeader.RoomNotificationComposer));
        Assert.Equal(unexpected, MysteryBody(world.Guest, ServerPacketHeader.RoomNotificationComposer));
        Assert.DoesNotContain(ServerPacketHeader.GotMysteryBoxPrizeComposer, _client.Sent);
        Assert.Equal(BlueLocked, world.Box.LegacyDataString);
        Assert.Empty(world.Store.Opened);
    }

    [Fact]
    public void AnyOtherFurniIsLeftToTheNormalUse()
    {
        var world = PrepareMysteryBox(BlueLocked);

        Assert.False(world.Service.TryUse(_room, _client, 12345));
    }

    [Fact]
    public void UsingAMysteryBoxNeverTogglesItsState()
    {
        var world = PrepareMysteryBox(BlueLocked);

        world.Box.Interactor.OnTrigger(_client, world.Box, 0, true);

        Assert.Equal(BlueLocked, world.Box.LegacyDataString);
    }

    [Fact]
    public void MysteryBoxFurnitureUsesTheMysteryBoxInteraction() =>
        Assert.Equal(InteractionType.MysteryBox, InteractionTypes.GetTypeFromString("mystery_box"));

    private void AssertOpened(MysteryBoxWorld world)
    {
        var prize = Utf("s").Concat(Ints(MysteryPrizeSprite));
        Assert.Equal(prize, MysteryBody(_client, ServerPacketHeader.GotMysteryBoxPrizeComposer));
        Assert.Equal(prize, MysteryBody(world.Guest, ServerPacketHeader.GotMysteryBoxPrizeComposer));
        Assert.Equal(BlueOpened, world.Box.LegacyDataString);

        var opening = Assert.Single(world.Store.Opened);
        Assert.Equal((MysteryBoxId, BlueOpened, 7, 8, "blue"), (opening.BoxItemId, opening.OpenedState, opening.OwnerId, opening.GuestId, opening.KeyColor));

        // The guest's key is used up, so the tracker shows no key.
        Assert.Equal(Utf("").Concat(Utf("")), MysteryBody(world.Guest, ServerPacketHeader.MysteryBoxKeysComposer));
        Assert.DoesNotContain(ServerPacketHeader.MysteryBoxKeysComposer, _client.Sent);

        Assert.True(_client.GetHabbo().Inventory!.Furniture.Floor.ContainsKey(TestMysteryBoxStore.OwnerPrizeId));
        Assert.True(world.Guest.GetHabbo().Inventory!.Furniture.Floor.ContainsKey(TestMysteryBoxStore.GuestPrizeId));
        Assert.Contains(ServerPacketHeader.FurniListAddComposer, _client.Sent);
        Assert.Contains(ServerPacketHeader.FurniListAddComposer, world.Guest.Sent);
    }

    private MysteryBoxWorld PrepareMysteryBox(string state, string ownerBox = "blue", string guestKey = "blue")
    {
        AdmitOwner();
        _client.GetHabbo().Inventory = new InventoryComponent();
        var guest = AddMysteryGuest(8, "guest");

        var store = new TestMysteryBoxStore();
        store.Users[7] = new(ownerBox, "");
        store.Users[8] = new("", guestKey);
        var prize = new ItemDefinition { Id = 77, SpriteId = MysteryPrizeSprite, Type = ItemType.Floor, ProductType = "s", ItemName = "prize", PublicName = "" };
        store.PrizeTable.Add(new(prize, 1));
        var service = new MysteryBoxService(store);

        var box = Furni(MysteryBoxId, InteractionType.MysteryBox, WiredBoxType.None);
        box.Definition.ItemName = "mystery_box";
        box.Definition.Modes = 25;
        box.Definition.Stackable = false;
        box.ExtraData = new LegacyDataFormat { Data = state };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, box, 1, 1, 0, true, false, false));
        _client.Sent.Clear();
        _client.Packets.Clear();
        guest.Sent.Clear();
        guest.Packets.Clear();

        return new(this, store, service, box, guest);
    }

    private TestClient AddMysteryGuest(int id, string name)
    {
        var guest = new TestClient();
        guest.SetHabbo(new Habbo { Id = id, Username = name, CurrentRoom = _room, Access = UserAccess.Empty, Inventory = new InventoryComponent() });
        guest.GetHabbo().IgnoresComponent = new([]);
        Assert.True(_room.GetRoomUserManager().AddAvatarToRoom(guest));
        guest.Sent.Clear();
        guest.Packets.Clear();

        return guest;
    }

    private static byte[] MysteryBody(TestClient client, uint header) => Assert.Single(client.Packets, packet => packet.Header == header).Body;

    private static byte[] Utf(string value)
    {
        var text = Encoding.UTF8.GetBytes(value);
        var bytes = new byte[2 + text.Length];
        BinaryPrimitives.WriteInt16BigEndian(bytes, (short)text.Length);
        text.CopyTo(bytes, 2);

        return bytes;
    }

    private sealed record MysteryBoxWorld(PlacedFurniRoomTests Tests, TestMysteryBoxStore Store, MysteryBoxService Service, Item Box, TestClient Guest)
    {
        public Task Use(TestClient client) =>
            new UseFurnitureEvent(new FurnitureUseService(null!, Proxy<IQuestManager>((_, _) => null)), null, Service)
                .Parse(Tests._room, client, ClientPacket((int)MysteryBoxId, 0));

        public Task Cancel(TestClient client, int boxOwnerId) =>
            new MysteryBoxWaitingCanceledEvent(Service).Parse(Tests._room, client, ClientPacket(boxOwnerId));
    }
}
