using Plus.Communication.Packets.Incoming.Catalog;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.Core.Settings;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Catalog.BuildersClub;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Inventory;
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
        var packet = ClientPacket(BuildersClubPageId, BuildersClubOfferId, "", 1, 1, 2, false);

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

    [Fact]
    public async Task ATrialPlacesOnlyWhileAloneInTheRoomAndAMemberAlsoWithVisitors()
    {
        Viewer();
        var visitor = new RoomUser(8, RoomId, 2, _room, _client, TestChatEmotions.Unused, new TestRewardProgress()) { X = 2, Y = 2 };
        RoomUsers().TryAdd(2, visitor);
        var (service, store, _) = BuildersClub();

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0);

        Assert.Empty(store.Created);
        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.Contains(ServerPacketHeader.RoomNotificationComposer, _client.Sent);

        store.Expiry = Clock.AddDays(1);
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0);

        Assert.Single(store.Created);
        Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor);
    }

    [Fact]
    public void OnlyTheRoomOwnerPlacesBuildersClubFurni()
    {
        Viewer();
        _room.OwnerName = "someone else";
        var (service, store, _) = BuildersClub();

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0);

        Assert.Empty(store.Created);
        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
    }

    [Fact]
    public void TheFurniLimitNormalPagesAndValuableFurniStopAPlacement()
    {
        Viewer();
        var (service, store, page) = BuildersClub(trialLimit: "1");
        store.Standing = 1;

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0);
        Assert.Empty(store.Created);

        store.Standing = 0;
        page.CatalogMode = CatalogModes.Normal;
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0);
        Assert.Empty(store.Created);

        page.CatalogMode = CatalogModes.BuildersClub;
        page.Offers[BuildersClubOfferId].Product.Definition!.InteractionType = InteractionType.Exchange;
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0);
        Assert.Empty(store.Created);
        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
    }

    [Fact]
    public void TheInfoStandCopyNamesTheBuildersClubOfferWithoutAPage()
    {
        Viewer();
        var (service, store, _) = BuildersClub();

        service.PlaceFloorItem(_room, _client, new(-1, BuildersClubOfferId, ""), 1, 1, 0);

        Assert.Single(store.Created);
        Assert.True(Assert.Single(_room.GetRoomItemHandler().GetWallAndFloor).IsBuildersClub);
    }

    [Fact]
    public void PickAllBuildersClubDeletesEveryBuildersClubFurniAndPickAllLeavesThem()
    {
        // AIR WIN63-202610151745-318700621 com/sulake/habbo/ui/handler/ChatInputWidgetHandler.as:325-327 says ":pickallbc" once confirmed.
        Viewer();
        var (service, store, _) = BuildersClub();
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 1, 1, 0);
        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 2, 1, 0);
        Assert.Equal(2, _room.GetRoomItemHandler().GetWallAndFloor.Count(item => item.IsBuildersClub));

        new Plus.HabboHotel.Rooms.Chat.Commands.User.PickAllCommand(null!, Proxy<IRoomItemPickupService>((_, _) => throw new InvalidOperationException("no music")))
            .Execute(_client, _room, []);
        Assert.Equal(2, _room.GetRoomItemHandler().GetWallAndFloor.Count(item => item.IsBuildersClub));

        new Plus.HabboHotel.Rooms.Chat.Commands.User.PickAllBuildersClubCommand(service).Execute(_client, _room, []);

        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
        Assert.Equal(store.Created, store.Deleted);
    }

    [Fact]
    public void AFailedPlacementDeletesTheStoredFurni()
    {
        Viewer();
        var (service, store, _) = BuildersClub();

        service.PlaceFloorItem(_room, _client, new(BuildersClubPageId, BuildersClubOfferId, ""), 40, 40, 0);

        Assert.Single(store.Created);
        Assert.Equal(store.Created, store.Deleted);
        Assert.Empty(_room.GetRoomItemHandler().GetWallAndFloor);
    }

    private static readonly DateTimeOffset Clock = new(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private (BuildersClubService Service, BuildersClubTestStore Store, CatalogPage Page) BuildersClub(string? trialLimit = null)
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
        var store = new BuildersClubTestStore();
        var clients = Proxy<IGameClientManager>((method, args) => method == "GetClientByUserId" && (int)args[0]! == 7 ? _client : null);

        return (new BuildersClubService(catalog, store, settings, clients, new FixedTimeProvider(Clock), TestLogging.For<BuildersClubService>()), store, page);
    }

    private System.Collections.Concurrent.ConcurrentDictionary<int, RoomUser> RoomUsers() =>
        (System.Collections.Concurrent.ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager)
            .GetField("_users", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(_room.GetRoomUserManager())!;

    private sealed class BuildersClubTestStore : IBuildersClubStore
    {
        private uint _nextId = 5000;
        public DateTimeOffset? Expiry { get; set; }
        public int Standing { get; set; }
        public List<uint> Created { get; } = [];
        public List<uint> Deleted { get; } = [];
        public BuildersClubMembership? Membership(int userId) => Expiry is { } expiry ? new(expiry, 0, 0) : null;
        public void Extend(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, int userId, int days, int furniLimit, DateTimeOffset now) =>
            throw new NotSupportedException();
        public bool AddFurni(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, int userId, int furni, DateTimeOffset now) =>
            throw new NotSupportedException();
        public int FurniCount(int userId) => Standing + Created.Count - Deleted.Count;
        public uint Create(uint definitionId, int userId, string extraData)
        {
            Created.Add(_nextId);

            return _nextId++;
        }
        public void Delete(uint itemId) => Deleted.Add(itemId);
    }
}
