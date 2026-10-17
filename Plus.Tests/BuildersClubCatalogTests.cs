using System.Text.Json.Nodes;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.Core.Settings;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Catalog.BuildersClub;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// The Builders Club catalogue mode of AIR WIN63-202610151745-318700621: the client asks for the NORMAL and the BUILDERS_CLUB index
// by mode and builds each in its own window (HabboCatalog.as:1744-1760, 2144-2150).
public sealed class BuildersClubCatalogTests
{
    private static readonly DateTimeOffset Now = new(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EachModeIndexesItsOwnTreeAndEndsWithItsMode()
    {
        var pages = new List<CatalogPage> { Page(1, -1, CatalogModes.Normal), Page(2, -1, CatalogModes.BuildersClub), Page(3, 2, CatalogModes.BuildersClub) };
        var snapshots = CatalogSnapshotTestSupport.Snapshots();
        var habbo = EditorTestSupport.Player();

        var builders = snapshots.CaptureIndex(habbo, pages, "BUILDERS_CLUB");
        var normal = snapshots.CaptureIndex(habbo, pages, "SOMETHING_ELSE");

        Assert.Equal("BUILDERS_CLUB", builders.Mode);
        Assert.Equal([2], builders.Roots.Select(root => root.WireId));
        Assert.Equal([3], builders.Roots[0].Children.Select(child => child.WireId));
        Assert.Equal("NORMAL", normal.Mode);
        Assert.Equal([1], normal.Roots.Select(root => root.WireId));

        var packet = new HabbiconTestSupport.RecordingPacket();
        new CatalogIndexComposer(builders).Compose(packet);
        Assert.Equal("BUILDERS_CLUB", packet.Writes[^1]);
    }

    [Fact]
    public void TheBuildersClubIndexIsFollowedByTheFurniCount()
    {
        var pages = new List<CatalogPage> { Page(1, -1, CatalogModes.Normal), Page(2, -1, CatalogModes.BuildersClub) };
        var catalog = CatalogSnapshotTestSupport.Proxy<ICatalogManager>((method, _) => method == "get_Pages" ? pages : throw new InvalidOperationException(method));
        var counted = new List<string>();
        var buildersClub = CatalogSnapshotTestSupport.Proxy<IBuildersClubService>((method, args) =>
        {
            counted.Add(method);
            ((GameClient)args[0]!).Send(new BuildersClubFurniCountComposer(3));

            return null;
        });
        var service = new CatalogBrowsingService(null!, null!, null!, TimeProvider.System, catalog, CatalogSnapshotTestSupport.Snapshots(), buildersClub);
        var (client, sent) = HabbiconTestSupport.Client(EditorTestSupport.Player());

        service.ShowMode(client, "BUILDERS_CLUB");
        service.ShowMode(client, "NORMAL");

        Assert.Equal([nameof(IBuildersClubService.SendFurniCount)], counted);
        Assert.Equal([ServerPacketHeader.CatalogIndexComposer, ServerPacketHeader.BuildersClubFurniCountComposer, ServerPacketHeader.CatalogIndexComposer],
            sent.Select(packet => packet.Header));
    }

    [Fact]
    public void APageOpensOnlyInItsOwnCatalogue()
    {
        var page = Page(7, -1, CatalogModes.BuildersClub);
        page.Layout = "default_3x3";
        var catalog = CatalogSnapshotTestSupport.Proxy<ICatalogManager>((method, args) =>
        {
            switch (method) {
                case nameof(ICatalogManager.TryGetPage):
                    args[1] = page;

                    return true;
                case "get_Promotions":
                    return Array.Empty<CatalogPromotion>();
                default:
                    throw new InvalidOperationException(method);
            }
        });
        var service = new CatalogBrowsingService(null!, null!, null!, TimeProvider.System, catalog, new CatalogSnapshotService(catalog, TimeProvider.System));
        var (client, sent) = HabbiconTestSupport.Client(EditorTestSupport.Player());

        service.ShowPage(client, new(7, -1, "NORMAL"));
        Assert.Empty(sent);

        service.ShowPage(client, new(7, -1, "BUILDERS_CLUB"));
        var (_, payload) = Assert.Single(sent);
        var expected = new HabbiconTestSupport.RecordingPacket();
        new CatalogPageComposer(new CatalogSnapshotService(catalog, TimeProvider.System).CapturePage(page, -1)).Compose(expected);
        Assert.Equal("BUILDERS_CLUB", expected.Writes[1]);
        Assert.NotEmpty(payload);
    }

    [Fact]
    public void OffersAreFoundInTheCatalogueOfTheirPage()
    {
        var normal = Page(1, -1, CatalogModes.Normal);
        normal.Offers[10] = new CatalogOffer { Id = 10 };
        var builders = Page(2, -1, CatalogModes.BuildersClub);
        builders.Offers[20] = new CatalogOffer { Id = 20 };
        builders.Offers[10] = new CatalogOffer { Id = 10 };
        var index = new CatalogOfferIndex();
        index.Build([normal, builders]);
        var habbo = EditorTestSupport.Player();

        Assert.True(index.TryGet(10, habbo, out var page, out _));
        Assert.Same(normal, page);
        Assert.False(index.TryGet(20, habbo, out _, out _));
        Assert.True(index.TryGetBuildersClub(20, habbo, out page, out _));
        Assert.Same(builders, page);
        Assert.True(index.TryGetBuildersClub(10, habbo, out page, out _));
        Assert.Same(builders, page);
    }

    [Fact]
    public void StatusAndFurniCountGoOutAsAirReadsThem()
    {
        var status = new HabbiconTestSupport.RecordingPacket();
        new BuildersClubSubscriptionStatusComposer(new BuildersClubStatus(617961, 500, 2000000, 617961)).Compose(status);
        var count = new HabbiconTestSupport.RecordingPacket();
        new BuildersClubFurniCountComposer(162).Compose(count);

        Assert.Equal(new object[] { 617961, 500, 2000000, 617961 }, status.Writes);
        Assert.Equal(new object[] { 162 }, count.Writes);
        Assert.Equal(1910u, ServerPacketHeader.BuildersClubSubscriptionStatusComposer);
        Assert.Equal(3928u, ServerPacketHeader.BuildersClubFurniCountComposer);
        Assert.Equal(141u, Communication.Packets.Incoming.ClientPacketHeader.BuildersClubPlaceRoomItemEvent);
        Assert.Equal(3269u, Communication.Packets.Incoming.ClientPacketHeader.BuildersClubPlaceWallItemEvent);
    }

    [Fact]
    public void AMemberHasTheMemberLimitAndATrialUserTheTrialLimit()
    {
        var store = new Store();
        var settings = new Dictionary<string, string>();
        var service = Service(store, settings);
        var habbo = EditorTestSupport.Player();

        Assert.Equal(new BuildersClubStatus(0, BuildersClubService.DefaultTrialFurniLimit, BuildersClubService.DefaultMaxFurniLimit, 0), service.Status(habbo));

        store.Expiry = Now.AddSeconds(617961);
        Assert.Equal(new BuildersClubStatus(617961, 500, 2000000, 617961), service.Status(habbo));

        settings["builders_club.grace_seconds"] = "60";
        settings["builders_club.furni_limit"] = "1000";
        store.Expiry = Now.AddSeconds(-30);
        var grace = service.Status(habbo);
        Assert.Equal(new BuildersClubStatus(0, 1000, 2000000, 30), grace);
        Assert.False(grace.IsMember);
    }

    [Fact]
    public void ABuildersClubFurniNeverEntersAnInventory()
    {
        var inventory = new FurnitureInventoryComponent([], []);
        var definition = new ItemDefinition { Type = ItemType.Floor };

        Assert.False(inventory.AddItem(new InventoryItem { Id = 1, Definition = definition, IsBuildersClub = true }));
        Assert.True(inventory.AddItem(new Item { Id = 2, Definition = definition }.ToInventoryItem()));
        Assert.False(inventory.AddItem(new Item { Id = 3, Definition = definition, IsBuildersClub = true }.ToInventoryItem()));
        Assert.Equal([2u], inventory.AllItems.Select(item => item.Id));
    }

    [Fact]
    public void FurnidataNamesTheBuildersClubOfferApartFromThePurchaseOffer()
    {
        var block = new ItemDefinition { Id = 1, ItemName = "bc_block_0_recolorable", ProductType = "s" };
        var chair = new ItemDefinition { Id = 2, ItemName = "chair", ProductType = "s" };
        var normal = Page(1, -1, CatalogModes.Normal);
        normal.Offers[10] = Offer(10, block);
        normal.Offers[11] = Offer(11, chair);
        var builders = Page(2, -1, CatalogModes.BuildersClub);
        builders.Offers[35202] = Offer(35202, block);
        var root = new JsonObject
        {
            ["roomitemtypes"] = new JsonObject
            {
                ["furnitype"] = new JsonArray(new JsonObject { ["classname"] = "bc_block_0_recolorable" }, new JsonObject { ["classname"] = "chair" })
            },
            ["wallitemtypes"] = new JsonObject { ["furnitype"] = new JsonArray() }
        };

        CatalogFurnidata.Overlay(root, [normal, builders]);

        var entries = root["roomitemtypes"]!["furnitype"]!.AsArray();
        Assert.Equal((10, true, 35202), ((int)entries[0]!["offerid"]!, (bool)entries[0]!["bc"]!, (int)entries[0]!["bcofferid"]!));
        Assert.Equal((11, false, -1), ((int)entries[1]!["offerid"]!, (bool)entries[1]!["bc"]!, (int)entries[1]!["bcofferid"]!));
    }

    [Fact]
    public void BoughtPackagesRaiseTheLimitAndAddonsNeedARunningMembership()
    {
        var store = new Store();
        var service = Service(store, []);
        var habbo = EditorTestSupport.Player();
        var month = new CatalogProduct { Type = CatalogProductType.BuildersClub, Amount = 31, ExtraParam = "1000" };
        var addon = new CatalogProduct { Type = CatalogProductType.BuildersClubAddon, Amount = 45 };

        Assert.True(service.CanBuy(habbo, month));
        Assert.False(service.CanBuy(habbo, addon));
        Assert.False(service.CanBuy(habbo, new CatalogProduct { Type = CatalogProductType.Furni }));
        Assert.True(service.Deliver(null!, null!, habbo, month));
        Assert.Equal([(31, 1000, Now)], store.Extended);

        store.Expiry = Now.AddDays(31);
        store.FurniLimit = 1000;
        store.FurniAddon = 45;
        Assert.True(service.CanBuy(habbo, addon));
        Assert.True(service.Deliver(null!, null!, habbo, addon));
        Assert.Equal([45], store.Added);
        Assert.Equal(new BuildersClubStatus(31 * 86400, 1045, 2000000, 31 * 86400), service.Status(habbo));
    }

    [Fact]
    public void MembershipOffersGoOutWithoutProducts()
    {
        var snapshot = CatalogSnapshotTestSupport.Snapshots().CaptureOffer(new CatalogOffer
        {
            Id = 13275,
            LocalizationKey = "builders_club_31_days",
            CostCredits = 100,
            BulkPurchase = false,
            Products = [new CatalogProduct { Type = CatalogProductType.BuildersClub, Amount = 31, ExtraParam = "1000" }]
        });

        Assert.Equal("builders_club_31_days", snapshot.LocalizationId);
        Assert.Empty(snapshot.Products);
        Assert.False(snapshot.CanSelectAmount);
        Assert.False(snapshot.CanGift);
    }

    // Migration 91 mirrors hhus' BUILDERS_CLUB tree with Habbo's page ids; the fresh install carries it.
    [Fact]
    public void TheFreshInstallCarriesHabbosBuildersClubTree()
    {
        var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/91_BuildersClubCatalogTree.sql"));
        var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));

        Assert.Contains("-- 173 pages (8 headings without a page), 4787 offers, 5665 page offers, 4711 furni.", migration);
        Assert.Contains("(2129066,NULL,'Furni',2,NULL,0,'furni','default_3x3','BUILDERS_CLUB',1,1,0)", fresh);
        Assert.Contains("(2129110,2129066,'Building Blocks',28,NULL,0,'bc_blocks_recolorable','recolorable_furniture','BUILDERS_CLUB',1,1,0)", fresh);
        Assert.Contains("(2100000001,2129066,'Hot now',195,NULL,2,'2020_hotnow','default_3x3','BUILDERS_CLUB',1,0,0)", fresh);
        Assert.Contains("'builders_club_loyalty','NORMAL',1,1,0)", fresh);
    }

    // Migration 92 adds the Builders Club offers Habbo's furnidata names (bcofferid) on no index page, such as the coloured blocks.
    [Fact]
    public void TheFreshInstallCarriesTheBuildersClubOffersOnlyFurnidataNames()
    {
        var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/92_BuildersClubFurnidataOffers.sql"));
        var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));

        Assert.Contains("2334 offers on one hidden, enabled Builders Club page (1 bundle left out)", migration);
        Assert.Contains("    (13169, 'bc_block_0*1', 's', ", migration);
        Assert.Contains("(2100000100,NULL,'Builders Club furni',1,NULL,99,'bc_furnidata_offers','default_3x3','BUILDERS_CLUB',0,1,0)", fresh);
        Assert.Contains("(2100000100,13169,", fresh);
    }

    private static CatalogOffer Offer(int id, ItemDefinition definition) => new()
    {
        Id = id,
        Products = [new CatalogProduct { Type = CatalogProductType.Furni, Definition = definition, Amount = 1 }]
    };

    private static CatalogPage Page(int id, int parentId, string mode) => new()
    {
        Id = id,
        ParentId = parentId,
        Enabled = true,
        Visible = true,
        Link = "page" + id,
        Caption = "Page " + id,
        Layout = "default_3x3",
        CatalogMode = mode
    };

    private static BuildersClubService Service(Store store, Dictionary<string, string> settings) => new(null!, store,
        CatalogSnapshotTestSupport.Proxy<ISettingsManager>((method, args) => method == nameof(ISettingsManager.GetOptionalValue) ? settings.GetValueOrDefault((string)args[0]!) : null),
        null!, new FixedTimeProvider(Now), TestLogging.For<BuildersClubService>());

    private sealed class Store : IBuildersClubStore
    {
        public DateTimeOffset? Expiry { get; set; }
        public int FurniLimit { get; set; }
        public int FurniAddon { get; set; }
        public List<(int Days, int FurniLimit, DateTimeOffset Now)> Extended { get; } = [];
        public List<int> Added { get; } = [];
        public BuildersClubMembership? Membership(int userId) => Expiry is { } expiry ? new(expiry, FurniLimit, FurniAddon) : null;
        public void Extend(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, int userId, int days, int furniLimit, DateTimeOffset now) =>
            Extended.Add((days, furniLimit, now));
        public bool AddFurni(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, int userId, int furni, DateTimeOffset now)
        {
            Added.Add(furni);

            return Expiry > now;
        }
        public int FurniCount(int userId) => 0;
        public uint Create(uint definitionId, int userId, string extraData) => throw new NotSupportedException();
        public void Delete(uint itemId) => throw new NotSupportedException();
    }
}
