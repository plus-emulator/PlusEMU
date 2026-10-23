using System.Data;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Catalog;
using Plus.Communication.Packets.Outgoing;
using Plus.Core;
using Plus.Core.Settings;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Groups.Forums;
using Plus.HabboHotel.Habbicons;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Subscriptions;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Effects;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Badges;
using Plus.HabboHotel.Users.Inventory.Bots;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Plus.HabboHotel.Users.Inventory.Pets;
using Xunit;

namespace Plus.Tests;

public sealed class CatalogPurchaseServiceTests
{
    [Fact]
    public async Task HandlerDecodesAllFieldsBeforeDelegating()
    {
        var purchases = new RecordingPurchaseService();
        var habbo = Habbo();
        var (client, _) = HabbiconTestSupport.Client(habbo);

        await new PurchaseFromCatalogEvent(purchases).Parse(
            client, HabbiconTestSupport.Incoming(12, 34, "extra", 7));

        Assert.Equal(new CatalogPurchaseRequest(12, 34, "extra", 7), purchases.Request);
    }

    [Fact]
    public async Task DisabledCatalogDoesNotChargeOrCreateFurniture()
    {
        var context = Context(enabled: false);

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal(0, context.Rewards.Charges);
        Assert.Equal(0, context.Factory.Creates);
        Assert.Empty(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.AllItems);
        Assert.Empty(context.Tracks.Calls);
    }

    [Fact]
    public async Task FailedWalletChargeDoesNotCreateOrPublishFurniture()
    {
        var context = Context(chargeSucceeds: false);

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal(1, context.Rewards.Charges);
        Assert.Equal(0, context.Factory.Creates);
        Assert.Empty(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.AllItems);
        Assert.Empty(context.Tracks.Calls);
        Assert.DoesNotContain(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
    }

    [Fact]
    public async Task OrdinaryPurchaseChargesCreatesAndPublishesInventoryItem()
    {
        var context = Context();

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal(90, context.Habbo.Credits);
        Assert.Equal(1, context.Factory.Creates);
        Assert.NotNull(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.GetItem(700));
        Assert.Equal((context.Client, Plus.HabboHotel.Quests.RewardTrackActions.BuyFromCatalogue, 1),
            Assert.Single(context.Tracks.Calls));
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.FurniListUpdateComposer);
    }

    [Theory]
    [InlineData(InteractionType.Teleport)]
    [InlineData(InteractionType.WiredRoomLinker)]
    public async Task TeleportersAndRoomLinkersAreBoughtAsOneLinkedPair(InteractionType interaction)
    {
        var context = Context(interaction: interaction);

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal(1, context.Factory.LinkedPairs);
        Assert.Empty(context.Factory.ExtraData);
        var inventory = Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture;
        Assert.NotNull(inventory.GetItem(700));
        Assert.NotNull(inventory.GetItem(701));
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
    }

    [Theory]
    [InlineData("110", "", "110")]
    [InlineData("2.10", "999", "2.10")]
    [InlineData("", "205", "205")]
    [InlineData("", "", "0")]
    public async Task RoomDecorationPurchaseStoresTheOfferPattern(string offerParam, string clientData, string stored)
    {
        var context = Context(interaction: InteractionType.Floor, extraParam: offerParam);

        await context.Service.Purchase(context.Client, new(1, 2, clientData, 1));

        Assert.Equal(stored, Assert.Single(context.Factory.ExtraData));
    }

    // The client only offers the class's palette, so a colour outside it is bought in the palette's first colour.
    [Theory]
    [InlineData("bc_block_0_recolorable", "0,133,255", "0,133,255")]
    [InlineData("bc_block_0_recolorable", "1,2,3", "255,255,255")]
    [InlineData("bc_block_0_recolorable", "", "255,255,255")]
    [InlineData("bc_block_grass_recolorable", "106,177,142", "106,177,142")]
    [InlineData("bc_block_grass_recolorable", "255,255,255", "176,177,174")]
    public async Task RecolorablePurchaseKeepsOnlyAColourFromTheClassPalette(string itemName, string clientData, string stored)
    {
        var context = Context(interaction: InteractionType.Recolorable, itemName: itemName);

        await context.Service.Purchase(context.Client, new(1, 2, clientData, 1));

        Assert.Equal(stored, Assert.Single(context.Factory.ExtraData));
    }

    [Fact]
    public async Task SeasonalCurrencyOfferChargesItsOwnTypeAndPublishesThatBalance()
    {
        var context = Context(points: 4, pointsType: 104);
        context.Habbo.Currencies[104] = 3;

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal(0, context.Rewards.Charges);
        Assert.Equal((100, 3), (context.Habbo.Credits, context.Habbo.Currencies[104]));
        context.Habbo.Currencies[104] = 10;

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal((90, 6, 0, 0), (context.Habbo.Credits, context.Habbo.Currencies[104], context.Habbo.Duckets, context.Habbo.Diamonds));
        var notification = new FlashIncomingPacket { Buffer = context.Sent.Single(packet => packet.Header == ServerPacketHeader.HabboActivityPointNotificationComposer).Payload };
        Assert.Equal((6, -4, 104), (notification.ReadInt(), notification.ReadInt(), notification.ReadInt()));
        var confirmation = new FlashIncomingPacket { Buffer = context.Sent.Single(packet => packet.Header == ServerPacketHeader.PurchaseOKComposer).Payload };
        Assert.Equal((2, "chair", false, 10, 4, 104), (confirmation.ReadInt(), confirmation.ReadString(), confirmation.ReadBool(), confirmation.ReadInt(), confirmation.ReadInt(), confirmation.ReadInt()));
    }

    [Fact]
    public async Task FurnitureBatchRewardsOnceAfterChargeAndInventoryPublication()
    {
        var context = Context();

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 3));

        Assert.Equal(70, context.Habbo.Credits);
        Assert.Equal(3, context.Factory.Creates);
        Assert.Equal(3, Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.AllItems.Count());
        Assert.Equal((context.Client, Plus.HabboHotel.Quests.RewardTrackActions.BuyFromCatalogue, 1),
            Assert.Single(context.Tracks.Calls));
    }

    [Fact]
    public async Task FactoryFailureDoesNotPublishFurniture()
    {
        var context = Context(factorySucceeds: false);

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal(1, context.Factory.Creates);
        Assert.Empty(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.AllItems);
        Assert.DoesNotContain(context.Sent, packet => packet.Header == ServerPacketHeader.FurniListNotificationComposer);
        Assert.Empty(context.Tracks.Calls);
    }

    [Fact]
    public async Task BotPurchaseCreatesInsideChargeBeforePublishingInOrder()
    {
        var context = Context(bot: true);
        context.BotStore.BeforeCreate = () =>
        {
            Assert.Equal(100, context.Habbo.Credits);
            Assert.Empty(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Bots.Bots);
            Assert.Empty(context.Sent);
        };

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal(1, context.Rewards.Charges);
        Assert.Same(context.Rewards.Connection, context.BotStore.Connection);
        Assert.Empty(context.Tracks.Calls);
        Assert.Same(context.Rewards.Transaction, context.BotStore.Transaction);
        Assert.Equal((50u, 42), (context.BotStore.Preset!.Id, context.BotStore.OwnerId));
        Assert.Equal(90, context.Habbo.Credits);
        Assert.Single(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Bots.Bots);
        Assert.Equal(new[]
        {
            ServerPacketHeader.CreditBalanceComposer,
            ServerPacketHeader.BotInventoryComposer,
            ServerPacketHeader.FurniListNotificationComposer,
            ServerPacketHeader.PurchaseOKComposer,
            ServerPacketHeader.FurniListUpdateComposer
        }, context.Sent.Select(packet => packet.Header));
        var notification = new Plus.Communication.Flash.FlashIncomingPacket { Buffer = context.Sent[2].Payload };
        Assert.Equal(1, notification.ReadInt());
        Assert.Equal(5, notification.ReadInt());
        Assert.Equal(1, notification.ReadInt());
        Assert.Equal(701u, notification.ReadUInt());
    }

    [Fact]
    public async Task BotAmountIsIgnoredSoOneBotCostsOneBot()
    {
        var context = Context(bot: true);

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 7));

        Assert.Equal(90, context.Habbo.Credits);
        Assert.Equal(1, context.BotStore.Creates);
        Assert.Single(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Bots.Bots);
    }

    [Theory]
    [InlineData(InteractionType.None)]
    [InlineData(InteractionType.Teleport)]
    public async Task FurnitureIsCreatedInsideTheCharge(InteractionType interaction)
    {
        var context = Context(interaction: interaction);

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal(1, context.Rewards.Charges);
        Assert.Same(context.Rewards.Transaction, context.Factory.Transaction);
        Assert.Equal(90, context.Habbo.Credits);
        Assert.NotNull(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.GetItem(700));
    }

    [Fact]
    public async Task BundleFurnitureIsCreatedInsideTheCharge()
    {
        var context = Context(products:
        [
            new CatalogProduct { Type = CatalogProductType.Furni, Definition = BundleDefinition(51) },
            new CatalogProduct { Type = CatalogProductType.Furni, Definition = BundleDefinition(52) }
        ]);

        await context.Service.Purchase(context.Client, new(1, 2, "", 1));

        Assert.Same(context.Rewards.Transaction, context.Factory.Transaction);
        Assert.Equal(2, Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.AllItems.Count());
    }

    [Fact]
    public async Task FurnitureThatCannotBeCreatedIsNotCharged()
    {
        var context = Context(factorySucceeds: false);

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal(100, context.Habbo.Credits);
        Assert.Empty(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.AllItems);
        Assert.DoesNotContain(context.Sent, packet => packet.Header == ServerPacketHeader.CreditBalanceComposer);
        Assert.DoesNotContain(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
    }

    [Fact]
    public async Task NewEffectIsCreatedInsideTheCharge()
    {
        var effects = new RecordingEffects();
        var context = Context(effect: true, effects: effects);
        context.Habbo.Effects = new EffectsComponent(TimeProvider.System);

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Same(context.Rewards.Transaction, effects.Transaction);
        Assert.Equal(90, context.Habbo.Credits);
        Assert.Equal(3600, context.Habbo.Effects.GetEffectNullable(108)!.Duration);
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.AvatarEffectAddedComposer);
    }

    [Fact]
    public async Task OwnedEffectGainsItsQuantityInsideTheCharge()
    {
        var effects = new RecordingEffects();
        var context = Context(effect: true, effects: effects);
        context.Habbo.Effects = new EffectsComponent(TimeProvider.System);
        var owned = new AvatarEffect(5, 42, 108, 3600, false, null, 1, effects);
        context.Habbo.Effects.TryAdd(owned);

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Same(context.Rewards.Transaction, effects.Transaction);
        Assert.Equal(new[] { 5 }, effects.AddedQuantities);
        Assert.Empty(effects.SavedOutside);
        Assert.Equal(2, owned.Quantity);
        Assert.Equal(90, context.Habbo.Credits);
    }

    [Fact]
    public async Task BadgeIsStoredInsideTheChargeAndPublishedAfterIt()
    {
        var badges = new RecordingBadges(stores: true);
        var context = Context(product: new CatalogProduct { Type = CatalogProductType.Badge, BadgeCode = "ACH_Bought" }, badges: badges);

        await context.Service.Purchase(context.Client, new(1, 2, "", 1));

        Assert.Same(context.Rewards.Transaction, badges.Transaction);
        Assert.Equal(new[] { "ACH_Bought" }, badges.Published);
        Assert.Empty(badges.GivenOutside);
        Assert.Equal(90, context.Habbo.Credits);
    }

    [Fact]
    public async Task BadgeThatCannotBeStoredIsNotCharged()
    {
        var badges = new RecordingBadges(stores: false);
        var context = Context(product: new CatalogProduct { Type = CatalogProductType.Badge, BadgeCode = "ACH_Restricted" }, badges: badges);

        await context.Service.Purchase(context.Client, new(1, 2, "", 1));

        Assert.Equal(100, context.Habbo.Credits);
        Assert.Empty(badges.Published);
        Assert.DoesNotContain(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
    }

    [Fact]
    public async Task FurniturePurchaseWithoutLoadedInventoryDoesNotCharge()
    {
        var context = Context();
        context.Habbo.Inventory = null;

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal(0, context.Rewards.Charges);
        Assert.Equal(100, context.Habbo.Credits);
        Assert.DoesNotContain(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
    }

    [Fact]
    public async Task EffectPurchaseWithoutLoadedEffectsDoesNotCharge()
    {
        var context = Context(effect: true);

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Null(context.Habbo.Effects);
        Assert.Equal(0, context.Rewards.Charges);
        Assert.Equal(100, context.Habbo.Credits);
        Assert.DoesNotContain(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
    }

    [Fact]
    public async Task MissingBotPresetDoesNotChargeOrPublishSuccess()
    {
        var context = Context(bot: true, botPreset: false);

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal(0, context.Rewards.Charges);
        Assert.Equal(0, context.BotStore.Creates);
        Assert.Equal(100, context.Habbo.Credits);
        Assert.Empty(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Bots.Bots);
        var error = Assert.Single(context.Sent);
        Assert.Equal(ServerPacketHeader.BroadcastMessageAlertComposer, error.Header);
        var payload = new Plus.Communication.Flash.FlashIncomingPacket { Buffer = error.Payload };
        Assert.Equal("Oops! There was an error whilst purchasing this bot. It seems that there is no bot data for the bot!", payload.ReadString());
        Assert.False(payload.HasDataRemaining());
        Assert.DoesNotContain(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
        Assert.DoesNotContain(context.Sent, packet => packet.Header == ServerPacketHeader.FurniListUpdateComposer);
    }

    [Fact]
    public async Task RefusedBotChargeNeverCreatesOrPublishes()
    {
        var context = Context(bot: true, chargeSucceeds: false);

        await context.Service.Purchase(context.Client, new(1, 2, "ignored", 1));

        Assert.Equal(1, context.Rewards.Charges);
        Assert.Equal(0, context.BotStore.Creates);
        Assert.Empty(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Bots.Bots);
        Assert.Empty(context.Sent);
    }

    [Fact]
    public async Task BotStoreFailurePropagatesBeforeWalletOrPublication()
    {
        var context = Context(bot: true, botStoreFails: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.Purchase(context.Client, new(1, 2, "ignored", 1)));

        Assert.Equal(1, context.BotStore.Creates);
        Assert.Equal(100, context.Habbo.Credits);
        Assert.Empty(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Bots.Bots);
        Assert.Empty(context.Sent);
    }

    [Fact]
    public async Task ClubPurchasePublishesPreparedOfferAndMembershipResponses()
    {
        var context = Context(club: true);

        await context.Service.Purchase(context.Client, new(1, 9, "", 1));

        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.HabboClubOffersComposer);
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.ScrSendUserInfoComposer);
    }

    [Fact]
    public async Task OwnerBuyingTheFirstForumTerminalOpensTheGroupForum()
    {
        var group = Group(owner: 42, members: [], forum: false);
        var enabled = new List<int>();
        var context = Context(interaction: InteractionType.GuildForum, groups: Groups(group), forums: Forums(enabled));

        await context.Service.Purchase(context.Client, new(1, 2, "4", 1));

        Assert.Equal([4], context.Factory.GroupIds);
        Assert.Equal([4], enabled);
        Assert.True(group.ForumEnabled);
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
    }

    [Theory]
    [InlineData(InteractionType.GuildForum, false, false)]
    [InlineData(InteractionType.GuildForum, true, true)]
    [InlineData(InteractionType.GuildItem, false, true)]
    [InlineData(InteractionType.GuildGate, false, true)]
    public async Task MembersBuyGroupFurnitureButOnlyTheOwnerOpensAForum(InteractionType interaction, bool forum, bool bought)
    {
        var enabled = new List<int>();
        var context = Context(interaction: interaction, groups: Groups(Group(owner: 7, members: [42], forum)), forums: Forums(enabled));

        await context.Service.Purchase(context.Client, new(1, 2, "4", 1));

        Assert.Equal(bought ? [4] : [], context.Factory.GroupIds);
        Assert.Equal(bought ? 1 : 0, context.Rewards.Charges);
        Assert.Equal(!bought, context.Sent.Any(packet => packet.Header == ServerPacketHeader.PurchaseErrorComposer));
        Assert.Empty(enabled);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("9")]
    [InlineData("abc")]
    [InlineData("-4")]
    [InlineData(" 4")]
    public async Task GroupFurnitureNeedsAnExistingGroupTheBuyerBelongsTo(string extraData)
    {
        var context = Context(interaction: InteractionType.GuildItem, groups: Groups(Group(owner: 7, members: [], forum: true)), forums: Forums([]));

        await context.Service.Purchase(context.Client, new(1, 2, extraData, 1));

        Assert.Equal(0, context.Factory.Creates);
        Assert.Equal(0, context.Rewards.Charges);
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseErrorComposer);
    }

    public static TheoryData<string> ShutdownBlockedKinds => ["furni", "bundle", "club", "builders_club", "builders_club_addon"];

    [Theory]
    [MemberData(nameof(ShutdownBlockedKinds))]
    public async Task PendingShutdownRefusesFurniClubAndBuildersClubWithoutChargingOrDelivering(string kind)
    {
        var purchase = ShutdownPurchase(kind, TestPlannedShutdown.Pending);

        await new PurchaseFromCatalogEvent(purchase.Context.Service).Parse(purchase.Context.Client,
            HabbiconTestSupport.Incoming(1, purchase.OfferId, "", 1));

        var refusal = Assert.Single(purchase.Context.Sent);
        Assert.Equal(ServerPacketHeader.PurchaseErrorComposer, refusal.Header);
        Assert.Equal(0, new FlashIncomingPacket { Buffer = refusal.Payload }.ReadInt());
        Assert.Equal(0, purchase.Context.Rewards.Charges);
        Assert.Equal(0, purchase.Memberships.Purchases);
        Assert.Equal(100, purchase.Context.Habbo.Credits);
        Assert.Equal(10, purchase.Context.Habbo.Currencies[5]);
        Assert.Equal(0, purchase.Context.Factory.Creates);
        Assert.Empty(Assert.IsType<InventoryComponent>(purchase.Context.Habbo.Inventory).Furniture.AllItems);
        Assert.Empty(purchase.BuildersClub.Delivered);
        Assert.Equal(0, purchase.BuildersClub.StatusSent);
        Assert.Empty(purchase.Context.Tracks.Calls);
    }

    [Theory]
    [MemberData(nameof(ShutdownBlockedKinds))]
    public async Task WithoutAPendingShutdownTheSameOffersAreBought(string kind)
    {
        var purchase = ShutdownPurchase(kind, TestPlannedShutdown.None);

        await new PurchaseFromCatalogEvent(purchase.Context.Service).Parse(purchase.Context.Client,
            HabbiconTestSupport.Incoming(1, purchase.OfferId, "", 1));

        Assert.Contains(purchase.Context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
        Assert.DoesNotContain(purchase.Context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseErrorComposer);
    }

    [Fact]
    public async Task PendingShutdownStillSellsABadge()
    {
        var context = Context(product: new CatalogProduct { Type = CatalogProductType.Badge, BadgeCode = "ACH_Shutdown" },
            plannedShutdown: TestPlannedShutdown.Pending);

        await new PurchaseFromCatalogEvent(context.Service).Parse(context.Client, HabbiconTestSupport.Incoming(1, 2, "", 1));

        Assert.Equal(1, context.Rewards.Charges);
        Assert.Equal(90, context.Habbo.Credits);
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
        Assert.DoesNotContain(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseErrorComposer);
    }

    private static ShutdownPurchaseCase ShutdownPurchase(string kind, IPlannedShutdown plannedShutdown)
    {
        var memberships = new RecordingMembership();
        var buildersClub = new RecordingBuildersClub(canBuy: true);
        var context = kind switch
        {
            "furni" => Context(points: 4, pointsType: 5, plannedShutdown: plannedShutdown),
            "bundle" => Context(points: 4, pointsType: 5, plannedShutdown: plannedShutdown, products:
            [
                new CatalogProduct { Type = CatalogProductType.Furni, Definition = BundleDefinition(51) },
                new CatalogProduct { Type = CatalogProductType.Furni, Definition = BundleDefinition(52) }
            ]),
            "club" => Context(club: true, memberships: memberships, plannedShutdown: plannedShutdown),
            "builders_club" => Context(points: 4, pointsType: 5, buildersClub: buildersClub, plannedShutdown: plannedShutdown,
                product: new CatalogProduct { Type = CatalogProductType.BuildersClub, Amount = 31, ExtraParam = "1000" }),
            "builders_club_addon" => Context(points: 4, pointsType: 5, buildersClub: buildersClub, plannedShutdown: plannedShutdown,
                product: new CatalogProduct { Type = CatalogProductType.BuildersClubAddon, Amount = 45 }),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        context.Habbo.Currencies[5] = 10;

        return new(context, kind == "club" ? 9 : 2, memberships, buildersClub);
    }

    private static ItemDefinition BundleDefinition(uint id) => new()
    {
        Id = id,
        ItemName = $"bundle_chair_{id}",
        ProductType = "s",
        Type = ItemType.Floor,
        InteractionType = InteractionType.None
    };

    private sealed record ShutdownPurchaseCase(TestContext Context, int OfferId, RecordingMembership Memberships, RecordingBuildersClub BuildersClub);

    private static Group Group(int owner, int[] members, bool forum) =>
        new(4, "Plus", "", "b28060s27124", 2, owner, null, 0, 3, 5, 0, forum, new([.. members], [], []));

    private static IGroupManager Groups(Group group) => CatalogSnapshotTestSupport.Proxy<IGroupManager>((method, args) =>
    {
        Assert.Equal(nameof(IGroupManager.TryGetGroup), method);
        args[1] = (int)args[0]! == group.Id ? group : null;

        return args[1] != null;
    });

    private static IGroupForumStore Forums(List<int> enabled) => CatalogSnapshotTestSupport.Proxy<IGroupForumStore>((method, args) =>
    {
        Assert.Equal(nameof(IGroupForumStore.Enable), method);
        enabled.Add((int)args[0]!);

        return true;
    });

    private static TestContext Context(bool enabled = true, bool chargeSucceeds = true,
        bool factorySucceeds = true, bool club = false, bool bot = false, bool botPreset = true,
        bool botStoreFails = false, int points = 0, int pointsType = 0, bool effect = false,
        InteractionType interaction = InteractionType.None, string extraParam = "", IGroupManager? groups = null, IGroupForumStore? forums = null,
        string itemName = "chair", CatalogProduct? product = null, Plus.HabboHotel.Catalog.BuildersClub.IBuildersClubService? buildersClub = null,
        IReadOnlyList<CatalogProduct>? products = null, RecordingMembership? memberships = null, IPlannedShutdown? plannedShutdown = null,
        IBadgeManager? badges = null, IAvatarEffectStore? effects = null)
    {
        var definition = new ItemDefinition
        {
            Id = 50,
            ItemName = itemName,
            ProductType = bot ? "r" : "s",
            Type = ItemType.Floor,
            InteractionType = interaction
        };
        var offer = new CatalogOffer
        {
            Id = 2,
            CostCredits = 10,
            CostPoints = points,
            PointsType = pointsType,
            LocalizationKey = "chair",
            Products = products ?? [product != null
                ? product
                : effect
                ? new CatalogProduct { Type = CatalogProductType.Effect, EffectId = 108 }
                : bot
                ? new CatalogProduct { Type = CatalogProductType.Bot, BotPresetId = (int)definition.Id }
                : new CatalogProduct { Type = CatalogProductType.Furni, Definition = definition, ExtraParam = extraParam }]
        };
        var page = new CatalogPage { Id = 1, Enabled = true, Layout = club ? "club_buy" : "default_3x3" };
        page.Offers.Add(2, offer);
        var catalog = Proxy<ICatalogManager, CatalogProxy>();
        var catalogProxy = (CatalogProxy)(object)catalog;
        catalogProxy.Page = page;
        catalogProxy.ClubOffer = new ClubOffer { Id = 9, Name = "HC_31", Days = 31 };
        catalogProxy.Bot = botPreset ? new CatalogBot
        {
            Id = definition.Id,
            Name = "Catalog Bot",
            Motto = "A bot motto",
            Figure = "hd-180-1.ch-210-66",
            Gender = "M",
            AiType = "generic"
        } : null;
        var rewards = new RecordingRewards(chargeSucceeds);
        var factory = new RecordingFactory(factorySucceeds);
        var botStore = new RecordingBotStore(botStoreFails);
        var habbo = Habbo();
        var (client, sent) = HabbiconTestSupport.Client(habbo);
        var tracks = new TestRewardProgress((target, action, amount) =>
        {
            Assert.Same(client, target);
            Assert.Equal(Plus.HabboHotel.Quests.RewardTrackActions.BuyFromCatalogue, action);
            Assert.Equal(1, amount);
            Assert.Equal(1, rewards.Charges);
            Assert.True(habbo.Credits < 100);
            Assert.NotEmpty(Assert.IsType<InventoryComponent>(habbo.Inventory).Furniture.AllItems);
            Assert.Contains(sent, packet => packet.Header == ServerPacketHeader.FurniListNotificationComposer);
            Assert.DoesNotContain(sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
        });
        var service = new CatalogPurchaseService(
            catalog,
            new Settings(enabled),
            Proxy<IAchievementManager, EmptyProxy>(),
            Proxy<IItemDataManager, EmptyProxy>(),
            badges ?? new RecordingBadges(stores: true),
            factory,
            botStore,
            Proxy<IHabbiconService, EmptyProxy>(),
            memberships ?? new RecordingMembership(),
            rewards,
            tracks,
            effects ?? Proxy<IAvatarEffectStore, EmptyProxy>(),
            groups ?? Proxy<IGroupManager, EmptyProxy>(),
            forums ?? Proxy<IGroupForumStore, EmptyProxy>(),
            new FixedClock(new DateTimeOffset(2040, 2, 3, 4, 5, 6, TimeSpan.Zero)),
            NullLogger<CatalogPurchaseService>.Instance,
            buildersClub,
            plannedShutdown);

        return new(service, client, habbo, sent, rewards, factory, botStore, tracks);
    }

    private static Habbo Habbo() => new()
    {
        Id = 42,
        Username = "Dennis",
        Credits = 100,
        Inventory = new InventoryComponent
        {
            Badges = new BadgesInventoryComponent(new()),
            Furniture = new FurnitureInventoryComponent(Array.Empty<InventoryItem>(), Array.Empty<InventoryItem>()),
            Pets = new PetsInventoryComponent(new()),
            Bots = new BotInventoryComponent(new())
        }
    };

    private static TInterface Proxy<TInterface, TProxy>()
        where TInterface : class
        where TProxy : DispatchProxy => DispatchProxy.Create<TInterface, TProxy>();

    [Fact]
    public async Task BuildersClubMembershipIsDeliveredInTheChargeAndItsStatusGoesOutAtOnce()
    {
        var buildersClub = new RecordingBuildersClub(canBuy: true);
        var membership = new CatalogProduct { Type = CatalogProductType.BuildersClub, Amount = 31, ExtraParam = "1000" };
        var context = Context(product: membership, buildersClub: buildersClub);

        await context.Service.Purchase(context.Client, new(1, 2, "", 5));

        Assert.Equal(1, context.Rewards.Charges);
        Assert.Equal(90, context.Habbo.Credits);
        Assert.Same(membership, Assert.Single(buildersClub.Delivered).Product);
        Assert.Same(context.Rewards.Transaction, buildersClub.Delivered[0].Transaction);
        Assert.Equal(1, buildersClub.StatusSent);
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
        Assert.Empty(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.AllItems);
    }

    [Fact]
    public async Task ABuildersClubPurchaseTheUserMayNotMakeIsRefusedBeforeCharging()
    {
        var buildersClub = new RecordingBuildersClub(canBuy: false);
        var context = Context(product: new CatalogProduct { Type = CatalogProductType.BuildersClubAddon, Amount = 45 }, buildersClub: buildersClub);

        await context.Service.Purchase(context.Client, new(1, 2, "", 1));

        Assert.Equal(0, context.Rewards.Charges);
        Assert.Empty(buildersClub.Delivered);
        Assert.Equal(0, buildersClub.StatusSent);
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseErrorComposer);
    }

    private sealed class RecordingBuildersClub(bool canBuy) : Plus.HabboHotel.Catalog.BuildersClub.IBuildersClubService
    {
        public List<(CatalogProduct Product, IDbTransaction Transaction)> Delivered { get; } = [];
        public int StatusSent { get; private set; }
        public bool CanBuy(Habbo habbo, CatalogProduct product) => canBuy;
        public bool Deliver(IDbConnection connection, IDbTransaction transaction, Habbo habbo, CatalogProduct product)
        {
            Delivered.Add((product, transaction));

            return true;
        }
        public void SendStatus(GameClient session) => StatusSent++;
        public Plus.HabboHotel.Catalog.BuildersClub.BuildersClubStatus Status(Habbo habbo) => throw new NotSupportedException();
        public void SendFurniCount(GameClient session) => throw new NotSupportedException();
        public void PlaceFloorItem(Plus.HabboHotel.Rooms.Room room, GameClient session, Plus.HabboHotel.Catalog.BuildersClub.BuildersClubPlacement request, int x, int y, int rotation, bool confirmed) =>
            throw new NotSupportedException();
        public void PlaceWallItem(Plus.HabboHotel.Rooms.Room room, GameClient session, Plus.HabboHotel.Catalog.BuildersClub.BuildersClubPlacement request, string wallLocation, bool confirmed) =>
            throw new NotSupportedException();
        public bool TryPickUp(GameClient session, Plus.HabboHotel.Rooms.Room room, Item item) => throw new NotSupportedException();
        public bool HidesRoom(Plus.HabboHotel.Rooms.Room room) => throw new NotSupportedException();
    }

    private sealed record TestContext(
        CatalogPurchaseService Service,
        GameClient Client,
        Habbo Habbo,
        List<(uint Header, byte[] Payload)> Sent,
        RecordingRewards Rewards,
        RecordingFactory Factory,
        RecordingBotStore BotStore,
        TestRewardProgress Tracks);

    private sealed class RecordingPurchaseService : ICatalogPurchaseService
    {
        public CatalogPurchaseRequest? Request { get; private set; }
        public Task Purchase(GameClient session, CatalogPurchaseRequest request)
        {
            Request = request;

            return Task.CompletedTask;
        }
    }

    private sealed class Settings(bool enabled) : ISettingsManager
    {
        public string TryGetValue(string value) => enabled ? "1" : "0";
        public string? GetOptionalValue(string key) => TryGetValue(key);
        public Task Reload() => Task.CompletedTask;
    }

    private sealed class RecordingRewards(bool succeeds) : IClubRewards
    {
        public int Charges { get; private set; }
        public IDbConnection Connection { get; } = Proxy<IDbConnection, EmptyProxy>();
        public IDbTransaction Transaction { get; } = Proxy<IDbTransaction, EmptyProxy>();
        public bool Charge(Habbo habbo, int credits, int points = 0, int pointsType = 0,
            Func<IDbConnection, IDbTransaction, bool>? deliver = null, bool kickbackEligible = true)
        {
            Charges++;

            if (!succeeds || deliver?.Invoke(Connection, Transaction) == false) {
                return false;
            }

            habbo.Credits -= credits;
            habbo.Currencies[pointsType] -= points;

            return true;
        }
        public ClubGiftInfo Gifts(Habbo habbo) => throw new NotSupportedException();
        public ClubGiftClaim? Claim(Habbo habbo, string productCode) => throw new NotSupportedException();
        public ClubKickback Kickback(Habbo habbo) => throw new NotSupportedException();
        public void RunPaydays() => throw new NotSupportedException();
    }

    private sealed class RecordingBotStore(bool fail) : ICatalogBotPurchaseStore
    {
        public Action? BeforeCreate { get; set; }
        public int Creates { get; private set; }
        public IDbConnection? Connection { get; private set; }
        public IDbTransaction? Transaction { get; private set; }
        public CatalogBot? Preset { get; private set; }
        public int OwnerId { get; private set; }

        public Bot Create(IDbConnection connection, IDbTransaction transaction, CatalogBot preset, int ownerId)
        {
            BeforeCreate?.Invoke();
            Creates++;
            (Connection, Transaction, Preset, OwnerId) = (connection, transaction, preset, ownerId);

            if (fail) {
                throw new InvalidOperationException("forced bot store failure");
            }

            return new Bot(701, ownerId, preset.Name!, preset.Motto!, preset.Figure!, preset.Gender!);
        }
    }

    private sealed class RecordingBadges(bool stores) : IBadgeManager
    {
        public IDbTransaction? Transaction { get; private set; }
        public List<string> Published { get; } = [];
        public List<string> GivenOutside { get; } = [];
        public IReadOnlyDictionary<string, BadgeDefinition> Badges { get; } = new Dictionary<string, BadgeDefinition>();
        public Task Init() => Task.CompletedTask;
        public Task GiveBadge(Habbo habbo, string code)
        {
            GivenOutside.Add(code);

            return Task.CompletedTask;
        }
        public bool StoreBadge(IDbConnection connection, IDbTransaction transaction, Habbo habbo, string code)
        {
            Transaction = transaction;

            return stores;
        }
        public void PublishBadge(Habbo habbo, string code) => Published.Add(code);
        public Task RemoveBadge(Habbo habbo, string badge) => throw new NotSupportedException();
        public Task<bool> ReplaceBadge(Habbo habbo, string? previous, string code) => throw new NotSupportedException();
        public Task<List<Plus.HabboHotel.Users.Badges.Badge>> LoadBadgesForHabbo(int userId) => throw new NotSupportedException();
    }

    private sealed class RecordingEffects : IAvatarEffectStore
    {
        public IDbTransaction? Transaction { get; private set; }
        public List<int> AddedQuantities { get; } = [];
        public List<int> SavedOutside { get; } = [];
        public IReadOnlyList<AvatarEffect> Load(int userId) => [];
        public AvatarEffect Create(IDbConnection connection, IDbTransaction transaction, int userId, int spriteId, double duration)
        {
            Transaction = transaction;

            return new(9, userId, spriteId, duration, false, null, 1, this);
        }
        public void AddQuantity(IDbConnection connection, IDbTransaction transaction, int id)
        {
            Transaction = transaction;
            AddedQuantities.Add(id);
        }
        public void Activate(int id, DateTimeOffset timestamp) => throw new NotSupportedException();
        public void SaveQuantity(int id, int quantity, bool activated, DateTimeOffset? activatedAt) => SavedOutside.Add(id);
    }

    private sealed class RecordingFactory(bool succeeds) : IItemFactory
    {
        public int Creates { get; private set; }
        public List<string> ExtraData { get; } = [];
        public List<int> GroupIds { get; } = [];
        public Item CreateSingleItemNullable(ItemDefinition definition, Habbo habbo, string extraData,
            string displayFlags, int groupId = 0, uint limitedNumber = 0, uint limitedStack = 0)
        {
            Creates++;
            ExtraData.Add(extraData);
            GroupIds.Add(groupId);

            if (!succeeds) {
                return null!;
            }

            return new Item { Id = (uint)(699 + Creates), OwnerId = (uint)habbo.Id, Definition = definition };
        }
        public Item CreateSingleItem(ItemDefinition definition, Habbo habbo, string extraData, string displayFlags,
            uint itemId, uint limitedNumber = 0, uint limitedStack = 0) => throw new NotSupportedException();
        public Item CreateGiftItem(ItemDefinition definition, Habbo habbo, string extraData, string displayFlags,
            int itemId, uint limitedNumber = 0, uint limitedStack = 0) => throw new NotSupportedException();
        public List<Item> CreateMultipleItems(ItemDefinition definition, Habbo habbo, string extraData, int amount,
            int groupId = 0)
        {
            var items = new List<Item>();

            for (var i = 0; i < amount; i++) {
                var item = CreateSingleItemNullable(definition, habbo, extraData, extraData, groupId);

                if (item != null) {
                    items.Add(item);
                }
            }

            return items;
        }
        public List<Item> CreateMultipleItems(ItemDefinition definition, int ownerId, string extraData, int amount,
            int groupId = 0) => throw new NotSupportedException();
        public int LinkedPairs { get; private set; }
        public List<Item> CreateTeleporterItems(ItemDefinition definition, Habbo habbo, int groupId = 0)
        {
            LinkedPairs++;
            Creates += 2;

            return [new Item { Id = (uint)(698 + Creates), OwnerId = (uint)habbo.Id, Definition = definition },
                new Item { Id = (uint)(699 + Creates), OwnerId = (uint)habbo.Id, Definition = definition }];
        }
        public IDbTransaction? Transaction { get; private set; }
        public Item CreateSingleItemNullable(IDbConnection connection, IDbTransaction transaction, ItemDefinition definition, Habbo habbo, string extraData,
            int groupId = 0, uint limitedNumber = 0, uint limitedStack = 0)
        {
            Transaction = transaction;

            return CreateSingleItemNullable(definition, habbo, extraData, extraData, groupId, limitedNumber, limitedStack);
        }
        public List<Item> CreateMultipleItems(IDbConnection connection, IDbTransaction transaction, ItemDefinition definition, int ownerId, string extraData,
            int amount, int groupId = 0)
        {
            Transaction = transaction;

            return CreateMultipleItems(definition, new Habbo { Id = ownerId }, extraData, amount, groupId);
        }
        public List<Item> CreateTeleporterItems(IDbConnection connection, IDbTransaction transaction, ItemDefinition definition, Habbo habbo, int groupId = 0)
        {
            Transaction = transaction;

            return CreateTeleporterItems(definition, habbo, groupId);
        }
        public void CreateMoodlightData(IDbConnection connection, IDbTransaction transaction, Item item) => throw new NotSupportedException();
        public void CreateTonerData(IDbConnection connection, IDbTransaction transaction, Item item) => throw new NotSupportedException();
    }

    public class CatalogProxy : DispatchProxy
    {
        public CatalogPage Page { get; set; } = null!;
        public ClubOffer ClubOffer { get; set; } = null!;
        public CatalogBot? Bot { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ICatalogManager.TryGetPage)) {
                args![1] = Page;

                return (int)args[0]! == Page.Id;
            }

            if (targetMethod?.Name == nameof(ICatalogManager.TryGetClubOffer)) {
                args![1] = ClubOffer;

                return (int)args[0]! == ClubOffer.Id;
            }

            if (targetMethod?.Name == nameof(ICatalogManager.TryGetBot)) {
                args![1] = Bot;

                return Bot != null && (uint)args[0]! == Bot.Id;
            }

            if (targetMethod?.Name == "get_ClubOffers") {
                return new[] { ClubOffer };
            }

            return Default(targetMethod?.ReturnType);
        }
    }

    private sealed class RecordingMembership : IClubMembershipService
    {
        public int Purchases { get; private set; }
        public DateTimeOffset? GetExpiry(int userId) => null;
        public DateTimeOffset? Purchase(Habbo habbo, ClubOffer offer, int? recipientId = null)
        {
            Purchases++;

            return new DateTimeOffset(2040, 3, 5, 4, 5, 6, TimeSpan.Zero);
        }
    }

    public class EmptyProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Default(targetMethod?.ReturnType);
    }

    private static object? Default(Type? type)
    {
        if (type == null || type == typeof(void)) {
            return null;
        }

        if (type == typeof(Task)) {
            return Task.CompletedTask;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>)) {
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type.GenericTypeArguments[0])
                .Invoke(null, new[] { type.GenericTypeArguments[0].IsValueType ? Activator.CreateInstance(type.GenericTypeArguments[0]) : null });
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
