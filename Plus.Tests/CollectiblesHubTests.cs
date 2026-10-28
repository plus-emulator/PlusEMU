using System.Buffers.Binary;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Collectibles;
using Plus.Communication.Packets.Outgoing;
using Plus.Core.Settings;
using Plus.Database.Migrator;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Collectibles;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// The Collectibles hub on a fresh install with Database/Migrations/109_CollectiblesHub.sql and 113_CollectiblesEmeraldsOnly.sql each
// applied twice, driven through the packet handlers. Layouts follow the client's collectibles parsers, except NftStoreOffers,
// which carries this hotel's min level per offer.
// Choices for details the spec leaves open:
// - Reading a wallet that is not the user's (score, collections) sends nothing. Actions on such a wallet answer with their failure
//   result and change nothing.
// - The wallet reply is CollectibleWalletAddresses (the Collector Wallet and no linked wallets) and EmeraldBalance. No test checks the
//   order of replies, only which packets came and what they hold.
// - Set claims: claim id "<collection id>.bonus" or "<collection id>.reward", limit 1, status 0 only while the claim would succeed
//   now, else 1. A collection's status is always 0.
// - A shop offer's minted count is every collectible of its product, whatever the source.
// - A shop offer's min level is compared with the level of the wallet that gets the collectible.
// - Mintable items: enabled rows whose window has not ended, by id; a row that starts later is listed with its start time.
// - The test connection uses the emulator's own settings (Database/Database.cs: AllowZeroDateTime, ConvertZeroDateTime).
// - Minting costs the mintable row's Emerald price. It sends FurniListRemove, EmeraldBalance and MintCollectibleResult(1) on success,
//   and only MintCollectibleResult(0) on any failure.
// - Reward claims are listed by claim id, status 0 while claimed < limit else 1, collection = set id. Other wallets get an empty list.
//   Claim all gives every remaining amount of a claim (claimLimit - claimedAmount) and answers 0
//   when it gave at least one collectible and 1 when nothing was left.
// - The level comes from the current score, not the highest score.
// - DATETIME columns hold UTC. The hotel clock is fixed at Now.
public sealed class CollectiblesHubTests
{
    private const int Emerald = 1001;
    // AIR's SilverBalance message. The hotel has no Silver, so it has no composer for it either.
    private const uint SilverBalanceHeader = 2182;
    private const string EmeraldsOnlyMigration = "Database/Migrations/113_CollectiblesEmeraldsOnly.sql";
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Created = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2026, 10, 2, 8, 30, 0, TimeSpan.Zero);

    private static readonly Product Chair = new(1, "4001", 10, null, "", "", "spring_chair", "common");
    private static readonly Product Lamp = new(0, "4002", 20, null, "", "", "spring_lamp", "rare");
    private static readonly Product Pet = new(10, "3", 30, null, "3 0 ffffff", "", "spring_pet", "epic");
    private static readonly Product Bonus = new(2, "600", 5, null, "", "", "spring_bonus", "legendary");
    private static readonly Product Reward = new(11, "4100", 7, null, "", "3001,3002", "spring_reward", "mythic");
    private static readonly Product Badge = new(4, "ACH_Winter1", 15, null, "", "", "winter_badge", "common");

    [Fact]
    public void HeadersMatchAir()
    {
        Assert.Equal(
            new uint[] { 3913, 2368, 60, 1653, 3288, 3277, 2688, 1028, 2719, 3957, 1352, 501 },
            new[]
            {
                ClientPacketHeader.GetCollectibleWalletAddressesEvent, ClientPacketHeader.GetCollectorScoreEvent, ClientPacketHeader.GetNftCollectionsEvent,
                ClientPacketHeader.ClaimNftCollectionBonusEvent, ClientPacketHeader.ClaimNftCollectionRewardEvent, ClientPacketHeader.GetNftStoreOffersEvent,
                ClientPacketHeader.PurchaseNftStoreOfferEvent, ClientPacketHeader.GetCollectibleMintableItemTypesEvent, ClientPacketHeader.GetCollectibleMintingEnabledEvent,
                ClientPacketHeader.MintCollectibleEvent, ClientPacketHeader.GetNftClaimsEvent, ClientPacketHeader.ClaimNftRewardsEvent,
            });
        Assert.Equal(
            new uint[] { 3185, 2659, 667, 154, 2730, 3151, 2908, 131, 2735, 595, 2824, 1957, 1071 },
            new[]
            {
                ServerPacketHeader.CollectibleWalletAddressesComposer, ServerPacketHeader.CollectorScoreComposer, ServerPacketHeader.NftCollectionsComposer,
                ServerPacketHeader.NftCollectionBonusClaimResultComposer, ServerPacketHeader.NftCollectionRewardClaimResultComposer, ServerPacketHeader.NftStoreOffersComposer,
                ServerPacketHeader.NftStorePurchaseResultComposer, ServerPacketHeader.CollectibleMintableItemTypesComposer, ServerPacketHeader.CollectibleMintingEnabledComposer,
                ServerPacketHeader.MintCollectibleResultComposer, ServerPacketHeader.NftClaimsComposer, ServerPacketHeader.NftClaimResultComposer,
                ServerPacketHeader.EmeraldBalanceComposer,
            });
    }

    [Fact]
    public void ThePacketManagerRoutesEveryHubRequestToItsHandler()
    {
        var hub = new CollectiblesHubService(null!, null!, TimeProvider.System);
        var handlers = typeof(ClaimNftRewardsEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ClaimNftRewardsEvent).Namespace && typeof(IPacketEvent).IsAssignableFrom(t))
            .Select(t => (IPacketEvent)Activator.CreateInstance(t, hub)!)
            .ToList();

        var manager = new PacketManager(handlers, NullLogger<PacketManager>.Instance);

        Assert.Equal(12, handlers.Count);
        Assert.All(new uint[] { 3913, 2368, 60, 1653, 3288, 3277, 2688, 1028, 2719, 3957, 1352, 501 },
            header => Assert.True(manager.IsRegistered(header), $"{header} has no handler"));
        // AIR's mint token, transfer fee and transfer requests.
        Assert.All(new uint[] { 79, 307, 668, 3654, 2626 }, header => Assert.False(manager.IsRegistered(header), $"{header} still has a handler"));
    }

    [RoomComponentDatabaseFact]
    public async Task TheCollectorWalletIsMadeOnceWithARandomAddress()
    {
        await InHotel(async hotel =>
        {
            var first = await hotel.Wallets(hotel.JohnDoe);
            var second = await hotel.Wallets(hotel.JohnDoe);
            var hers = await hotel.Wallets(hotel.JaneDoe);

            Assert.Matches("^0x[0-9a-f]{40}$", first.Collector);
            Assert.Equal(first.Collector, second.Collector);
            Assert.NotEqual(first.Collector, hers.Collector);
            Assert.Equal([first.Collector], hotel.Db.Query<string>("SELECT address FROM collectible_wallets WHERE user_id = 7 AND is_collector = 1"));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheWalletReplyIsOnlyTheCollectorWalletAndTheEmeraldBalance()
    {
        await InHotel(async hotel =>
        {
            var collector = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.LinkWallet(hotel.JohnDoe, "0x" + new string('b', 40));
            hotel.SetBalance(hotel.JohnDoe, Emerald, 75);

            var replies = await hotel.Send(hotel.JohnDoe, new GetCollectibleWalletAddressesEvent(hotel.Hub));
            var wallets = await hotel.Wallets(hotel.JohnDoe);

            Assert.DoesNotContain(replies, p => p.Header == SilverBalanceHeader);
            Assert.Equal((collector, 75), (wallets.Collector, wallets.Emeralds));
            Assert.Empty(wallets.Linked);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheCollectorScoreCountsEachProductOnceAndAddsTheBoostOfCompletedSets()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.Own(wallet, "spring_chair", 2);
            hotel.Own(wallet, "spring_lamp");

            Assert.Equal((30, 30, 1), await hotel.Score(hotel.JohnDoe, wallet));

            hotel.Own(wallet, "spring_pet");

            Assert.Equal((110, 110, 2), await hotel.Score(hotel.JohnDoe, wallet));

            hotel.Own(wallet, "spring_bonus");

            Assert.Equal((115, 115, 2), await hotel.Score(hotel.JohnDoe, wallet));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheHighestScoreIsKeptWhenTheScoreDrops()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.Own(wallet, "spring_chair");
            hotel.Own(wallet, "spring_lamp");
            hotel.Own(wallet, "spring_pet");
            Assert.Equal((110, 110, 2), await hotel.Score(hotel.JohnDoe, wallet));

            hotel.Db.Execute("DELETE FROM user_collectibles WHERE product_code = 'spring_pet'");

            Assert.Equal((30, 110, 1), await hotel.Score(hotel.JohnDoe, wallet));
            Assert.Equal(110, hotel.Db.QuerySingle<int>("SELECT highest_score FROM collectible_wallets WHERE address = @wallet", new { wallet }));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheLevelIsTheHighestLevelTheScoreReaches()
    {
        await InHotel(async hotel =>
        {
            hotel.Db.Execute("INSERT INTO collectible_products (product_code, product_type, item_type_id, score) VALUES ('trophy_249', 1, '9001', 249), ('trophy_250', 1, '9002', 250)");
            var hers = await hotel.CollectorWallet(hotel.JaneDoe);
            var mine = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.Own(hers, "trophy_249");
            hotel.Own(mine, "trophy_250");

            Assert.Equal((249, 249, 2), await hotel.Score(hotel.JaneDoe, hers));
            Assert.Equal((250, 250, 3), await hotel.Score(hotel.JohnDoe, mine));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task ReadingAWalletThatIsNotTheUsersSendsNothing()
    {
        await InHotel(async hotel =>
        {
            var hers = await hotel.CollectorWallet(hotel.JaneDoe);
            var unknown = "0x" + new string('e', 40);
            await hotel.CollectorWallet(hotel.JohnDoe);

            foreach (var wallet in new[] { hers, unknown }) {
                Assert.Empty(await hotel.Send(hotel.JohnDoe, new GetCollectorScoreEvent(hotel.Hub), wallet));
                Assert.Empty(await hotel.Send(hotel.JohnDoe, new GetNftCollectionsEvent(hotel.Hub), wallet));
            }
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheCollectionsListShowsEveryEnabledSetWithWhatTheWalletOwns()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.Own(wallet, "spring_chair", 2);
            hotel.Own(wallet, "spring_lamp");
            hotel.Own(await hotel.CollectorWallet(hotel.JaneDoe), "spring_pet");

            var collections = await hotel.Collections(hotel.JohnDoe, wallet);

            Assert.Equal(["spring_set", "winter_set"], collections.Select(c => c.Head.Id));
            Assert.Equal([Chair with { Amount = 2 }, Lamp with { Amount = 1 }, Pet with { Amount = 0 }], collections[0].Items);
            Assert.Equal(new CollectionHead("spring_set", "Spring Set", 30, 60, 50, Bonus with { Amount = 0 }, Reward with { Amount = 0 },
                    Now.AddDays(-1).ToUnixTimeMilliseconds(), Now.AddDays(1).ToUnixTimeMilliseconds(), 0,
                    new ItemClaim("spring_set.bonus", 0, 1, 1), new ItemClaim("spring_set.reward", 0, 1, 1)),
                collections[0].Head);
            Assert.Equal([Badge with { Amount = 0 }], collections[1].Items);
            Assert.Equal(new CollectionHead("winter_set", "Winter Set", 0, 15, 0, null, null, -1, -1, 0, null, null), collections[1].Head);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task ACompletedSetShowsItsBonusAndRewardAsClaimableUntilTheyAreClaimed()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.Own(wallet, "spring_chair");
            hotel.Own(wallet, "spring_lamp");
            hotel.Own(wallet, "spring_pet");

            var before = (await hotel.Collections(hotel.JohnDoe, wallet))[0].Head;
            Assert.Equal((60, new ItemClaim("spring_set.bonus", 0, 1, 0), new ItemClaim("spring_set.reward", 0, 1, 0)), (before.Score, before.BonusClaim, before.RewardClaim));

            Assert.True((await hotel.ClaimReward(hotel.JohnDoe, "spring_set", wallet)).Success);

            var after = (await hotel.Collections(hotel.JohnDoe, wallet))[0].Head;
            Assert.Equal(Reward with { Amount = 1 }, after.Reward);
            Assert.Equal(new ItemClaim("spring_set.reward", 1, 1, 1), after.RewardClaim);
            Assert.Equal(new ItemClaim("spring_set.bonus", 0, 1, 0), after.BonusClaim);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheRewardOfACompletedSetIsGivenOnce()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.Own(wallet, "spring_chair");
            hotel.Own(wallet, "spring_lamp");
            hotel.Own(wallet, "spring_pet");

            Assert.Equal(("spring_set", wallet, true), await hotel.ClaimReward(hotel.JohnDoe, "spring_set", wallet));
            Assert.Equal(("spring_set", wallet, false), await hotel.ClaimReward(hotel.JohnDoe, "spring_set", wallet));

            Assert.Equal([("spring_reward", 1, "collection_reward")], hotel.Collectibles(wallet).Where(c => c.Product == "spring_reward"));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheRewardIsRefusedForAnIncompleteSetOrAWalletThatIsNotTheUsers()
    {
        await InHotel(async hotel =>
        {
            var mine = await hotel.CollectorWallet(hotel.JohnDoe);
            var hers = await hotel.CollectorWallet(hotel.JaneDoe);
            hotel.Own(mine, "spring_chair");
            hotel.Own(mine, "spring_lamp");
            hotel.Own(hers, "spring_chair");
            hotel.Own(hers, "spring_lamp");
            hotel.Own(hers, "spring_pet");

            Assert.Equal(("spring_set", mine, false), await hotel.ClaimReward(hotel.JohnDoe, "spring_set", mine));
            Assert.Equal(("spring_set", hers, false), await hotel.ClaimReward(hotel.JohnDoe, "spring_set", hers));
            Assert.Equal(("winter_set", mine, false), await hotel.ClaimReward(hotel.JohnDoe, "winter_set", mine));

            Assert.Equal(0, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM user_collectibles WHERE product_code = 'spring_reward'"));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheBonusIsGivenOnceInsideTheReleaseWindow()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.Own(wallet, "spring_chair");
            hotel.Own(wallet, "spring_lamp");
            hotel.Own(wallet, "spring_pet");

            Assert.Equal(("spring_set", wallet, true), await hotel.ClaimBonus(hotel.JohnDoe, "spring_set", wallet));
            Assert.Equal(("spring_set", wallet, false), await hotel.ClaimBonus(hotel.JohnDoe, "spring_set", wallet));

            Assert.Equal([("spring_bonus", 1, "collection_bonus")], hotel.Collectibles(wallet).Where(c => c.Product == "spring_bonus"));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheBonusIsRefusedBeforeTheReleaseAfterTheSnapshotOrForAnIncompleteSet()
    {
        await InHotel(async hotel =>
        {
            var complete = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.Own(complete, "spring_chair");
            hotel.Own(complete, "spring_lamp");
            hotel.Own(complete, "spring_pet");
            var incomplete = await hotel.CollectorWallet(hotel.JaneDoe);
            hotel.Own(incomplete, "spring_chair");

            Assert.False((await hotel.ClaimBonus(hotel.JaneDoe, "spring_set", incomplete)).Success);

            hotel.Db.Execute("UPDATE collectible_collections SET released_at = @released WHERE id = 'spring_set'", new { released = Now.AddHours(1).UtcDateTime });
            Assert.False((await hotel.ClaimBonus(hotel.JohnDoe, "spring_set", complete)).Success);

            hotel.Db.Execute("UPDATE collectible_collections SET released_at = @released, snapshot_at = @snapshot WHERE id = 'spring_set'",
                new { released = Now.AddDays(-2).UtcDateTime, snapshot = Now.AddHours(-1).UtcDateTime });
            Assert.False((await hotel.ClaimBonus(hotel.JohnDoe, "spring_set", complete)).Success);

            Assert.Equal(0, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM user_collectibles WHERE product_code = 'spring_bonus'"));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheShopListsEnabledOffersWithHowManyWereMintedAndTheLevelTheyNeed()
    {
        await InHotel(async hotel =>
        {
            var hers = await hotel.CollectorWallet(hotel.JaneDoe);
            hotel.Own(hers, "spring_chair", 2);

            Assert.Equal(
                [new StoreOffer("spring_chair", 20, true, true, 3, 2, 1, Chair), new StoreOffer("winter_badge", 5, false, false, -1, 0, 0, Badge)],
                await hotel.StoreOffers(hotel.JohnDoe));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task BuyingAnOfferTakesEmeraldsAndGivesACollectibleWithTheNextSerial()
    {
        await InHotel(async hotel =>
        {
            var collector = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetBalance(hotel.JohnDoe, Emerald, 300);

            var first = await hotel.Buy(hotel.JohnDoe, "winter_badge", collector);
            var second = await hotel.Buy(hotel.JohnDoe, "winter_badge", collector);

            Assert.Equal(0, Read(first, ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.Equal(295, Read(first, ServerPacketHeader.EmeraldBalanceComposer).Int());
            Assert.Equal(0, Read(second, ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.Equal(290, Read(second, ServerPacketHeader.EmeraldBalanceComposer).Int());
            Assert.Equal((290, 290), (hotel.DbBalance(hotel.JohnDoe, Emerald), hotel.JohnDoe.Currencies[Emerald]));
            Assert.Equal([("winter_badge", 1, "shop"), ("winter_badge", 2, "shop")], hotel.Collectibles(collector));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task BuyingIsRefusedWithTooFewEmeralds()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetBalance(hotel.JohnDoe, Emerald, 4);

            var replies = await hotel.Buy(hotel.JohnDoe, "winter_badge", wallet);

            Assert.Equal(1, Read(replies, ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.DoesNotContain(replies, p => p.Header == ServerPacketHeader.EmeraldBalanceComposer);
            Assert.Equal((4, 4), (hotel.DbBalance(hotel.JohnDoe, Emerald), hotel.JohnDoe.Currencies[Emerald]));
            Assert.Empty(hotel.Collectibles(wallet));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task BuyingAnOfferBelowItsCollectorLevelIsRefused()
    {
        await InHotel(async hotel =>
        {
            hotel.Db.Execute("UPDATE collectible_shop_offers SET min_level = 2 WHERE product_code = 'winter_badge'");
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            Assert.Equal(1, (await hotel.Score(hotel.JohnDoe, wallet)).Level);

            var replies = await hotel.Buy(hotel.JohnDoe, "winter_badge", wallet);

            Assert.Equal(1, Read(replies, ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.DoesNotContain(replies, p => p.Header == ServerPacketHeader.EmeraldBalanceComposer);
            Assert.Equal((300, 300), (hotel.DbBalance(hotel.JohnDoe, Emerald), hotel.JohnDoe.Currencies[Emerald]));
            Assert.Empty(hotel.Collectibles(wallet));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task BuyingAnOfferAtOrAboveItsCollectorLevelSucceeds()
    {
        await InHotel(async hotel =>
        {
            hotel.Db.Execute("UPDATE collectible_shop_offers SET min_level = 2 WHERE product_code = 'winter_badge'");
            hotel.Db.Execute("INSERT INTO collectible_products (product_code, product_type, item_type_id, score) VALUES ('trophy_100', 1, '9004', 100)");
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.Own(wallet, "trophy_100");
            Assert.Equal(2, (await hotel.Score(hotel.JohnDoe, wallet)).Level);

            var atLevel = await hotel.Buy(hotel.JohnDoe, "winter_badge", wallet);
            hotel.Db.Execute("UPDATE collectible_shop_offers SET min_level = 1 WHERE product_code = 'winter_badge'");
            var aboveLevel = await hotel.Buy(hotel.JohnDoe, "winter_badge", wallet);

            Assert.Equal(0, Read(atLevel, ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.Equal(295, Read(atLevel, ServerPacketHeader.EmeraldBalanceComposer).Int());
            Assert.Equal(0, Read(aboveLevel, ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.Equal(290, Read(aboveLevel, ServerPacketHeader.EmeraldBalanceComposer).Int());
            Assert.Equal((290, 290), (hotel.DbBalance(hotel.JohnDoe, Emerald), hotel.JohnDoe.Currencies[Emerald]));
            Assert.Equal([("trophy_100", 1, "staff"), ("winter_badge", 1, "shop"), ("winter_badge", 2, "shop")], hotel.Collectibles(wallet));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task AnOfferWithoutALevelIsSoldEvenToAWalletWithoutALevel()
    {
        await InHotel(async hotel =>
        {
            hotel.Db.Execute("DELETE FROM collectible_levels");
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            Assert.Equal(0, (await hotel.Score(hotel.JohnDoe, wallet)).Level);

            Assert.Equal(0, Read(await hotel.Buy(hotel.JohnDoe, "winter_badge", wallet), ServerPacketHeader.NftStorePurchaseResultComposer).Short());

            Assert.Equal([("winter_badge", 1, "shop")], hotel.Collectibles(wallet));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task ALimitedOfferSellsOutAtItsMintLimit()
    {
        await InHotel(async hotel =>
        {
            hotel.Own(await hotel.CollectorWallet(hotel.JaneDoe), "spring_chair", 2);
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetBalance(hotel.JohnDoe, Emerald, 300);

            var last = await hotel.Buy(hotel.JohnDoe, "spring_chair", wallet);
            var soldOut = await hotel.Buy(hotel.JohnDoe, "spring_chair", wallet);

            Assert.Equal(0, Read(last, ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.Equal(1, Read(soldOut, ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.Equal((280, 280), (hotel.DbBalance(hotel.JohnDoe, Emerald), hotel.JohnDoe.Currencies[Emerald]));
            Assert.Equal([("spring_chair", 3, "shop")], hotel.Collectibles(wallet));
            Assert.Equal(3, (await hotel.StoreOffers(hotel.JohnDoe)).Single(o => o.Code == "spring_chair").Minted);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task BuyingIntoAWalletThatIsNotTheUsersOrADisabledOfferIsRefused()
    {
        await InHotel(async hotel =>
        {
            var hers = await hotel.CollectorWallet(hotel.JaneDoe);
            var mine = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetBalance(hotel.JohnDoe, Emerald, 300);

            Assert.Equal(1, Read(await hotel.Buy(hotel.JohnDoe, "winter_badge", hers), ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.Equal(1, Read(await hotel.Buy(hotel.JohnDoe, "spring_lamp", mine), ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.Equal(1, Read(await hotel.Buy(hotel.JohnDoe, "no_such_product", mine), ServerPacketHeader.NftStorePurchaseResultComposer).Short());

            Assert.Equal((300, 300), (hotel.DbBalance(hotel.JohnDoe, Emerald), hotel.JohnDoe.Currencies[Emerald]));
            Assert.Equal(0, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM user_collectibles"));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingIsEnabledByTheServerSetting()
    {
        await InHotel(async hotel =>
        {
            Assert.True(await hotel.MintingEnabled(hotel.JohnDoe));

            await hotel.Setting("collectibles.minting.enabled", "0");

            Assert.False(await hotel.MintingEnabled(hotel.JohnDoe));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheMintableItemsListShowsEnabledRowsThatHaveNotEnded()
    {
        await InHotel(async hotel =>
        {
            Assert.Equal(
                [
                    new Mintable(5001, 0, 0, false, 150, true, 0),
                    new Mintable(5002, Seconds(Now.AddDays(-1)), Seconds(Now.AddDays(1)), true, 100, false, 1),
                    new Mintable(5005, Seconds(Now.AddHours(1)), 0, false, 50, false, 0),
                ],
                await hotel.MintableItems(hotel.JohnDoe));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingTakesTheEmeraldPriceConsumesTheFurniAndGivesTheCollectible()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetBalance(hotel.JohnDoe, Emerald, 200);
            var item = hotel.GiveFurni(hotel.JohnDoe, 5001, ItemType.Floor);

            var replies = await hotel.Mint(hotel.JohnDoe, item, wallet);

            Assert.Equal(
                new[] { ServerPacketHeader.FurniListRemoveComposer, ServerPacketHeader.EmeraldBalanceComposer, ServerPacketHeader.MintCollectibleResultComposer }.Order(),
                replies.Select(p => p.Header).Order());
            Assert.Equal(1, Read(replies, ServerPacketHeader.MintCollectibleResultComposer).Short());
            Assert.Equal((int)item.Id, Read(replies, ServerPacketHeader.FurniListRemoveComposer).Int());
            Assert.Equal(50, Read(replies, ServerPacketHeader.EmeraldBalanceComposer).Int());
            Assert.Equal((50, 50), (hotel.DbBalance(hotel.JohnDoe, Emerald), hotel.JohnDoe.Currencies[Emerald]));
            Assert.Null(hotel.JohnDoe.Inventory!.Furniture.GetItem(item.Id));
            Assert.Equal(0, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM items WHERE id = @id", new { id = item.Id }));
            Assert.Equal([("spring_pet", 1, "mint")], hotel.Collectibles(wallet));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingAWallFurniUsesItsWallRow()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            var item = hotel.GiveFurni(hotel.JohnDoe, 5002, ItemType.Wall);

            var replies = await hotel.Mint(hotel.JohnDoe, item, wallet);

            Assert.Equal(1, Read(replies, ServerPacketHeader.MintCollectibleResultComposer).Short());
            Assert.Equal(200, Read(replies, ServerPacketHeader.EmeraldBalanceComposer).Int());
            Assert.Equal([("spring_lamp", 1, "mint")], hotel.Collectibles(wallet));
            Assert.Equal((200, 200), (hotel.DbBalance(hotel.JohnDoe, Emerald), hotel.JohnDoe.Currencies[Emerald]));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingFailsWithTooFewEmeralds()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetBalance(hotel.JohnDoe, Emerald, 149);

            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5001, ItemType.Floor), wallet);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingFailsForAFurniTheUserDoesNotOwn()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            var hers = hotel.GiveFurni(hotel.JaneDoe, 5001, ItemType.Floor);

            Assert.Equal([ServerPacketHeader.MintCollectibleResultComposer], (await hotel.Mint(hotel.JohnDoe, hers, wallet)).Select(p => p.Header));

            Assert.NotNull(hotel.JaneDoe.Inventory!.Furniture.GetItem(hers.Id));
            Assert.Equal(1, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM items WHERE id = @id AND user_id = 8", new { id = hers.Id }));
            Assert.Equal((300, 300), (hotel.DbBalance(hotel.JohnDoe, Emerald), hotel.JohnDoe.Currencies[Emerald]));
            Assert.Empty(hotel.Collectibles(wallet));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingFailsForAFurniWithNoEnabledMintableRow()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);

            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 7777, ItemType.Floor), wallet);
            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5001, ItemType.Wall), wallet);
            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5003, ItemType.Floor), wallet);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingFailsOutsideTheMintingWindow()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);

            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5004, ItemType.Floor), wallet);
            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5005, ItemType.Floor), wallet);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingFailsWhenTheServerTurnedItOff()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            await hotel.Setting("collectibles.minting.enabled", "0");

            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5001, ItemType.Floor), wallet);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingFailsIntoAWalletThatIsNotTheUsers()
    {
        await InHotel(async hotel =>
        {
            var hers = await hotel.CollectorWallet(hotel.JaneDoe);
            await hotel.CollectorWallet(hotel.JohnDoe);

            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5001, ItemType.Floor), hers);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task RewardClaimsListTheValidClaimsTheCollectorsLevelReaches()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);

            Assert.Equal(
                [
                    new NftClaim("oct_badge", 0, 0, 1, Ms(Now.AddDays(-1)), Ms(Now.AddDays(1)), Ms(Created), Ms(Updated), "winter_set", "winter_badge", wallet, Badge, "winter_set", "Winter Set"),
                    new NftClaim("oct_double", 0, 0, 2, Ms(Now.AddDays(-1)), Ms(Now.AddDays(1)), Ms(Created), Ms(Updated), "spring_set", "spring_chair", wallet, Chair, "spring_set", "Spring Set"),
                ],
                await hotel.Claims(hotel.JohnDoe, wallet));

            hotel.Db.Execute("INSERT INTO collectible_products (product_code, product_type, item_type_id, score) VALUES ('trophy_1000', 1, '9003', 1000)");
            hotel.Own(wallet, "trophy_1000");

            Assert.Equal(["oct_badge", "oct_double", "oct_level5"], (await hotel.Claims(hotel.JohnDoe, wallet)).Select(c => c.ClaimId));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task RewardClaimsAreListedOnlyForTheUsersCollectorWallet()
    {
        await InHotel(async hotel =>
        {
            await hotel.CollectorWallet(hotel.JohnDoe);
            var linked = hotel.LinkWallet(hotel.JohnDoe, "0x" + new string('5', 40));
            var hers = await hotel.CollectorWallet(hotel.JaneDoe);

            Assert.Empty(await hotel.Claims(hotel.JohnDoe, linked));
            Assert.Empty(await hotel.Claims(hotel.JohnDoe, hers));
        });
    }

    // A linked wallet row left from before 113 belongs to the user, but no request accepts it any more.
    [RoomComponentDatabaseFact]
    public async Task ALinkedWalletLeftFromBeforeIsRefusedLikeAWalletThatIsNotTheUsers()
    {
        await InHotel(async hotel =>
        {
            await hotel.CollectorWallet(hotel.JohnDoe);
            var linked = hotel.LinkWallet(hotel.JohnDoe, "0x" + new string('6', 40));
            hotel.Own(linked, "spring_chair");
            hotel.Own(linked, "spring_lamp");
            hotel.Own(linked, "spring_pet");

            Assert.Empty(await hotel.Send(hotel.JohnDoe, new GetCollectorScoreEvent(hotel.Hub), linked));
            Assert.Empty(await hotel.Send(hotel.JohnDoe, new GetNftCollectionsEvent(hotel.Hub), linked));
            Assert.Equal(("spring_set", linked, false), await hotel.ClaimBonus(hotel.JohnDoe, "spring_set", linked));
            Assert.Equal(("spring_set", linked, false), await hotel.ClaimReward(hotel.JohnDoe, "spring_set", linked));
            var bought = await hotel.Buy(hotel.JohnDoe, "winter_badge", linked);
            Assert.Equal(1, Read(bought, ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.DoesNotContain(bought, p => p.Header == ServerPacketHeader.EmeraldBalanceComposer);
            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5001, ItemType.Floor), linked);

            Assert.Equal((300, 300), (hotel.DbBalance(hotel.JohnDoe, Emerald), hotel.JohnDoe.Currencies[Emerald]));
            Assert.Equal([("spring_chair", 1, "staff"), ("spring_lamp", 1, "staff"), ("spring_pet", 1, "staff")], hotel.Collectibles(linked));
            Assert.Equal(3, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM user_collectibles"));
            Assert.Equal(0, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM user_collectible_collection_claims"));
            Assert.Equal(0, hotel.Db.QuerySingle<int>("SELECT highest_score FROM collectible_wallets WHERE address = @linked", new { linked }));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task ClaimAllGivesEveryRemainingRewardThenNothingIsLeft()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);

            Assert.Equal(0, Read(await hotel.ClaimAll(hotel.JohnDoe), ServerPacketHeader.NftClaimResultComposer).Short());
            Assert.Equal([("spring_chair", 1, "reward_claim"), ("spring_chair", 2, "reward_claim"), ("winter_badge", 1, "reward_claim")], hotel.Collectibles(wallet).Order());
            Assert.Equal([("oct_badge", 1, 1), ("oct_double", 2, 1)], (await hotel.Claims(hotel.JohnDoe, wallet)).Select(c => (c.ClaimId, c.Claimed, c.Status)));

            Assert.Equal(1, Read(await hotel.ClaimAll(hotel.JohnDoe), ServerPacketHeader.NftClaimResultComposer).Short());

            Assert.Equal(3, hotel.Collectibles(wallet).Count);
            Assert.Equal([("oct_badge", 1), ("oct_double", 2)],
                hotel.Db.Query<(string, int)>("SELECT claim_id, claimed_amount FROM user_collectible_reward_claims WHERE wallet_address = @wallet ORDER BY claim_id", new { wallet }));
        });
    }

    // Habbo's content from Database/Migrations/111_CollectiblesHubContent.sql, with the min level that 113 gives each offer by its price. FreshInstall.sql has no furni for nft_emerald_tome_5000, so its offer is left out.
    [RoomComponentDatabaseFact]
    public async Task TheShopSellsHabbosOffersInHabbosOrder()
    {
        await InHotel(async hotel =>
        {
            var offers = await hotel.StoreOffers(hotel.JohnDoe);

            Assert.Equal(28, offers.Count);
            Assert.DoesNotContain(offers, o => o.Code == "nft_emerald_tome_5000");
            Assert.Equal(new StoreOffer("nft_h26_witchchair", 700, true, false, -1, 0, 5, new Product(1, "19160", 115, null, "", "", "nft_h26_witchchair", "Epic")), offers[0]);
            Assert.Equal(new StoreOffer("nft_h23_vintaque_chair", 40, false, false, -1, 0, 0, new Product(1, "14101", 10, null, "", "", "nft_h23_vintaque_chair", "Common")),
                offers.Single(o => o.Code == "nft_h23_vintaque_chair"));
            Assert.Equal([("nft_emerald_100", 101, 2), ("nft_emerald_1000", 1001, 5), ("nft_emerald_500", 501, 4), ("nft_emerald_50", 51, 0), ("nft_emerald_10", 11, 0)],
                offers.TakeLast(5).Select(o => (o.Code, o.EmeraldPrice, o.MinLevel)));
        }, habboContent: true);
    }

    [RoomComponentDatabaseFact]
    public async Task HabbosMintableFurniKeepTheirMintingWindows()
    {
        await InHotel(async hotel =>
        {
            var items = await hotel.MintableItems(hotel.JohnDoe);

            Assert.Equal(74, items.Count);
            Assert.Equal(new Mintable(19169, 1792148400, 1794391140, false, 50, false, 0), items[0]);
            Assert.Equal(new Mintable(14880, 1785996000, 1794304800, false, 50, false, 0), items[^1]);
            Assert.Equal(5, items.Count(i => i.ItemType == 1));
            // 113 turns each Curator Stamp of Habbo's price into 50 Emeralds.
            Assert.Equal([(1619, 200), (4740, 150), (10210, 250)], items.Where(i => i.Price != 50).Select(i => (i.SpriteId, i.Price)));
            Assert.Equal("nft_hween_r19_witchfamiliar",
                hotel.Db.QuerySingle<string>("SELECT product_code FROM collectible_mintable_items WHERE item_type = 's' AND sprite_id = 10818"));
        }, habboContent: true);
    }

    [RoomComponentDatabaseFact]
    public async Task TheCollectionsTabListsHabbosSetsNewestFirst()
    {
        await InHotel(async hotel =>
        {
            var collections = await hotel.Collections(hotel.JohnDoe, await hotel.CollectorWallet(hotel.JohnDoe));

            Assert.Equal(73, collections.Count);
            Assert.Equal("witch_scholar", collections[0].Head.Id);

            var vintaque = Assert.Single(collections, c => c.Head.Id == "vintaque");
            Assert.Equal(new CollectionHead("vintaque", "Vintaque", 0, 150, 0, null, null, -1, -1, 0, null, null), vintaque.Head);
            Assert.Equal(15, vintaque.Items.Count);
            Assert.Equal(("nft_h23_vintaque_sofa", "nft_h23_vintaque_nightstand"), (vintaque.Items[0].Code, vintaque.Items[^1].Code));
            Assert.All(vintaque.Items, item => Assert.Equal(((short)1, 0, "Common"), (item.Type, item.Amount ?? -1, item.Rarity)));

            var soda = Assert.Single(Assert.Single(collections, c => c.Head.Id == "film_buff").Items, i => i.Code == "clothing_nftsoda");
            var furni = hotel.Db.QuerySingle<(int Sprite, string Sets)>("SELECT sprite_id, custom_params FROM furniture WHERE furnidata_classname = 'clothing_nftsoda'");
            Assert.Equal(new Product(11, furni.Sprite.ToString(), 10, 0, "", furni.Sets, "clothing_nftsoda", "Common"), soda);

            Assert.Equal("Emerald Furni", Assert.Single(collections, c => c.Head.Id == "emerald_furni").Head.Name);
            Assert.Contains(collections, c => c.Head.Id == "Lunar_New_Year_2024");
        }, habboContent: true);
    }

    // 111 needs the tables and columns that 113 removes, so it runs again on a hotel before 113.
    [RoomComponentDatabaseFact]
    public async Task RunningTheContentMigrationAgainKeepsStaffEditsAndAddsNothing()
    {
        await BeforeEmeraldsOnly(async db =>
        {
            db.Execute("UPDATE collectible_shop_offers SET emerald_price = 1 WHERE product_code = 'nft_h26_witchchair'");
            db.Execute("DELETE FROM collectible_mintable_items WHERE sprite_id = 19169");

            await Apply(db, "Database/Migrations/111_CollectiblesHubContent.sql");

            Assert.Equal((73, 582, 28, 10, 74, 0), db.QuerySingle<(int, int, int, int, int, int)>("""
                SELECT (SELECT COUNT(*) FROM collectible_collections), (SELECT COUNT(*) FROM collectible_products),
                    (SELECT COUNT(*) FROM collectible_shop_offers), (SELECT COUNT(*) FROM collectible_mint_token_offers),
                    (SELECT COUNT(*) FROM collectible_mintable_items), (SELECT COUNT(*) FROM collectible_reward_claims)
                """));
            Assert.Equal(1, db.QuerySingle<int>("SELECT emerald_price FROM collectible_shop_offers WHERE product_code = 'nft_h26_witchchair'"));
        });
    }

    // A hotel that ran 109 and 111 and still has Silver, mint tokens and linked wallets. Offers sit on each edge of the price bands.
    [RoomComponentDatabaseFact]
    public async Task TheEmeraldsOnlyMigrationPricesRelicsInEmeraldsSetsShopLevelsAndRunsTwice()
    {
        await BeforeEmeraldsOnly(async db =>
        {
            const string collector = "0x1111111111111111111111111111111111111111";
            const string linked = "0x2222222222222222222222222222222222222222";
            db.Execute("INSERT IGNORE INTO users (id, username, look, gender) VALUES (7, 'johndoe', 'hd-180-1', 'M')");
            db.Execute("INSERT INTO collectible_wallets (address, user_id, is_collector, mint_tokens) VALUES (@collector, 7, 1, 12), (@linked, 7, 0, 3)",
                new { collector, linked });
            var bands = new[] { (75, 0), (76, 2), (150, 2), (151, 3), (300, 3), (301, 4), (600, 4), (601, 5), (1200, 5), (1201, 6) };

            foreach (var (price, _) in bands) {
                db.Execute("INSERT INTO collectible_products (product_code, product_type, item_type_id, score) VALUES (@code, 1, @code, 10)", new { code = $"band_{price}" });
                db.Execute("INSERT INTO collectible_shop_offers (product_code, emerald_price) VALUES (@code, @price)", new { code = $"band_{price}", price });
            }

            await Apply(db, EmeraldsOnlyMigration);

            Assert.Equal(bands, db.Query<(int, int)>("SELECT emerald_price, min_level FROM collectible_shop_offers WHERE product_code LIKE 'band%' ORDER BY emerald_price"));
            Assert.Equal(5, db.QuerySingle<int>("SELECT min_level FROM collectible_shop_offers WHERE product_code = 'nft_h26_witchchair'"));
            Assert.Equal(("0", "NO"), db.QuerySingle<(string, string)>(
                "SELECT column_default, is_nullable FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'collectible_shop_offers' AND column_name = 'min_level'"));
            Assert.Equal(["emerald_price"], db.Query<string>(
                "SELECT column_name FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'collectible_mintable_items' AND column_name IN ('token_price', 'emerald_price')"));
            Assert.Equal(74, db.QuerySingle<int>("SELECT COUNT(*) FROM collectible_mintable_items"));
            Assert.Equal([(1619, 200), (4740, 150), (10210, 250)],
                db.Query<(int, int)>("SELECT sprite_id, emerald_price FROM collectible_mintable_items WHERE emerald_price <> 50 ORDER BY sprite_id"));
            Assert.Empty(db.Query<string>(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'collectible_mint_token_offers'"));
            Assert.Empty(db.Query<string>(
                "SELECT column_name FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'collectible_wallets' AND column_name = 'mint_tokens'"));
            Assert.Equal([(collector, true), (linked, false)], db.Query<(string, bool)>("SELECT address, is_collector FROM collectible_wallets ORDER BY address"));
            Assert.Equal(["collectibles.minting.enabled"], db.Query<string>("SELECT `key` FROM server_settings WHERE `key` LIKE 'collectibles.%'"));

            db.Execute("UPDATE collectible_shop_offers SET min_level = 2 WHERE product_code = 'band_1201'");
            var before = HubSnapshot(db);

            await Apply(db, EmeraldsOnlyMigration);

            Assert.Equal(before, HubSnapshot(db));
        });
    }

    // Mint tokens become Emeralds at 50 per token for the user of every wallet, linked wallets too.
    [RoomComponentDatabaseFact]
    public async Task TheEmeraldsOnlyMigrationTurnsMintTokensIntoEmeraldsOnce()
    {
        await BeforeEmeraldsOnly(async db =>
        {
            db.Execute("INSERT IGNORE INTO users (id, username, look, gender) VALUES (7, 'johndoe', 'hd-180-1', 'M'), (8, 'janedoe', 'hd-180-1', 'F')");
            db.Execute("INSERT INTO collectible_wallets (address, user_id, is_collector, mint_tokens) VALUES (@mine, 7, 1, 12), (@hers, 8, 0, 3)",
                new { mine = "0x" + new string('3', 40), hers = "0x" + new string('4', 40) });
            UserCurrencyStore.Set(db, 7, Emerald, 40);
            db.Execute("DELETE FROM user_currencies WHERE user_id = 8 AND type = @Emerald", new { Emerald });

            await Apply(db, EmeraldsOnlyMigration);

            Assert.Equal([(7, 640), (8, 150)], db.Query<(int, int)>("SELECT user_id, amount FROM user_currencies WHERE type = @Emerald AND user_id IN (7, 8) ORDER BY user_id", new { Emerald }));

            await Apply(db, EmeraldsOnlyMigration);

            Assert.Equal([(7, 640), (8, 150)], db.Query<(int, int)>("SELECT user_id, amount FROM user_currencies WHERE type = @Emerald AND user_id IN (7, 8) ORDER BY user_id", new { Emerald }));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task RunningTheEmeraldsOnlyMigrationAgainKeepsALevelStaffRemoved()
    {
        await BeforeEmeraldsOnly(async db =>
        {
            await Apply(db, EmeraldsOnlyMigration);
            Assert.Equal(5, db.QuerySingle<int>("SELECT min_level FROM collectible_shop_offers WHERE product_code = 'nft_h26_witchchair'"));
            db.Execute("UPDATE collectible_shop_offers SET min_level = 0 WHERE product_code = 'nft_h26_witchchair'");

            await Apply(db, EmeraldsOnlyMigration);

            Assert.Equal(0, db.QuerySingle<int>("SELECT min_level FROM collectible_shop_offers WHERE product_code = 'nft_h26_witchchair'"));
        });
    }

    // The first run stopped right after it added emerald_price, so every relic held 0 Emeralds until the next run.
    [RoomComponentDatabaseFact]
    public async Task TheEmeraldsOnlyMigrationPricesRelicsAfterARunThatStoppedHalfway()
    {
        await BeforeEmeraldsOnly(async db =>
        {
            var statements = SqlScript.Split(await File.ReadAllTextAsync(HabbiconPacketTests.Repo(EmeraldsOnlyMigration)));
            await db.ExecuteAsync(Assert.Single(statements, s => s.Contains("ALTER TABLE collectible_mintable_items ADD COLUMN")));

            await Apply(db, EmeraldsOnlyMigration);

            Assert.Equal(74, db.QuerySingle<int>("SELECT COUNT(*) FROM collectible_mintable_items"));
            Assert.Equal([(1619, 200), (4740, 150), (10210, 250)],
                db.Query<(int, int)>("SELECT sprite_id, emerald_price FROM collectible_mintable_items WHERE emerald_price <> 50 ORDER BY sprite_id"));
        });
    }

    // A FreshInstall.sql rebuilt after 113 has the hub tables in the 113 shape, and a new hotel still runs 109, 111 and 113 on it.
    // 109 and 113 on today's FreshInstall.sql give that shape. The hub must end up as on a hotel that ran 109, 111 and 113 in order.
    [RoomComponentDatabaseFact]
    public async Task TheContentMigrationFillsAFreshInstallRebuiltAfterTheEmeraldsOnlyMigration()
    {
        const string hubMigration = "Database/Migrations/109_CollectiblesHub.sql";
        const string contentMigration = "Database/Migrations/111_CollectiblesHubContent.sql";
        List<string> inOrder = [];
        List<string> rebuilt = [];

        await InSchema(async (db, _) =>
        {
            foreach (var file in new[] { hubMigration, contentMigration, EmeraldsOnlyMigration }) {
                await Apply(db, file);
            }

            inOrder = HubSnapshot(db);
        });
        await InSchema(async (db, _) =>
        {
            foreach (var file in new[] { hubMigration, EmeraldsOnlyMigration, hubMigration, contentMigration, contentMigration, EmeraldsOnlyMigration }) {
                await Apply(db, file);
            }

            Assert.Equal(74, db.QuerySingle<int>("SELECT COUNT(*) FROM collectible_mintable_items"));
            Assert.Equal([(1619, 200), (4740, 150), (10210, 250)],
                db.Query<(int, int)>("SELECT sprite_id, emerald_price FROM collectible_mintable_items WHERE emerald_price <> 50 ORDER BY sprite_id"));
            rebuilt = HubSnapshot(db);
        });

        Assert.Equal(inOrder, rebuilt);
    }

    private static async Task AssertMintFails(Hotel hotel, Habbo habbo, InventoryItem item, string wallet)
    {
        var emeralds = habbo.Currencies[Emerald];

        var replies = await hotel.Mint(habbo, item, wallet);

        Assert.Equal([ServerPacketHeader.MintCollectibleResultComposer], replies.Select(p => p.Header));
        Assert.Equal(0, Read(replies, ServerPacketHeader.MintCollectibleResultComposer).Short());
        Assert.NotNull(habbo.Inventory!.Furniture.GetItem(item.Id));
        Assert.Equal(1, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM items WHERE id = @id", new { id = item.Id }));
        Assert.Equal((emeralds, emeralds), (hotel.DbBalance(habbo, Emerald), habbo.Currencies[Emerald]));
        Assert.Equal(0, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM user_collectibles WHERE source = 'mint'"));
    }

    // Every hub table's columns and rows, and the hub settings, to show that a migration run changed nothing.
    private static List<string> HubSnapshot(MySqlConnection db)
    {
        var tables = db.Query<string>("SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE '%collectible%' ORDER BY table_name").ToList();
        var columns = db.Query<string>("""
            SELECT CONCAT_WS(' ', table_name, ordinal_position, column_name, column_type, is_nullable, COALESCE(column_default, 'none'))
            FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name LIKE '%collectible%' ORDER BY table_name, ordinal_position
            """);
        var rows = tables.SelectMany(table => db.Query($"SELECT * FROM `{table}`").Select(row => $"{table} {(object)row}")).Order();
        var settings = db.Query<string>("SELECT CONCAT(`key`, '=', `value`) FROM server_settings WHERE `key` LIKE 'collectibles.%' ORDER BY `key`");

        return [.. tables, .. columns, .. rows, .. settings];
    }

    private static long Ms(DateTimeOffset time) => time.ToUnixTimeMilliseconds();

    private static int Seconds(DateTimeOffset time) => (int)time.ToUnixTimeSeconds();

    private static PayloadReader Read(List<(uint Header, byte[] Payload)> replies, uint header) =>
        new(Assert.Single(replies, p => p.Header == header).Payload);

    private sealed record Product(short Type, string ItemTypeId, int Score, int? Amount, string PetFigure, string FigureSetIds, string Code, string Rarity);

    private sealed record ItemClaim(string ClaimId, int Claimed, int Limit, short Status);

    private sealed record CollectionHead(string Id, string Name, int Score, int TotalScore, int BoostScore, Product? Bonus, Product? Reward,
        long Released, long Snapshot, short Status, ItemClaim? BonusClaim, ItemClaim? RewardClaim);

    private sealed record Collection(List<Product> Items, CollectionHead Head);

    private sealed record StoreOffer(string Code, int EmeraldPrice, bool Featured, bool Limited, int MintLimit, int Minted, int MinLevel, Product Product);

    private sealed record Mintable(int SpriteId, int Start, int End, bool RegionLocked, int Price, bool LimitedEdition, short ItemType);

    private sealed record NftClaim(string ClaimId, int Status, int Claimed, int Limit, long ValidFrom, long ValidTo, long CreatedAt, long UpdatedAt,
        string Collection, string ProductCode, string Wallet, Product ClaimProduct, string SetId, string SetName);

    private sealed class PayloadReader(byte[] payload)
    {
        private int _at;

        public int Int()
        {
            var value = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(_at, 4));
            _at += 4;

            return value;
        }

        public short Short()
        {
            var value = BinaryPrimitives.ReadInt16BigEndian(payload.AsSpan(_at, 2));
            _at += 2;

            return value;
        }

        public bool Bool() => payload[_at++] != 0;

        public long Long() => ((long)Int() << 32) | (uint)Int();

        public string String()
        {
            var length = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(_at, 2));
            var value = Encoding.UTF8.GetString(payload, _at + 2, length);
            _at += 2 + length;

            return value;
        }

        public void End() => Assert.Equal(payload.Length, _at);

        public Product Product(bool withAmount)
        {
            var type = Short();
            var itemTypeId = String();
            var score = Int();
            int? amount = withAmount ? Int() : null;
            var petFigure = String();
            var sets = Enumerable.Range(0, Int()).Select(_ => Int()).ToList();

            return new(type, itemTypeId, score, amount, petFigure, string.Join(",", sets), String(), String());
        }

        public ItemClaim ItemClaim() => new(String(), Int(), Int(), Short());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Hotel(MySqlConnection connection, CollectiblesHubService hub, SettingsManager settings)
    {
        private readonly Dictionary<int, (Habbo Habbo, List<(uint Header, byte[] Payload)> Sent)> _users = new();

        public MySqlConnection Db => connection;
        public CollectiblesHubService Hub => hub;
        public Habbo JohnDoe => User(7, "johndoe");
        public Habbo JaneDoe => User(8, "janedoe");

        private Habbo User(int id, string name)
        {
            if (_users.TryGetValue(id, out var known)) {
                return known.Habbo;
            }

            connection.Execute("INSERT IGNORE INTO users (id, username, look, gender) VALUES (@id, @name, 'hd-180-1', 'M')", new { id, name });
            var habbo = new Habbo
            {
                Id = id,
                Username = name,
                Look = "hd-180-1",
                Gender = "m",
                Access = EditorTestSupport.Access([]),
                Inventory = new InventoryComponent { Furniture = new FurnitureInventoryComponent([], []) },
            };
            var (client, sent) = HabbiconTestSupport.Client(habbo);
            habbo.Client = client;
            _users[id] = (habbo, sent);
            SetBalance(habbo, Emerald, 300);

            return habbo;
        }

        public void SetBalance(Habbo habbo, int type, int amount)
        {
            UserCurrencyStore.Set(connection, habbo.Id, type, amount);
            habbo.Currencies[type] = amount;
        }

        public int DbBalance(Habbo habbo, int type) => UserCurrencyStore.Get(connection, habbo.Id, type);

        public async Task Setting(string key, string value)
        {
            connection.Execute("UPDATE server_settings SET `value` = @value WHERE `key` = @key", new { key, value });
            await settings.Reload();
        }

        public string LinkWallet(Habbo habbo, string address)
        {
            connection.Execute("INSERT INTO collectible_wallets (address, user_id, is_collector) VALUES (@address, @userId, 0)", new { address, userId = habbo.Id });

            return address;
        }

        public void Own(string wallet, string product, int count = 1)
        {
            for (var i = 0; i < count; i++) {
                connection.Execute(
                    "INSERT INTO user_collectibles (wallet_address, product_code, serial_number, source) " +
                    "SELECT @wallet, @product, COALESCE(MAX(serial_number), 0) + 1, 'staff' FROM user_collectibles WHERE product_code = @product",
                    new { wallet, product });
            }
        }

        public List<(string Product, int Serial, string Source)> Collectibles(string wallet) =>
            connection.Query<(string, int, string)>(
                "SELECT product_code, serial_number, source FROM user_collectibles WHERE wallet_address = @wallet ORDER BY id", new { wallet }).ToList();

        public InventoryItem GiveFurni(Habbo habbo, int spriteId, ItemType type)
        {
            var name = $"mint_test_{spriteId}_{type}".ToLowerInvariant();
            var furnitureId = connection.QuerySingleOrDefault<uint?>("SELECT id FROM furniture WHERE item_name = @name", new { name })
                ?? connection.QuerySingle<uint>("INSERT INTO furniture (item_name, public_name, type, sprite_id) VALUES (@name, @name, @type, @spriteId); SELECT LAST_INSERT_ID();",
                    new { name, type = type == ItemType.Floor ? "s" : "i", spriteId });
            var itemId = connection.QuerySingle<uint>("INSERT INTO items (user_id, room_id, base_item, extra_data) VALUES (@userId, 0, @furnitureId, ''); SELECT LAST_INSERT_ID();",
                new { userId = habbo.Id, furnitureId });
            var item = new InventoryItem
            {
                Id = itemId,
                OwnerId = (uint)habbo.Id,
                Definition = new ItemDefinition { Id = furnitureId, SpriteId = spriteId, Type = type, ItemName = name },
            };
            Assert.True(habbo.Inventory!.Furniture.AddItem(item));

            return item;
        }

        public async Task<List<(uint Header, byte[] Payload)>> Send(Habbo habbo, IPacketEvent handler, params object[] values)
        {
            var sent = _users[habbo.Id].Sent;
            sent.Clear();
            await handler.Parse(habbo.Client!, HabbiconTestSupport.Incoming(values));

            return [.. sent];
        }

        public async Task<(string Collector, List<string> Linked, int Emeralds)> Wallets(Habbo habbo)
        {
            var replies = await Send(habbo, new GetCollectibleWalletAddressesEvent(hub));
            var wallets = Read(replies, ServerPacketHeader.CollectibleWalletAddressesComposer);
            var collector = wallets.String();
            var linked = Enumerable.Range(0, wallets.Int()).Select(_ => wallets.String()).ToList();
            wallets.End();

            return (collector, linked, Read(replies, ServerPacketHeader.EmeraldBalanceComposer).Int());
        }

        public async Task<string> CollectorWallet(Habbo habbo) => (await Wallets(habbo)).Collector;

        public async Task<(int Score, int Highest, int Level)> Score(Habbo habbo, string wallet)
        {
            var reader = Read(await Send(habbo, new GetCollectorScoreEvent(hub), wallet), ServerPacketHeader.CollectorScoreComposer);
            var score = (reader.Int(), reader.Int(), reader.Int());
            reader.End();

            return score;
        }

        public async Task<List<Collection>> Collections(Habbo habbo, string wallet)
        {
            var reader = Read(await Send(habbo, new GetNftCollectionsEvent(hub), wallet), ServerPacketHeader.NftCollectionsComposer);
            var collections = new List<Collection>();

            for (var count = reader.Int(); count > 0; count--) {
                var items = Enumerable.Range(0, reader.Int()).Select(_ => reader.Product(withAmount: true)).ToList();
                var (id, name, score, total, boost) = (reader.String(), reader.String(), reader.Int(), reader.Int(), reader.Int());
                var bonus = reader.Bool() ? reader.Product(withAmount: true) : null;
                var reward = reader.Bool() ? reader.Product(withAmount: true) : null;
                var (released, snapshot, status) = (reader.Long(), reader.Long(), reader.Short());
                var bonusClaim = bonus != null ? reader.ItemClaim() : null;
                var rewardClaim = reward != null ? reader.ItemClaim() : null;
                collections.Add(new(items, new(id, name, score, total, boost, bonus, reward, released, snapshot, status, bonusClaim, rewardClaim)));
            }

            reader.End();

            return collections;
        }

        public Task<(string CollectionId, string Wallet, bool Success)> ClaimBonus(Habbo habbo, string collectionId, string wallet) =>
            ClaimResult(habbo, new ClaimNftCollectionBonusEvent(hub), ServerPacketHeader.NftCollectionBonusClaimResultComposer, collectionId, wallet);

        public Task<(string CollectionId, string Wallet, bool Success)> ClaimReward(Habbo habbo, string collectionId, string wallet) =>
            ClaimResult(habbo, new ClaimNftCollectionRewardEvent(hub), ServerPacketHeader.NftCollectionRewardClaimResultComposer, collectionId, wallet);

        private async Task<(string, string, bool)> ClaimResult(Habbo habbo, IPacketEvent handler, uint header, string collectionId, string wallet)
        {
            var reader = Read(await Send(habbo, handler, collectionId, wallet), header);
            var result = (reader.String(), reader.String(), reader.Bool());
            reader.End();

            return result;
        }

        public async Task<List<StoreOffer>> StoreOffers(Habbo habbo)
        {
            var reader = Read(await Send(habbo, new GetNftStoreOffersEvent(hub)), ServerPacketHeader.NftStoreOffersComposer);
            var offers = Enumerable.Range(0, reader.Int())
                .Select(_ => new StoreOffer(reader.String(), reader.Int(), reader.Bool(), reader.Bool(), reader.Int(), reader.Int(), reader.Int(), reader.Product(withAmount: false)))
                .ToList();
            reader.End();

            return offers;
        }

        public Task<List<(uint Header, byte[] Payload)>> Buy(Habbo habbo, string productCode, string wallet) =>
            Send(habbo, new PurchaseNftStoreOfferEvent(hub), productCode, wallet);

        public async Task<bool> MintingEnabled(Habbo habbo)
        {
            var reader = Read(await Send(habbo, new GetCollectibleMintingEnabledEvent(hub)), ServerPacketHeader.CollectibleMintingEnabledComposer);
            var enabled = reader.Bool();
            reader.End();

            return enabled;
        }

        public async Task<List<Mintable>> MintableItems(Habbo habbo)
        {
            var reader = Read(await Send(habbo, new GetCollectibleMintableItemTypesEvent(hub)), ServerPacketHeader.CollectibleMintableItemTypesComposer);
            var items = Enumerable.Range(0, reader.Int())
                .Select(_ => new Mintable(reader.Int(), reader.Int(), reader.Int(), reader.Bool(), reader.Int(), reader.Bool(), reader.Short()))
                .ToList();
            reader.End();

            return items;
        }

        public Task<List<(uint Header, byte[] Payload)>> Mint(Habbo habbo, InventoryItem item, string wallet) =>
            Send(habbo, new MintCollectibleEvent(hub), (int)item.Id, wallet);

        public async Task<List<NftClaim>> Claims(Habbo habbo, string wallet)
        {
            var reader = Read(await Send(habbo, new GetNftClaimsEvent(hub), wallet), ServerPacketHeader.NftClaimsComposer);
            var claims = Enumerable.Range(0, reader.Int())
                .Select(_ => new NftClaim(reader.String(), reader.Int(), reader.Int(), reader.Int(), reader.Long(), reader.Long(), reader.Long(), reader.Long(),
                    reader.String(), reader.String(), reader.String(), reader.Product(false), reader.String(), reader.String()))
                .ToList();
            reader.End();

            return claims;
        }

        public Task<List<(uint Header, byte[] Payload)>> ClaimAll(Habbo habbo) =>
            Send(habbo, new ClaimNftRewardsEvent(hub), "", "");
    }

    private static Task InHotel(Func<Hotel, Task> run, bool habboContent = false) => InSchema(async (connection, connectionString) =>
    {
        foreach (var file in new[] { "Database/Migrations/109_CollectiblesHub.sql", "Database/Migrations/109_CollectiblesHub.sql" }) {
            await Apply(connection, file);
        }

        if (habboContent) {
            await Apply(connection, "Database/Migrations/111_CollectiblesHubContent.sql");
            await Apply(connection, "Database/Migrations/111_CollectiblesHubContent.sql");
        }

        await Apply(connection, EmeraldsOnlyMigration);
        await Apply(connection, EmeraldsOnlyMigration);

        if (!habboContent) {
            await Seed(connection);
        }

        var database = new HabbiconDatabaseTests.TestDatabase(connectionString);
        var settings = new SettingsManager(database, NullLogger<SettingsManager>.Instance);
        await settings.Reload();
        await run(new Hotel(connection, new CollectiblesHubService(database, settings, new FixedClock(Now)), settings));
    });

    // A hotel that got the hub from 109 and 111 before 113 existed. The hub tables FreshInstall.sql already has are dropped first, so
    // 109 makes them in the shape 113 starts from.
    private static Task BeforeEmeraldsOnly(Func<MySqlConnection, Task> run) => InSchema(async (connection, _) =>
    {
        await connection.ExecuteAsync("""
            SET FOREIGN_KEY_CHECKS = 0;
            DROP TABLE IF EXISTS user_collectible_collection_claims, user_collectible_reward_claims, user_collectibles, collectible_wallets,
                collectible_reward_claims, collectible_mintable_items, collectible_mint_token_offers, collectible_shop_offers, collectible_levels,
                collectible_products, collectible_collections;
            SET FOREIGN_KEY_CHECKS = 1;
            """);
        await Apply(connection, "Database/Migrations/109_CollectiblesHub.sql");
        await Apply(connection, "Database/Migrations/111_CollectiblesHubContent.sql");
        await run(connection);
    });

    private static async Task InSchema(Func<MySqlConnection, string, Task> run)
    {
        var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!) { AllowUserVariables = true, Pooling = false, AllowZeroDateTime = true, ConvertZeroDateTime = true };
        await using var admin = new MySqlConnection(options.ConnectionString);
        await admin.OpenAsync();
        var schema = "collectibles_hub_" + Guid.NewGuid().ToString("N");
        await admin.ExecuteAsync($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            options.Database = schema;
            await using var connection = new MySqlConnection(options.ConnectionString);
            await connection.OpenAsync();
            await Apply(connection, "Database/FreshInstall.sql");
            await run(connection, options.ConnectionString);
        }
        finally {
            await admin.ExecuteAsync($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static async Task Apply(MySqlConnection connection, string file)
    {
        foreach (var statement in SqlScript.Split(await File.ReadAllTextAsync(HabbiconPacketTests.Repo(file)))) {
            await connection.ExecuteAsync(statement, commandTimeout: 300);
        }
    }

    // Spring Set: chair 10 + lamp 20 + pet 30 = 60, boost 50, a bonus and a reward, released yesterday, snapshot tomorrow.
    // Winter Set: one badge worth 15, nothing else. Retired Set is disabled. The spring chair offer needs level 1, which every wallet has.
    private static async Task Seed(MySqlConnection connection)
    {
        var times = new
        {
            dayAgo = Now.AddDays(-1).UtcDateTime,
            inADay = Now.AddDays(1).UtcDateTime,
            hourAgo = Now.AddHours(-1).UtcDateTime,
            inAnHour = Now.AddHours(1).UtcDateTime,
            monthAgo = Now.AddDays(-30).UtcDateTime,
            monthAhead = Now.AddDays(30).UtcDateTime,
            created = Created.UtcDateTime,
            updated = Updated.UtcDateTime,
        };

        await connection.ExecuteAsync("""
            INSERT INTO collectible_collections (id, name, boost_score, bonus_product_code, reward_product_code, released_at, snapshot_at, sort_order, enabled) VALUES
            ('winter_set', 'Winter Set', 0, NULL, NULL, NULL, NULL, 2, 1),
            ('spring_set', 'Spring Set', 50, 'spring_bonus', 'spring_reward', @dayAgo, @inADay, 1, 1),
            ('retired_set', 'Retired Set', 0, NULL, NULL, NULL, NULL, 0, 0);

            INSERT INTO collectible_products (product_code, collection_id, product_type, item_type_id, score, rarity, pet_figure, figure_set_ids, sort_order) VALUES
            ('spring_lamp', 'spring_set', 0, '4002', 20, 'rare', '', '', 2),
            ('spring_pet', 'spring_set', 10, '3', 30, 'epic', '3 0 ffffff', '', 3),
            ('spring_chair', 'spring_set', 1, '4001', 10, 'common', '', '', 1),
            ('spring_bonus', NULL, 2, '600', 5, 'legendary', '', '', 0),
            ('spring_reward', NULL, 11, '4100', 7, 'mythic', '', '3001,3002', 0),
            ('winter_badge', 'winter_set', 4, 'ACH_Winter1', 15, 'common', '', '', 1),
            ('retired_cup', 'retired_set', 1, '4200', 99, 'common', '', '', 1);

            INSERT INTO collectible_shop_offers (product_code, emerald_price, min_level, is_featured, is_limited, mint_limit, sort_order, enabled) VALUES
            ('winter_badge', 5, 0, 0, 0, -1, 2, 1),
            ('spring_chair', 20, 1, 1, 1, 3, 1, 1),
            ('spring_lamp', 8, 0, 0, 0, -1, 0, 0);

            INSERT INTO collectible_mintable_items (id, item_type, sprite_id, product_code, emerald_price, starts_at, ends_at, region_locked, limited_edition, enabled) VALUES
            (1, 's', 5001, 'spring_pet', 150, NULL, NULL, 0, 1, 1),
            (2, 'i', 5002, 'spring_lamp', 100, @dayAgo, @inADay, 1, 0, 1),
            (3, 's', 5003, 'spring_chair', 50, NULL, NULL, 0, 0, 0),
            (4, 's', 5004, 'spring_chair', 50, @dayAgo, @hourAgo, 0, 0, 1),
            (5, 's', 5005, 'spring_chair', 50, @inAnHour, NULL, 0, 0, 1);

            INSERT INTO collectible_reward_claims (claim_id, product_code, set_id, set_name, min_level, claim_limit, valid_from, valid_to, created_at, updated_at) VALUES
            ('oct_double', 'spring_chair', 'spring_set', 'Spring Set', 1, 2, @dayAgo, @inADay, @created, @updated),
            ('oct_badge', 'winter_badge', 'winter_set', 'Winter Set', 1, 1, @dayAgo, @inADay, @created, @updated),
            ('oct_level5', 'spring_pet', 'spring_set', 'Spring Set', 5, 1, @dayAgo, @inADay, @created, @updated),
            ('sep_badge', 'winter_badge', 'winter_set', 'Winter Set', 1, 1, @monthAgo, @hourAgo, @created, @updated),
            ('nov_badge', 'winter_badge', 'winter_set', 'Winter Set', 1, 1, @inAnHour, @monthAhead, @created, @updated);
            """, times);
    }
}
