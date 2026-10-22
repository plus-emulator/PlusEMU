using Dapper;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Catalog;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Core.Settings;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Catalog.BuildersClub;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// Builders Club placement as AIR WIN63-202610151745-318700621 checks it (HabboCatalog.getBuilderFurniPlaceableStatus,
// com/sulake/habbo/catalog/HabboCatalog.as:2842-2889) and as hhus answers it: the furni count follows every placement and removal.
public partial class PlacedFurniRoomTests
{
    private const int BuildersClubPageId = 900;
    private const int BuildersClubOfferId = 35202;

    [Fact]
    public async Task BuildersClubFurniIsPlacedFromItsPageAndDeletedWhenPickedUp()
    {
        Viewer();
        var (service, store, _) = BuildersClub();
        var packet = ClientPacket(BuildersClubPageId, BuildersClubOfferId, "", 1, 1, 2, true);

        await new BuildersClubPlaceRoomItemEvent(service).Parse(_room, _client, packet);

        Assert.False(packet.HasDataRemaining());
        var placed = Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.True(placed.IsBuildersClub);
        Assert.Equal((store.Created.Single(), 7, "owner", 1, 1, 2), (placed.Id, placed.UserId, placed.Username, placed.GetX, placed.GetY, placed.Rotation));
        Assert.Equal([ServerPacketHeader.BuildersClubFurniCountComposer], _client.Sent.Where(header => header == ServerPacketHeader.BuildersClubFurniCountComposer));

        _client.Sent.Clear();
        var pickup = new RoomItemPickupService(Proxy<IGameClientManager>((method, args) => method == "GetClientByUserId" && (int)args[0]! == 7 ? _client : null),
            Proxy<IQuestManager>((_, _) => throw new InvalidOperationException("A Builders Club pickup is no quest pickup")),
            new PickupStore(_ => throw new InvalidOperationException("A Builders Club furni is never stored in an inventory")), service);
        await new PickupObjectEvent(pickup).Parse(_client, ClientPacket(2, (int)placed.Id));

        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.Equal(store.Created, store.Deleted);
        Assert.Null(Assert.IsType<InventoryComponent>(_client.GetHabbo().Inventory).Furniture.GetItem(placed.Id));
        Assert.Contains(ServerPacketHeader.BuildersClubFurniCountComposer, _client.Sent);
        Assert.DoesNotContain(ServerPacketHeader.FurniListUpdateComposer, _client.Sent);
    }

    // Observed on AIR WIN63-202610201032-107964540: the placer's new furni count comes before the furni's ObjectAdd and
    // before its ObjectRemove, in the same tick.
    [Fact]
    public async Task TheFurniCountComesBeforeThePlacedFloorFurniIsAdded()
    {
        Viewer();
        var (service, _, _) = BuildersClub();

        await new BuildersClubPlaceRoomItemEvent(service).Parse(_room, _client, ClientPacket(BuildersClubPageId, BuildersClubOfferId, "", 1, 1, 2, true));

        var placed = Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
        var wire = unchecked((int)BuildersClubItemIds.ToWire(placed.Id));
        Assert.Equal([(ServerPacketHeader.BuildersClubFurniCountComposer, 1), (ServerPacketHeader.ObjectAddComposer, wire)], CountAndFurniPackets());
    }

    [Fact]
    public async Task TheFurniCountComesBeforeThePickedUpFloorFurniIsRemoved()
    {
        Viewer();
        var (service, _, _) = BuildersClub();
        await new BuildersClubPlaceRoomItemEvent(service).Parse(_room, _client, ClientPacket(BuildersClubPageId, BuildersClubOfferId, "", 1, 1, 2, true));
        var placed = Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
        var wire = unchecked((int)BuildersClubItemIds.ToWire(placed.Id));
        _client.Packets.Clear();
        var pickup = new RoomItemPickupService(Proxy<IGameClientManager>((method, args) => method == "GetClientByUserId" && (int)args[0]! == 7 ? _client : null),
            Proxy<IQuestManager>((_, _) => throw new InvalidOperationException("A Builders Club pickup is no quest pickup")),
            new PickupStore(_ => throw new InvalidOperationException("A Builders Club furni is never stored in an inventory")), service);

        await new PickupObjectEvent(pickup).Parse(_client, ClientPacket(2, (int)placed.Id));

        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.Equal([(ServerPacketHeader.BuildersClubFurniCountComposer, 0), (ServerPacketHeader.ObjectRemoveComposer, wire)], CountAndFurniPackets());
    }

    // Observed on AIR WIN63-202610201032-107964540: with 419 furni standing, a pickup sends 418 before ObjectRemove and the next
    // placement sends 419 before ObjectAdd.
    [Fact]
    public async Task ThePlacementCountIncludesTheNewFurniAndThePickupCountLeavesItOut()
    {
        Viewer();
        var (service, store, _) = BuildersClub();
        store.Expiry = Clock.AddDays(1);
        store.Standing = 418;

        await new BuildersClubPlaceRoomItemEvent(service).Parse(_room, _client, ClientPacket(BuildersClubPageId, BuildersClubOfferId, "", 1, 1, 2, true));
        var placed = Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
        var wire = unchecked((int)BuildersClubItemIds.ToWire(placed.Id));

        Assert.Equal([(ServerPacketHeader.BuildersClubFurniCountComposer, 419), (ServerPacketHeader.ObjectAddComposer, wire)], CountAndFurniPackets());

        _client.Packets.Clear();
        var pickup = new RoomItemPickupService(Proxy<IGameClientManager>((method, args) => method == "GetClientByUserId" && (int)args[0]! == 7 ? _client : null),
            Proxy<IQuestManager>((_, _) => throw new InvalidOperationException("A Builders Club pickup is no quest pickup")),
            new PickupStore(_ => throw new InvalidOperationException("A Builders Club furni is never stored in an inventory")), service);
        await new PickupObjectEvent(pickup).Parse(_client, ClientPacket(2, (int)placed.Id));

        Assert.Equal([(ServerPacketHeader.BuildersClubFurniCountComposer, 418), (ServerPacketHeader.ObjectRemoveComposer, wire)], CountAndFurniPackets());
        Assert.Equal(418, store.FurniCount(7));
    }

    // The same placement and pickup with the stores that write MariaDB.
    [RoomComponentDatabaseFact]
    public async Task TheStoredFurniCountsFollowThePlacementAndThePickup()
    {
        Viewer();
        await BuildersClubDatabaseTests.InSchemaAsync(async (connection, connectionString) =>
        {
            BuildersClubDatabaseTests.Migrate(connection);
            // Far from the ids other tests give ordinary furni, as in BuildersClubTestStore.
            connection.Execute("ALTER TABLE items AUTO_INCREMENT = 1950100000");
            var database = new HabbiconDatabaseTests.TestDatabase(connectionString);
            InstallRoomItemStore(new RoomItemStore(database));
            var store = new BuildersClubStore(database);
            var (service, _) = BuildersClub(store);

            await new BuildersClubPlaceRoomItemEvent(service).Parse(_room, _client, ClientPacket(BuildersClubPageId, BuildersClubOfferId, "", 1, 1, 2, true));
            var placed = Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
            var wire = unchecked((int)BuildersClubItemIds.ToWire(placed.Id));

            Assert.Equal([(ServerPacketHeader.BuildersClubFurniCountComposer, 1), (ServerPacketHeader.ObjectAddComposer, wire)], CountAndFurniPackets());
            Assert.Equal(1, store.FurniCount(7));

            _client.Packets.Clear();
            var pickup = new RoomItemPickupService(Proxy<IGameClientManager>((method, args) => method == "GetClientByUserId" && (int)args[0]! == 7 ? _client : null),
                Proxy<IQuestManager>((_, _) => throw new InvalidOperationException("A Builders Club pickup is no quest pickup")),
                new PickupStore(_ => throw new InvalidOperationException("A Builders Club furni is never stored in an inventory")), service);
            await new PickupObjectEvent(pickup).Parse(_client, ClientPacket(2, (int)placed.Id));

            Assert.Equal([(ServerPacketHeader.BuildersClubFurniCountComposer, 0), (ServerPacketHeader.ObjectRemoveComposer, wire)], CountAndFurniPackets());
            Assert.Equal(0, store.FurniCount(7));
        });
    }

    [Fact]
    public void AFailedPlacementSendsNoFurniCount()
    {
        Viewer();
        var (service, store, _) = BuildersClub();

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 40, 40, 0, confirmed: true);

        Assert.Equal(0, store.FurniCount(7));
        Assert.Empty(CountAndFurniPackets());
    }

    [Fact]
    public async Task APickupWhoseStoredRowCannotBeDeletedStillRemovesTheFurniForTheRoom()
    {
        Viewer();
        var (service, store, _) = BuildersClub();
        await new BuildersClubPlaceRoomItemEvent(service).Parse(_room, _client, ClientPacket(BuildersClubPageId, BuildersClubOfferId, "", 1, 1, 2, true));
        var placed = Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
        var wire = unchecked((int)BuildersClubItemIds.ToWire(placed.Id));
        _client.Packets.Clear();
        store.FailDelete = true;
        var pickup = new RoomItemPickupService(Proxy<IGameClientManager>((method, args) => method == "GetClientByUserId" && (int)args[0]! == 7 ? _client : null),
            Proxy<IQuestManager>((_, _) => throw new InvalidOperationException("A Builders Club pickup is no quest pickup")),
            new PickupStore(_ => throw new InvalidOperationException("A Builders Club furni is never stored in an inventory")), service);

        await Record.ExceptionAsync(() => new PickupObjectEvent(pickup).Parse(_client, ClientPacket(2, (int)placed.Id)));

        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.Contains((ServerPacketHeader.ObjectRemoveComposer, wire), CountAndFurniPackets());
    }

    [Fact]
    public void APlacementWhoseCountCannotBeReadKeepsTheRoomAndTheStoreInStep()
    {
        Viewer();
        var (service, store, _) = BuildersClub();
        store.FailFurniCountWhilePlaced = true;

        Record.Exception(() => service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 2, confirmed: true));

        Assert.Equal(store.Created.Except(store.Deleted), _room.GetRoomItemHandler().GetWallAndFloor.Select(item => item.Id));
    }

    // The furni count with its value, and the furni adds and removes with the furni id, in the order the client got them.
    private (uint Header, int Value)[] CountAndFurniPackets() => _client.Packets
        .Where(packet => packet.Header is ServerPacketHeader.BuildersClubFurniCountComposer or ServerPacketHeader.ObjectAddComposer or ServerPacketHeader.ObjectRemoveComposer)
        .Select(packet => (packet.Header, packet.Header == ServerPacketHeader.ObjectRemoveComposer
            ? int.Parse(System.Text.Encoding.UTF8.GetString(packet.Body, 2, System.Buffers.Binary.BinaryPrimitives.ReadInt16BigEndian(packet.Body)))
            : System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(packet.Body)))
        .ToArray();

    [Fact]
    public async Task ATrialPlacesOnlyWhileAloneInTheRoomAndAMemberAlsoWithVisitors()
    {
        Viewer();
        var visitor = new RoomUser(8, RoomId, 2, _room, _client, TestChatEmotions.Unused, new TestRewardProgress()) { X = 2, Y = 2 };
        RoomUsers().TryAdd(2, visitor);
        var (service, store, _) = BuildersClub();

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0, confirmed: true);

        Assert.Empty(store.Created);
        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.Contains(ServerPacketHeader.RoomNotificationComposer, _client.Sent);

        store.Expiry = Clock.AddDays(1);
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0, confirmed: true);

        Assert.Single(store.Created);
        Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
    }

    [Fact]
    public void OnlyTheRoomOwnerPlacesBuildersClubFurni()
    {
        Viewer();
        _room.OwnerName = "someone else";
        var (service, store, _) = BuildersClub();

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0, confirmed: true);

        Assert.Empty(store.Created);
        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
    }

    [Fact]
    public void TheFurniLimitNormalPagesAndValuableFurniStopAPlacement()
    {
        Viewer();
        var (service, store, page) = BuildersClub(trialLimit: "1");
        store.Standing = 1;

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0, confirmed: true);
        Assert.Empty(store.Created);

        store.Standing = 0;
        page.CatalogMode = CatalogModes.Normal;
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0, confirmed: true);
        Assert.Empty(store.Created);

        page.CatalogMode = CatalogModes.BuildersClub;
        page.Offers[BuildersClubOfferId].Product.Definition!.InteractionType = InteractionType.Exchange;
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0, confirmed: true);
        Assert.Empty(store.Created);
        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
    }

    // Observed on AIR WIN63-202610201032-107964540: the three contracts place from the Builders Club "Contracts" page.
    [Theory]
    [InlineData(InteractionType.WiredContractPayment)]
    [InlineData(InteractionType.WiredContractTrade)]
    [InlineData(InteractionType.WiredContractReward)]
    public async Task AContractIsPlacedFromTheBuildersClub(InteractionType contract)
    {
        Viewer();
        var (service, store, page) = BuildersClub();
        page.Offers[BuildersClubOfferId].Product.Definition!.InteractionType = contract;

        await new BuildersClubPlaceRoomItemEvent(service).Parse(_room, _client, ClientPacket(BuildersClubPageId, BuildersClubOfferId, "", 1, 1, 0, true));

        Assert.Single(store.Created);
        Assert.True(Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor).IsBuildersClub);
        Assert.Contains(ServerPacketHeader.ObjectAddComposer, _client.Sent);
        Assert.Contains(ServerPacketHeader.BuildersClubFurniCountComposer, _client.Sent);
    }

    // Observed on AIR WIN63-202610201032-107964540: a Builders Club placement of a chest gets no answer at all.
    [Theory]
    [InlineData(InteractionType.WiredChestFurni)]
    [InlineData(InteractionType.WiredChestCoins)]
    public async Task AChestIsRefusedSilentlyByTheBuildersClub(InteractionType chest)
    {
        Viewer();
        var (service, store, page) = BuildersClub();
        page.Offers[BuildersClubOfferId].Product.Definition!.InteractionType = chest;

        await new BuildersClubPlaceRoomItemEvent(service).Parse(_room, _client, ClientPacket(BuildersClubPageId, BuildersClubOfferId, "", 1, 1, 0, true));

        Assert.Empty(store.Created);
        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.Empty(_client.Sent);
    }

    [Fact]
    public void TheInfoStandCopyNamesTheBuildersClubOfferWithoutAPage()
    {
        Viewer();
        var (service, store, _) = BuildersClub();

        service.PlaceFloorItem(_room, _client, new(-1, BuildersClubOfferId, ""), 1, 1, 0, confirmed: true);

        Assert.Single(store.Created);
        Assert.True(Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor).IsBuildersClub);
    }

    [Fact]
    public void PickAllBuildersClubDeletesEveryBuildersClubFurniAndPickAllLeavesThem()
    {
        // AIR WIN63-202610151745-318700621 com/sulake/habbo/ui/handler/ChatInputWidgetHandler.as:325-327 says ":pickallbc" once confirmed.
        Viewer();
        var (service, store, _) = BuildersClub();
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0, confirmed: true);
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 2, 1, 0, confirmed: true);
        Assert.Equal(2, _room.GetRoomItemHandler().GetWallAndFloor.Count(item => item.IsBuildersClub));

        new Plus.HabboHotel.Rooms.Chat.Commands.User.PickAllCommand(null!, Proxy<IRoomItemPickupService>((_, _) => throw new InvalidOperationException("no music")))
            .Execute(_client, _room, []);
        Assert.Equal(2, _room.GetRoomItemHandler().GetWallAndFloor.Count(item => item.IsBuildersClub));

        new Plus.HabboHotel.Rooms.Chat.Commands.User.PickAllBuildersClubCommand(service).Execute(_client, _room, []);

        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.Equal(store.Created, store.Deleted);
    }

    [Fact]
    public async Task BuildersClubFurniTravelsByItsBandIdAndIsPickedUpByIt()
    {
        // AIR WIN63-202610151745-318700621 com/sulake/habbo/utils/class_1858.as:21-24: the infostand knows a Builders Club furni only
        // by an object id in this band (InfoStandFurniView.as:598-635).
        Viewer();
        var (service, store, _) = BuildersClub();

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0, confirmed: true);

        var placed = Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
        var wire = BuildersClubItemIds.ToWire(placed.Id);
        Assert.True(BuildersClubItemIds.Contains(wire));
        Assert.Same(placed, _room.GetRoomItemHandler().GetItem(wire));
        // Ordinary and temporary furni keep their ids both ways.
        Assert.Equal(42u, BuildersClubItemIds.ToWire(42u));
        Assert.Equal(42u, BuildersClubItemIds.FromWire(42u));
        Assert.Equal(TemporaryItemIds.First, BuildersClubItemIds.ToWire(TemporaryItemIds.First));
        Assert.Equal(TemporaryItemIds.First, BuildersClubItemIds.FromWire(TemporaryItemIds.First));

        using (var reader = new BinaryReader(new MemoryStream(TypedFurnitureStateTests.Payload(new ObjectAddComposer(RoomItemSnapshot.Capture(placed)))))) {
            Assert.Equal(unchecked((int)wire), TypedFurnitureStateTests.ReadInt(reader));
        }

        var pickup = new RoomItemPickupService(Proxy<IGameClientManager>((method, args) => method == "GetClientByUserId" && (int)args[0]! == 7 ? _client : null),
            Proxy<IQuestManager>((_, _) => throw new InvalidOperationException("A Builders Club pickup is no quest pickup")),
            new PickupStore(_ => throw new InvalidOperationException("A Builders Club furni is never stored in an inventory")), service);
        await new PickupObjectEvent(pickup).Parse(_client, ClientPacket(2, unchecked((int)wire)));

        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.Equal(store.Created, store.Deleted);
        Assert.Equal(placed.Id, BuildersClubItemIds.ToWire(placed.Id));
    }

    [Fact]
    public void ATrialPlacementAsksBeforeItHidesTheRoomAndAMemberNeverHidesIt()
    {
        // AIR WIN63-202610151745-318700621 com/sulake/habbo/room/class_2005.as:366-397: BuildersClubPlacementWarning (2075) asks
        // room.confirm.hide_room and the client sends the placement again confirmed.
        Viewer();
        var (service, store, _) = BuildersClub();

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, "x"), 1, 1, 2);

        Assert.Empty(store.Created);
        Assert.Contains(ServerPacketHeader.BuildersClubPlacementWarningComposer, _client.Sent);

        using (var reader = new BinaryReader(new MemoryStream(TypedFurnitureStateTests.Payload(
                   Plus.Communication.Packets.Outgoing.Catalog.BuildersClubPlacementWarningComposer.Floor(BuildersClubPageId, BuildersClubOfferId, "x", 1, 1, 2))))) {
            Assert.Equal((0, BuildersClubPageId, BuildersClubOfferId, "x", 1, 1, 2), (TypedFurnitureStateTests.ReadInt(reader), TypedFurnitureStateTests.ReadInt(reader),
                TypedFurnitureStateTests.ReadInt(reader), TypedFurnitureStateTests.ReadString(reader), TypedFurnitureStateTests.ReadInt(reader),
                TypedFurnitureStateTests.ReadInt(reader), TypedFurnitureStateTests.ReadInt(reader)));
        }

        Assert.False(service.HidesRoom(_room));
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, "x"), 1, 1, 2, confirmed: true);
        Assert.Single(store.Created);
        Assert.True(service.HidesRoom(_room));

        // Once hidden, a further trial placement asks nothing.
        _client.Sent.Clear();
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, "x"), 2, 1, 2);
        Assert.Equal(2, store.Created.Count);
        Assert.DoesNotContain(ServerPacketHeader.BuildersClubPlacementWarningComposer, _client.Sent);

        store.Expiry = Clock.AddDays(1);
        Assert.False(service.HidesRoom(_room));
    }

    [Fact]
    public void AFailedPlacementDeletesTheStoredFurni()
    {
        Viewer();
        var (service, store, _) = BuildersClub();

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 40, 40, 0, confirmed: true);

        Assert.Single(store.Created);
        Assert.Equal(store.Created, store.Deleted);
        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
    }

    // Observed on AIR WIN63-202610201032-107964540: a poster from the Builders Club takes its number from the offer's product,
    // whatever extra param the client sends.
    [Theory]
    [InlineData("")]
    [InlineData("53")]
    public async Task ABuildersClubPosterTakesItsNumberFromTheOffer(string clientExtraParam)
    {
        Viewer();
        var (service, store, page) = BuildersClub();
        var poster = Furni(0, InteractionType.None, WiredBoxType.None, ItemType.Wall).Definition;
        poster.Id = 4001;
        poster.ItemName = "poster";
        page.Offers[18498] = new CatalogOffer
        {
            Id = 18498,
            Products = [new CatalogProduct { Type = CatalogProductType.Furni, Definition = poster, Amount = 1, ExtraParam = "52" }]
        };

        await new BuildersClubPlaceWallItemEvent(service).Parse(_room, _client,
            ClientPacket(BuildersClubPageId, 18498, clientExtraParam, ":w=1,1 l=0,0 l", true));

        var placed = Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.Equal("52", placed.LegacyDataString);
        Assert.Equal("52", store.ExtraData[placed.Id]);
        var added = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ItemAddComposer).Body };
        var wireId = added.ReadString();
        added.ReadInt();
        var position = added.ReadString();
        Assert.Equal((BuildersClubItemIds.ToWire(placed.Id).ToString(), ":w=1,1 l=0,0 l", "52"), (wireId, position, added.ReadString()));
    }

    // Observed on AIR WIN63-202610201032-107964540: a cine_light1 (three states) placed from the Builders Club has data "0",
    // and clicks go "0" -> "1" -> "2" -> "0".
    [Fact]
    public async Task ABuildersClubWallLampStartsAtStateZero()
    {
        Viewer();
        var (service, store, page) = BuildersClub();
        var lamp = Furni(0, InteractionType.None, WiredBoxType.None, ItemType.Wall).Definition;
        lamp.Id = 4383;
        lamp.ItemName = "cine_light1";
        lamp.Modes = 3;
        page.Offers[18306] = new CatalogOffer
        {
            Id = 18306,
            Products = [new CatalogProduct { Type = CatalogProductType.Furni, Definition = lamp, Amount = 1 }]
        };

        await new BuildersClubPlaceWallItemEvent(service).Parse(_room, _client,
            ClientPacket(BuildersClubPageId, 18306, "", ":w=1,1 l=0,0 l", true));

        var placed = Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.Equal("0", store.ExtraData[placed.Id]);
        Assert.Equal("0", placed.LegacyDataString);
        var added = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ItemAddComposer).Body };
        added.ReadString();
        added.ReadInt();
        added.ReadString();
        Assert.Equal("0", added.ReadString());

        var use = new FurnitureUseService(new FurnitureUseStore(EditorTestSupport.UntouchableDatabase()), Proxy<IQuestManager>((_, _) => null));
        var states = new List<string>();

        for (var click = 0; click < 3; click++) {
            await new UseWallItemEvent(use).Parse(_room, _client, ClientPacket(unchecked((int)BuildersClubItemIds.ToWire(placed.Id)), 0));
            states.Add(placed.LegacyDataString);
        }

        Assert.Equal(["1", "2", "0"], states);
    }

    // Observed on AIR WIN63-202610201032-107964540: every multi-state default wall item below starts at "0" when placed from the Builders
    // Club; the two single-state ones keep an empty value. The state counts are the ones Habbo's click cycles showed.
    [Theory]
    [InlineData("cine_light2", 3, "0")]
    [InlineData("party_lights", 2, "0")]
    [InlineData("uni_fairylights", 4, "0")]
    [InlineData("torch", 2, "0")]
    [InlineData("hs_wlight", 2, "0")]
    [InlineData("window_basic", 2, "0")]
    [InlineData("party_wc_boy", 2, "0")]
    [InlineData("sb_tag1", 2, "0")]
    [InlineData("pixel_painting_blue", 3, "0")]
    [InlineData("school_chalkboard", 4, "0")]
    [InlineData("exe_map", 2, "0")]
    [InlineData("tiki_surfboard", 9, "0")]
    [InlineData("window_square", 1, "")]
    [InlineData("window_golden", 1, "")]
    public async Task ABuildersClubWallItemStartsAtZeroOnlyWhenItHasSeveralStates(string name, int states, string data)
    {
        Viewer();
        var (service, store, page) = BuildersClub();
        var wall = Furni(0, InteractionType.None, WiredBoxType.None, ItemType.Wall).Definition;
        wall.Id = 4400;
        wall.ItemName = name;
        wall.Modes = states;
        page.Offers[18400] = new CatalogOffer
        {
            Id = 18400,
            Products = [new CatalogProduct { Type = CatalogProductType.Furni, Definition = wall, Amount = 1 }]
        };

        await new BuildersClubPlaceWallItemEvent(service).Parse(_room, _client,
            ClientPacket(BuildersClubPageId, 18400, "", ":w=1,1 l=0,0 l", true));

        var placed = Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.Equal(data, store.ExtraData[placed.Id]);
    }

    private static readonly DateTimeOffset Clock = new(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private (BuildersClubService Service, BuildersClubTestStore Store, CatalogPage Page) BuildersClub(string? trialLimit = null)
    {
        var store = new BuildersClubTestStore();
        InstallRoomItemStore(store);
        var (service, page) = BuildersClub(store, trialLimit);

        return (service, store, page);
    }

    private void InstallRoomItemStore(IRoomItemStore store) =>
        Set("_roomItemHandling", new RoomItemHandling(_room, store, TestRoomItemMetadataStore.Instance, TestGameClientManager.Empty, TestLanguageManager.RoomItems,
            TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards));

    private (BuildersClubService Service, CatalogPage Page) BuildersClub(IBuildersClubStore store, string? trialLimit = null)
    {
        var definition = Furni(0, InteractionType.None, WiredBoxType.None).Definition;
        definition.Id = 19348;
        definition.ItemName = "bc_block_0_recolorable";
        var offer = new CatalogOffer
        {
            Id = BuildersClubOfferId,
            Products = [new CatalogProduct { Type = CatalogProductType.Furni, Definition = definition, Amount = 1 }]
        };
        var page = new CatalogPage { Id = BuildersClubPageId, ParentId = -1, Enabled = true, Visible = true, CatalogMode = CatalogModes.BuildersClub };
        page.Offers[offer.Id] = offer;
        var catalog = Proxy<ICatalogManager>((method, args) =>
        {
            switch (method) {
                case nameof(ICatalogManager.TryGetPage):
                    args[1] = (int)args[0]! == page.Id ? page : null;

                    return args[1] != null;
                case nameof(ICatalogManager.TryGetBuildersClubOffer):
                    var found = (int)args[0]! == offer.Id && page.IsBuildersClub;
                    args[2] = found ? page : null;
                    args[3] = found ? offer : null;

                    return found;
                default:
                    throw new InvalidOperationException(method);
            }
        });
        var settings = Proxy<ISettingsManager>((method, args) => method == nameof(ISettingsManager.GetOptionalValue) && (string)args[0]! == "builders_club.trial_furni_limit"
            ? trialLimit
            : null);
        var clients = Proxy<IGameClientManager>((method, args) => method == "GetClientByUserId" && (int)args[0]! == 7 ? _client : null);

        return (new BuildersClubService(catalog, store, settings, clients, new FixedTimeProvider(Clock), TestLogging.For<BuildersClubService>()), page);
    }

    private System.Collections.Concurrent.ConcurrentDictionary<int, RoomUser> RoomUsers() =>
        (System.Collections.Concurrent.ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager)
            .GetField("_users", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(_room.GetRoomUserManager())!;

    // Counts like BuildersClubStore.FurniCount: only furni whose stored row names a room, which the room item store writes when the
    // furni is placed. A new furni is stored without a room.
    private sealed class BuildersClubTestStore : IBuildersClubStore, IRoomItemStore
    {
        // Far from the ids other tests give ordinary furni: a Builders Club furni's band id is process-wide.
        private uint _nextId = 1_950_000_000;
        private readonly Dictionary<uint, uint> _roomIds = [];
        public DateTimeOffset? Expiry { get; set; }
        public int Standing { get; set; }
        public List<uint> Created { get; } = [];
        public List<uint> Deleted { get; } = [];
        public Dictionary<uint, string> ExtraData { get; } = [];
        public bool FailDelete { get; set; }
        public bool FailFurniCountWhilePlaced { get; set; }
        public BuildersClubMembership? Membership(int userId) => Expiry is { } expiry ? new(expiry, 0, 0) : null;
        public void Extend(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, int userId, int days, int furniLimit, DateTimeOffset now) =>
            throw new NotSupportedException();
        public bool AddFurni(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, int userId, int furni, DateTimeOffset now) =>
            throw new NotSupportedException();
        public int FurniCount(int userId) => FailFurniCountWhilePlaced && Created.Count > Deleted.Count
            ? throw new InvalidOperationException("The database is gone")
            : Standing + _roomIds.Count(row => row.Value != 0);
        public uint Create(uint definitionId, int userId, string extraData)
        {
            Created.Add(_nextId);
            ExtraData[_nextId] = extraData;
            _roomIds[_nextId] = 0;

            return _nextId++;
        }
        public void Delete(uint itemId)
        {
            if (FailDelete) {
                throw new InvalidOperationException("The database is gone");
            }

            Deleted.Add(itemId);
            _roomIds.Remove(itemId);
        }
        public void AssignOwner(uint itemId, int userId) { }
        public void ClearRoom(uint itemId) => Place(itemId, 0);
        public void SaveWallPosition(uint itemId, string wallPosition) { }
        public void SaveMoved(IReadOnlyList<RoomItemSave> items) { }
        public void PlaceFloor(uint itemId, uint roomId, int x, int y, double z, int rotation) => Place(itemId, roomId);
        public void PlaceWall(uint itemId, uint roomId, int x, int y, double z, int rotation, string wallPosition) => Place(itemId, roomId);
        private void Place(uint itemId, uint roomId)
        {
            if (_roomIds.ContainsKey(itemId)) {
                _roomIds[itemId] = roomId;
            }
        }
    }
}
