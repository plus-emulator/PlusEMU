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

// The Collectibles hub on a fresh install with Database/Migrations/108_CollectiblesHub.sql applied twice, driven through the packet
// handlers. Layouts follow AIR WIN63-202610201032-107964540 (communication/messages/parser/collectibles).
// Choices for details the spec leaves open:
// - Reading a wallet that is not the user's (score, collections, mint tokens) sends nothing. Actions on such a wallet answer with
//   their failure result and change nothing.
// - The wallet reply is CollectibleWalletAddresses (Collector Wallet, then linked wallets in the order staff added them), SilverBalance
//   and EmeraldBalance. No test checks the order of replies, only which packets came and what they hold.
// - Set claims: claim id "<collection id>.bonus" or "<collection id>.reward", limit 1, status 0 only while the claim would succeed
//   now, else 1. A collection's status is always 0.
// - A shop offer's minted count is every collectible of its product, whatever the source.
// - Mintable items: enabled rows whose window has not ended, by id; a row that starts later is listed with its start time.
// - Buying mint tokens that fails sends nothing. Minting sends FurniListRemove, CollectibleMintTokens and MintCollectibleResult(1) on
//   success, and MintCollectibleResult(0) without FurniListRemove on any failure.
// - Transfer result 1 = not one of the user's linked wallets, 2 = not enough silver. Success sends NftTransferResult(0) and SilverBalance.
// - Reward claims are listed by claim id, status 0 while claimed < limit else 1, collection = set id. Other wallets get an empty list.
//   Claim all answers 0 when it gave at least one collectible and 1 when nothing was left.
// - The level comes from the current score, not the highest score.
// - DATETIME columns hold UTC. The hotel clock is fixed at Now.
public sealed class CollectiblesHubTests
{
    private const int Silver = 1000;
    private const int Emerald = 1001;
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
            new uint[] { 3112, 3283, 2267, 2709, 1471, 1112, 2065, 446, 264, 3070, 2050, 3760, 1663, 3678, 1589, 1590, 2855 },
            new[]
            {
                ClientPacketHeader.GetCollectibleWalletAddressesEvent, ClientPacketHeader.GetCollectorScoreEvent, ClientPacketHeader.GetNftCollectionsEvent,
                ClientPacketHeader.ClaimNftCollectionBonusEvent, ClientPacketHeader.ClaimNftCollectionRewardEvent, ClientPacketHeader.GetNftStoreOffersEvent,
                ClientPacketHeader.PurchaseNftStoreOfferEvent, ClientPacketHeader.GetCollectibleMintableItemTypesEvent, ClientPacketHeader.GetCollectibleMintingEnabledEvent,
                ClientPacketHeader.GetMintTokenOffersEvent, ClientPacketHeader.GetCollectibleMintTokensEvent, ClientPacketHeader.PurchaseMintTokensEvent,
                ClientPacketHeader.MintCollectibleEvent, ClientPacketHeader.GetNftTransferFeeEvent, ClientPacketHeader.TransferNftsEvent,
                ClientPacketHeader.GetNftClaimsEvent, ClientPacketHeader.ClaimNftRewardsEvent,
            });
        Assert.Equal(
            new uint[] { 2681, 1598, 3181, 3325, 352, 3287, 358, 749, 2689, 2636, 768, 1072, 682, 2056, 3065, 3539, 2182, 2789 },
            new[]
            {
                ServerPacketHeader.CollectibleWalletAddressesComposer, ServerPacketHeader.CollectorScoreComposer, ServerPacketHeader.NftCollectionsComposer,
                ServerPacketHeader.NftCollectionBonusClaimResultComposer, ServerPacketHeader.NftCollectionRewardClaimResultComposer, ServerPacketHeader.NftStoreOffersComposer,
                ServerPacketHeader.NftStorePurchaseResultComposer, ServerPacketHeader.CollectibleMintableItemTypesComposer, ServerPacketHeader.CollectibleMintingEnabledComposer,
                ServerPacketHeader.MintTokenOffersComposer, ServerPacketHeader.CollectibleMintTokensComposer, ServerPacketHeader.MintCollectibleResultComposer,
                ServerPacketHeader.NftTransferFeeComposer, ServerPacketHeader.NftTransferResultComposer, ServerPacketHeader.NftClaimsComposer,
                ServerPacketHeader.NftClaimResultComposer, ServerPacketHeader.SilverBalanceComposer, ServerPacketHeader.EmeraldBalanceComposer,
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

        Assert.Equal(17, handlers.Count);
        Assert.All(new uint[] { 3112, 3283, 2267, 2709, 1471, 1112, 2065, 446, 264, 3070, 2050, 3760, 1663, 3678, 1589, 1590, 2855 },
            header => Assert.True(manager.IsRegistered(header), $"{header} has no handler"));
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
    public async Task TheWalletListIsTheCollectorWalletThenTheLinkedWalletsInTheOrderTheyWereAdded()
    {
        await InHotel(async hotel =>
        {
            var collector = await hotel.CollectorWallet(hotel.JohnDoe);
            var later = hotel.LinkWallet(hotel.JohnDoe, "0x" + new string('b', 40));
            var earlier = hotel.LinkWallet(hotel.JohnDoe, "0x" + new string('a', 40));
            hotel.LinkWallet(hotel.JaneDoe, "0x" + new string('c', 40));
            hotel.SetBalance(hotel.JohnDoe, Silver, 640);
            hotel.SetBalance(hotel.JohnDoe, Emerald, 75);

            var wallets = await hotel.Wallets(hotel.JohnDoe);

            Assert.Equal(collector, wallets.Collector);
            Assert.Equal([later, earlier], wallets.Linked);
            Assert.Equal((640, 75), (wallets.Silver, wallets.Emeralds));
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
            var collector = await hotel.CollectorWallet(hotel.JohnDoe);
            var linked = hotel.LinkWallet(hotel.JohnDoe, "0x" + new string('d', 40));
            hotel.Own(linked, "trophy_249");
            hotel.Own(collector, "trophy_250");

            Assert.Equal((249, 249, 2), await hotel.Score(hotel.JohnDoe, linked));
            Assert.Equal((250, 250, 3), await hotel.Score(hotel.JohnDoe, collector));
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
                Assert.Empty(await hotel.Send(hotel.JohnDoe, new GetCollectibleMintTokensEvent(hotel.Hub), wallet));
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
            var incomplete = hotel.LinkWallet(hotel.JohnDoe, "0x" + new string('f', 40));
            hotel.Own(incomplete, "spring_chair");

            Assert.False((await hotel.ClaimBonus(hotel.JohnDoe, "spring_set", incomplete)).Success);

            hotel.Db.Execute("UPDATE collectible_collections SET released_at = @released WHERE id = 'spring_set'", new { released = Now.AddHours(1).UtcDateTime });
            Assert.False((await hotel.ClaimBonus(hotel.JohnDoe, "spring_set", complete)).Success);

            hotel.Db.Execute("UPDATE collectible_collections SET released_at = @released, snapshot_at = @snapshot WHERE id = 'spring_set'",
                new { released = Now.AddDays(-2).UtcDateTime, snapshot = Now.AddHours(-1).UtcDateTime });
            Assert.False((await hotel.ClaimBonus(hotel.JohnDoe, "spring_set", complete)).Success);

            Assert.Equal(0, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM user_collectibles WHERE product_code = 'spring_bonus'"));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheShopListsEnabledOffersWithHowManyWereMinted()
    {
        await InHotel(async hotel =>
        {
            var hers = await hotel.CollectorWallet(hotel.JaneDoe);
            hotel.Own(hers, "spring_chair", 2);

            Assert.Equal(
                [new StoreOffer("spring_chair", 20, true, true, 3, 2, Chair), new StoreOffer("winter_badge", 5, false, false, -1, 0, Badge)],
                await hotel.StoreOffers(hotel.JohnDoe));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task BuyingAnOfferTakesEmeraldsAndGivesACollectibleWithTheNextSerial()
    {
        await InHotel(async hotel =>
        {
            var collector = await hotel.CollectorWallet(hotel.JohnDoe);
            var linked = hotel.LinkWallet(hotel.JohnDoe, "0x" + new string('1', 40));
            hotel.SetBalance(hotel.JohnDoe, Emerald, 300);

            var first = await hotel.Buy(hotel.JohnDoe, "winter_badge", collector);
            var second = await hotel.Buy(hotel.JohnDoe, "winter_badge", linked);

            Assert.Equal(0, Read(first, ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.Equal(295, Read(first, ServerPacketHeader.EmeraldBalanceComposer).Int());
            Assert.Equal(0, Read(second, ServerPacketHeader.NftStorePurchaseResultComposer).Short());
            Assert.Equal(290, Read(second, ServerPacketHeader.EmeraldBalanceComposer).Int());
            Assert.Equal((290, 290), (hotel.DbBalance(hotel.JohnDoe, Emerald), hotel.JohnDoe.Currencies[Emerald]));
            Assert.Equal([("winter_badge", 1, "shop")], hotel.Collectibles(collector));
            Assert.Equal([("winter_badge", 2, "shop")], hotel.Collectibles(linked));
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
                    new Mintable(5001, 0, 0, false, 3, true, 0),
                    new Mintable(5002, Seconds(Now.AddDays(-1)), Seconds(Now.AddDays(1)), true, 2, false, 1),
                    new Mintable(5005, Seconds(Now.AddHours(1)), 0, false, 1, false, 0),
                ],
                await hotel.MintableItems(hotel.JohnDoe));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheMintTokenOffersListShowsEnabledOffers()
    {
        await InHotel(async hotel =>
        {
            Assert.Equal([(12, "mint_tokens_50", 400, 50), (11, "mint_tokens_10", 100, 10)], await hotel.TokenOffers(hotel.JohnDoe));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintTokensAreBoughtWithSilver()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetBalance(hotel.JohnDoe, Silver, 500);
            Assert.Equal(0, await hotel.Tokens(hotel.JohnDoe, wallet));

            var replies = await hotel.BuyTokens(hotel.JohnDoe, 11, wallet);

            Assert.Equal(400, Read(replies, ServerPacketHeader.SilverBalanceComposer).Int());
            Assert.Equal(10, Read(replies, ServerPacketHeader.CollectibleMintTokensComposer).Int());
            Assert.Equal((400, 400), (hotel.DbBalance(hotel.JohnDoe, Silver), hotel.JohnDoe.Currencies[Silver]));
            Assert.Equal(10, await hotel.Tokens(hotel.JohnDoe, wallet));
            Assert.Equal(10, hotel.Db.QuerySingle<int>("SELECT mint_tokens FROM collectible_wallets WHERE address = @wallet", new { wallet }));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task BuyingMintTokensIsRefusedWithTooLittleSilverOrForAnotherUsersWallet()
    {
        await InHotel(async hotel =>
        {
            var mine = await hotel.CollectorWallet(hotel.JohnDoe);
            var hers = await hotel.CollectorWallet(hotel.JaneDoe);
            hotel.SetBalance(hotel.JohnDoe, Silver, 99);

            Assert.Empty(await hotel.BuyTokens(hotel.JohnDoe, 11, mine));

            hotel.SetBalance(hotel.JohnDoe, Silver, 500);
            Assert.Empty(await hotel.BuyTokens(hotel.JohnDoe, 11, hers));
            Assert.Empty(await hotel.BuyTokens(hotel.JohnDoe, 13, mine));
            Assert.Empty(await hotel.BuyTokens(hotel.JohnDoe, 999, mine));

            Assert.Equal((500, 500), (hotel.DbBalance(hotel.JohnDoe, Silver), hotel.JohnDoe.Currencies[Silver]));
            Assert.Equal(0, hotel.Db.QuerySingle<int>("SELECT COALESCE(SUM(mint_tokens), 0) FROM collectible_wallets"));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingConsumesTheFurniAndGivesTheCollectible()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetTokens(wallet, 5);
            var item = hotel.GiveFurni(hotel.JohnDoe, 5001, ItemType.Floor);

            var replies = await hotel.Mint(hotel.JohnDoe, item, wallet);

            Assert.Equal(1, Read(replies, ServerPacketHeader.MintCollectibleResultComposer).Short());
            Assert.Equal((int)item.Id, Read(replies, ServerPacketHeader.FurniListRemoveComposer).Int());
            Assert.Equal(2, Read(replies, ServerPacketHeader.CollectibleMintTokensComposer).Int());
            Assert.Null(hotel.JohnDoe.Inventory!.Furniture.GetItem(item.Id));
            Assert.Equal(0, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM items WHERE id = @id", new { id = item.Id }));
            Assert.Equal(2, hotel.Db.QuerySingle<int>("SELECT mint_tokens FROM collectible_wallets WHERE address = @wallet", new { wallet }));
            Assert.Equal([("spring_pet", 1, "mint")], hotel.Collectibles(wallet));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingAWallFurniUsesItsWallRow()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetTokens(wallet, 2);
            var item = hotel.GiveFurni(hotel.JohnDoe, 5002, ItemType.Wall);

            Assert.Equal(1, Read(await hotel.Mint(hotel.JohnDoe, item, wallet), ServerPacketHeader.MintCollectibleResultComposer).Short());

            Assert.Equal([("spring_lamp", 1, "mint")], hotel.Collectibles(wallet));
            Assert.Equal(0, hotel.Db.QuerySingle<int>("SELECT mint_tokens FROM collectible_wallets WHERE address = @wallet", new { wallet }));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingFailsWithoutEnoughTokens()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetTokens(wallet, 2);

            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5001, ItemType.Floor), wallet, tokens: 2);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingFailsForAFurniTheUserDoesNotOwn()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetTokens(wallet, 5);
            var hers = hotel.GiveFurni(hotel.JaneDoe, 5001, ItemType.Floor);

            Assert.Equal(0, Read(await hotel.Mint(hotel.JohnDoe, hers, wallet), ServerPacketHeader.MintCollectibleResultComposer).Short());

            Assert.NotNull(hotel.JaneDoe.Inventory!.Furniture.GetItem(hers.Id));
            Assert.Equal(1, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM items WHERE id = @id AND user_id = 8", new { id = hers.Id }));
            Assert.Equal(5, hotel.Db.QuerySingle<int>("SELECT mint_tokens FROM collectible_wallets WHERE address = @wallet", new { wallet }));
            Assert.Empty(hotel.Collectibles(wallet));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingFailsForAFurniWithNoEnabledMintableRow()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetTokens(wallet, 5);

            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 7777, ItemType.Floor), wallet, tokens: 5);
            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5001, ItemType.Wall), wallet, tokens: 5);
            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5003, ItemType.Floor), wallet, tokens: 5);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingFailsOutsideTheMintingWindow()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetTokens(wallet, 5);

            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5004, ItemType.Floor), wallet, tokens: 5);
            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5005, ItemType.Floor), wallet, tokens: 5);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingFailsWhenTheServerTurnedItOff()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetTokens(wallet, 5);
            await hotel.Setting("collectibles.minting.enabled", "0");

            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5001, ItemType.Floor), wallet, tokens: 5);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task MintingFailsIntoAWalletThatIsNotTheUsers()
    {
        await InHotel(async hotel =>
        {
            var hers = await hotel.CollectorWallet(hotel.JaneDoe);
            await hotel.CollectorWallet(hotel.JohnDoe);
            hotel.SetTokens(hers, 5);

            await AssertMintFails(hotel, hotel.JohnDoe, hotel.GiveFurni(hotel.JohnDoe, 5001, ItemType.Floor), hers, tokens: 5);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheTransferFeeFollowsTheServerSetting()
    {
        await InHotel(async hotel =>
        {
            Assert.Equal(0, await hotel.TransferFee(hotel.JohnDoe));

            await hotel.Setting("collectibles.transfer.fee", "25");

            Assert.Equal(25, await hotel.TransferFee(hotel.JohnDoe));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TransferMovesEveryCollectibleOfALinkedWalletToTheCollectorWalletForTheFee()
    {
        await InHotel(async hotel =>
        {
            await hotel.Setting("collectibles.transfer.fee", "25");
            var collector = await hotel.CollectorWallet(hotel.JohnDoe);
            var linked = hotel.LinkWallet(hotel.JohnDoe, "0x" + new string('2', 40));
            hotel.Own(collector, "spring_lamp");
            hotel.Own(linked, "spring_chair");
            hotel.Own(linked, "winter_badge");
            hotel.SetBalance(hotel.JohnDoe, Silver, 500);

            var replies = await hotel.Transfer(hotel.JohnDoe, linked);

            Assert.Equal(0, Read(replies, ServerPacketHeader.NftTransferResultComposer).Short());
            Assert.Equal(475, Read(replies, ServerPacketHeader.SilverBalanceComposer).Int());
            Assert.Equal((475, 475), (hotel.DbBalance(hotel.JohnDoe, Silver), hotel.JohnDoe.Currencies[Silver]));
            Assert.Equal([("spring_chair", 1, "staff"), ("spring_lamp", 1, "staff"), ("winter_badge", 1, "staff")], hotel.Collectibles(collector).Order());
            Assert.Empty(hotel.Collectibles(linked));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TransferIsRefusedFromTheCollectorWalletOrAWalletThatIsNotTheUsers()
    {
        await InHotel(async hotel =>
        {
            var collector = await hotel.CollectorWallet(hotel.JohnDoe);
            var hersLinked = hotel.LinkWallet(hotel.JaneDoe, "0x" + new string('3', 40));
            await hotel.CollectorWallet(hotel.JaneDoe);
            hotel.Own(collector, "spring_chair");
            hotel.Own(hersLinked, "winter_badge");
            hotel.SetBalance(hotel.JohnDoe, Silver, 500);

            Assert.Equal(1, Read(await hotel.Transfer(hotel.JohnDoe, collector), ServerPacketHeader.NftTransferResultComposer).Short());
            Assert.Equal(1, Read(await hotel.Transfer(hotel.JohnDoe, hersLinked), ServerPacketHeader.NftTransferResultComposer).Short());

            Assert.Equal([("spring_chair", 1, "staff")], hotel.Collectibles(collector));
            Assert.Equal([("winter_badge", 1, "staff")], hotel.Collectibles(hersLinked));
            Assert.Equal((500, 500), (hotel.DbBalance(hotel.JohnDoe, Silver), hotel.JohnDoe.Currencies[Silver]));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TransferIsRefusedWithTooLittleSilverForTheFee()
    {
        await InHotel(async hotel =>
        {
            await hotel.Setting("collectibles.transfer.fee", "25");
            var collector = await hotel.CollectorWallet(hotel.JohnDoe);
            var linked = hotel.LinkWallet(hotel.JohnDoe, "0x" + new string('4', 40));
            hotel.Own(linked, "spring_chair");
            hotel.SetBalance(hotel.JohnDoe, Silver, 24);

            var replies = await hotel.Transfer(hotel.JohnDoe, linked);

            Assert.Equal(2, Read(replies, ServerPacketHeader.NftTransferResultComposer).Short());
            Assert.DoesNotContain(replies, p => p.Header == ServerPacketHeader.SilverBalanceComposer);
            Assert.Equal([("spring_chair", 1, "staff")], hotel.Collectibles(linked));
            Assert.Empty(hotel.Collectibles(collector));
            Assert.Equal((24, 24), (hotel.DbBalance(hotel.JohnDoe, Silver), hotel.JohnDoe.Currencies[Silver]));
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

    [RoomComponentDatabaseFact]
    public async Task ClaimAllGivesOneOfEachClaimableRewardUntilNothingIsLeft()
    {
        await InHotel(async hotel =>
        {
            var wallet = await hotel.CollectorWallet(hotel.JohnDoe);

            Assert.Equal(0, Read(await hotel.ClaimAll(hotel.JohnDoe), ServerPacketHeader.NftClaimResultComposer).Short());
            Assert.Equal([("spring_chair", 1, "reward_claim"), ("winter_badge", 1, "reward_claim")], hotel.Collectibles(wallet).Order());
            Assert.Equal([("oct_badge", 1, 1), ("oct_double", 1, 0)], (await hotel.Claims(hotel.JohnDoe, wallet)).Select(c => (c.ClaimId, c.Claimed, c.Status)));

            Assert.Equal(0, Read(await hotel.ClaimAll(hotel.JohnDoe), ServerPacketHeader.NftClaimResultComposer).Short());
            Assert.Equal(1, Read(await hotel.ClaimAll(hotel.JohnDoe), ServerPacketHeader.NftClaimResultComposer).Short());

            Assert.Equal([("spring_chair", 1, "reward_claim"), ("spring_chair", 2, "reward_claim"), ("winter_badge", 1, "reward_claim")], hotel.Collectibles(wallet).Order());
            Assert.Equal([("oct_badge", 1), ("oct_double", 2)],
                hotel.Db.Query<(string, int)>("SELECT claim_id, claimed_amount FROM user_collectible_reward_claims WHERE wallet_address = @wallet ORDER BY claim_id", new { wallet }));
        });
    }

    private static async Task AssertMintFails(Hotel hotel, Habbo habbo, InventoryItem item, string wallet, int tokens)
    {
        var replies = await hotel.Mint(habbo, item, wallet);

        Assert.Equal(0, Read(replies, ServerPacketHeader.MintCollectibleResultComposer).Short());
        Assert.DoesNotContain(replies, p => p.Header == ServerPacketHeader.FurniListRemoveComposer);
        Assert.NotNull(habbo.Inventory!.Furniture.GetItem(item.Id));
        Assert.Equal(1, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM items WHERE id = @id", new { id = item.Id }));
        Assert.Equal(tokens, hotel.Db.QuerySingle<int>("SELECT mint_tokens FROM collectible_wallets WHERE address = @wallet", new { wallet }));
        Assert.Equal(0, hotel.Db.QuerySingle<int>("SELECT COUNT(*) FROM user_collectibles WHERE source = 'mint'"));
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

    private sealed record StoreOffer(string Code, int EmeraldPrice, bool Featured, bool Limited, int MintLimit, int Minted, Product Product);

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
            SetBalance(habbo, Silver, 500);
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

        public void SetTokens(string wallet, int tokens) =>
            connection.Execute("UPDATE collectible_wallets SET mint_tokens = @tokens WHERE address = @wallet", new { wallet, tokens });

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

        public async Task<(string Collector, List<string> Linked, int Silver, int Emeralds)> Wallets(Habbo habbo)
        {
            var replies = await Send(habbo, new GetCollectibleWalletAddressesEvent(hub));
            var wallets = Read(replies, ServerPacketHeader.CollectibleWalletAddressesComposer);
            var collector = wallets.String();
            var linked = Enumerable.Range(0, wallets.Int()).Select(_ => wallets.String()).ToList();
            wallets.End();

            return (collector, linked, Read(replies, ServerPacketHeader.SilverBalanceComposer).Int(), Read(replies, ServerPacketHeader.EmeraldBalanceComposer).Int());
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
                .Select(_ => new StoreOffer(reader.String(), reader.Int(), reader.Bool(), reader.Bool(), reader.Int(), reader.Int(), reader.Product(withAmount: false)))
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

        public async Task<List<(int OfferId, string ProductCode, int SilverPrice, int Tokens)>> TokenOffers(Habbo habbo)
        {
            var reader = Read(await Send(habbo, new GetMintTokenOffersEvent(hub)), ServerPacketHeader.MintTokenOffersComposer);
            var offers = Enumerable.Range(0, reader.Int()).Select(_ => (reader.Int(), reader.String(), reader.Int(), reader.Int())).ToList();
            reader.End();

            return offers;
        }

        public async Task<int> Tokens(Habbo habbo, string wallet)
        {
            var reader = Read(await Send(habbo, new GetCollectibleMintTokensEvent(hub), wallet), ServerPacketHeader.CollectibleMintTokensComposer);
            var tokens = reader.Int();
            reader.End();

            return tokens;
        }

        public Task<List<(uint Header, byte[] Payload)>> BuyTokens(Habbo habbo, int offerId, string wallet) =>
            Send(habbo, new PurchaseMintTokensEvent(hub), offerId, wallet);

        public Task<List<(uint Header, byte[] Payload)>> Mint(Habbo habbo, InventoryItem item, string wallet) =>
            Send(habbo, new MintCollectibleEvent(hub), (int)item.Id, wallet);

        public async Task<int> TransferFee(Habbo habbo)
        {
            var reader = Read(await Send(habbo, new GetNftTransferFeeEvent(hub)), ServerPacketHeader.NftTransferFeeComposer);
            var fee = reader.Int();
            reader.End();

            return fee;
        }

        public Task<List<(uint Header, byte[] Payload)>> Transfer(Habbo habbo, string wallet) =>
            Send(habbo, new TransferNftsEvent(hub), wallet);

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

    private static async Task InHotel(Func<Hotel, Task> run)
    {
        var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!) { AllowUserVariables = true, Pooling = false };
        await using var admin = new MySqlConnection(options.ConnectionString);
        await admin.OpenAsync();
        var schema = "collectibles_hub_" + Guid.NewGuid().ToString("N");
        await admin.ExecuteAsync($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            options.Database = schema;
            await using var connection = new MySqlConnection(options.ConnectionString);
            await connection.OpenAsync();

            foreach (var file in new[] { "Database/FreshInstall.sql", "Database/Migrations/108_CollectiblesHub.sql", "Database/Migrations/108_CollectiblesHub.sql" }) {
                foreach (var statement in SqlScript.Split(await File.ReadAllTextAsync(HabbiconPacketTests.Repo(file)))) {
                    await connection.ExecuteAsync(statement, commandTimeout: 300);
                }
            }

            await Seed(connection);
            var database = new HabbiconDatabaseTests.TestDatabase(options.ConnectionString);
            var settings = new SettingsManager(database, NullLogger<SettingsManager>.Instance);
            await settings.Reload();
            await run(new Hotel(connection, new CollectiblesHubService(database, settings, new FixedClock(Now)), settings));
        }
        finally {
            await admin.ExecuteAsync($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    // Spring Set: chair 10 + lamp 20 + pet 30 = 60, boost 50, a bonus and a reward, released yesterday, snapshot tomorrow.
    // Winter Set: one badge worth 15, nothing else. Retired Set is disabled.
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

            INSERT INTO collectible_shop_offers (product_code, emerald_price, is_featured, is_limited, mint_limit, sort_order, enabled) VALUES
            ('winter_badge', 5, 0, 0, -1, 2, 1),
            ('spring_chair', 20, 1, 1, 3, 1, 1),
            ('spring_lamp', 8, 0, 0, -1, 0, 0);

            INSERT INTO collectible_mint_token_offers (id, product_code, silver_price, amount_tokens, sort_order, enabled) VALUES
            (11, 'mint_tokens_10', 100, 10, 2, 1),
            (12, 'mint_tokens_50', 400, 50, 1, 1),
            (13, 'mint_tokens_old', 1, 1000, 0, 0);

            INSERT INTO collectible_mintable_items (id, item_type, sprite_id, product_code, token_price, starts_at, ends_at, region_locked, limited_edition, enabled) VALUES
            (1, 's', 5001, 'spring_pet', 3, NULL, NULL, 0, 1, 1),
            (2, 'i', 5002, 'spring_lamp', 2, @dayAgo, @inADay, 1, 0, 1),
            (3, 's', 5003, 'spring_chair', 1, NULL, NULL, 0, 0, 0),
            (4, 's', 5004, 'spring_chair', 1, @dayAgo, @hourAgo, 0, 0, 1),
            (5, 's', 5005, 'spring_chair', 1, @inAnHour, NULL, 0, 0, 1);

            INSERT INTO collectible_reward_claims (claim_id, product_code, set_id, set_name, min_level, claim_limit, valid_from, valid_to, created_at, updated_at) VALUES
            ('oct_double', 'spring_chair', 'spring_set', 'Spring Set', 1, 2, @dayAgo, @inADay, @created, @updated),
            ('oct_badge', 'winter_badge', 'winter_set', 'Winter Set', 1, 1, @dayAgo, @inADay, @created, @updated),
            ('oct_level5', 'spring_pet', 'spring_set', 'Spring Set', 5, 1, @dayAgo, @inADay, @created, @updated),
            ('sep_badge', 'winter_badge', 'winter_set', 'Winter Set', 1, 1, @monthAgo, @hourAgo, @created, @updated),
            ('nov_badge', 'winter_badge', 'winter_set', 'Winter Set', 1, 1, @inAnHour, @monthAhead, @created, @updated);
            """, times);
    }
}
