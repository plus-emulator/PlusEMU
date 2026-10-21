using System.Collections.Immutable;
using System.Data;
using System.Security.Cryptography;
using Dapper;
using Plus.Communication.Packets.Outgoing.Collectibles;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Core.Settings;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.HabboHotel.Users.Collectibles;

/// <summary>
/// The Collectibles hub in the catalog (AIR catalog/collectibles): wallets, collector score, collections, shop, minting, transfer and
/// monthly reward claims. Habbo keeps collectibles on a blockchain; this hotel keeps them in the collectible_ tables. Each method
/// answers the session itself.
/// </summary>
public interface ICollectiblesHubService
{
    Task SendWallets(GameClient session);
    Task SendCollectorScore(GameClient session, string wallet);
    Task SendCollections(GameClient session, string wallet);
    Task ClaimCollectionBonus(GameClient session, string collectionId, string wallet);
    Task ClaimCollectionReward(GameClient session, string collectionId, string wallet);
    Task SendStoreOffers(GameClient session);
    Task PurchaseStoreOffer(GameClient session, string productCode, string wallet);
    Task SendMintableItemTypes(GameClient session);
    Task SendMintingEnabled(GameClient session);
    Task SendMintTokenOffers(GameClient session);
    Task SendMintTokens(GameClient session, string wallet);
    Task PurchaseMintTokens(GameClient session, int offerId, string wallet);
    Task Mint(GameClient session, int itemId, string wallet);
    Task SendTransferFee(GameClient session);
    Task Transfer(GameClient session, string wallet);
    Task SendRewardClaims(GameClient session, string wallet);
    Task ClaimRewards(GameClient session);
}

// Every write that spends or grants runs in one transaction that first locks the wallet row, so two quick clicks queue up and the
// second one sees what the first one committed. Balances of an online user live in habbo.Currencies (saved at logout), so a spend checks
// them under WalletSync and writes the new balance to user_currencies in the same transaction.
public sealed class CollectiblesHubService(IDatabase database, ISettingsManager settings, TimeProvider clock) : ICollectiblesHubService
{
    private const string MintingEnabledSetting = "collectibles.minting.enabled";
    private const string TransferFeeSetting = "collectibles.transfer.fee";

    private const string ProductSelect = """
        SELECT product_code AS ProductCode, collection_id AS CollectionId, product_type AS ProductType, item_type_id AS ItemTypeId, score AS Score,
            rarity AS Rarity, pet_figure AS PetFigure, figure_set_ids AS FigureSetIds
        FROM collectible_products ORDER BY sort_order, product_code
        """;

    private const string StoreOfferSelect = """
        SELECT o.product_code AS ProductCode, o.emerald_price AS EmeraldPrice, o.is_featured AS IsFeatured, o.is_limited AS IsLimited,
            o.mint_limit AS MintLimit, (SELECT COUNT(*) FROM user_collectibles c WHERE c.product_code = o.product_code) AS MintedCount
        FROM collectible_shop_offers o WHERE o.enabled = 1
        """;

    private const string MintableSelect = """
        SELECT id AS Id, item_type AS ItemType, sprite_id AS SpriteId, product_code AS ProductCode, token_price AS TokenPrice, starts_at AS StartsAt,
            ends_at AS EndsAt, region_locked AS RegionLocked, limited_edition AS LimitedEdition
        FROM collectible_mintable_items WHERE enabled = 1 AND (ends_at IS NULL OR ends_at > @now)
        """;

    private const string TokenOfferSelect =
        "SELECT id AS Id, product_code AS ProductCode, silver_price AS SilverPrice, amount_tokens AS AmountTokens FROM collectible_mint_token_offers WHERE enabled = 1";

    private enum TransferResult : short
    {
        Moved = 0,
        NotALinkedWallet = 1,
        NotEnoughSilver = 2
    }

    public Task SendWallets(GameClient session) => Run(session, habbo =>
    {
        using (var connection = database.Connection()) {
            var collector = CollectorWallet(connection, null, habbo.Id);
            var linked = connection.Query<string>("SELECT address FROM collectible_wallets WHERE user_id = @userId AND is_collector = 0 ORDER BY id",
                new { userId = habbo.Id });
            session.Send(new CollectibleWalletAddressesComposer(collector.Address, [.. linked]));
        }

        session.Send(new SilverBalanceComposer(habbo.Currencies[ActivityPointType.Silver]));
        session.Send(new EmeraldBalanceComposer(habbo.Currencies[ActivityPointType.Emerald]));
    });

    public Task SendCollectorScore(GameClient session, string wallet) => Run(session, habbo =>
    {
        using var connection = database.Connection();

        if (FindWallet(connection, null, habbo.Id, wallet) is not { } owned) {
            return;
        }

        var book = Book(connection, null, owned.Address);
        var score = book.Score;

        if (score > owned.HighestScore) {
            connection.Execute("UPDATE collectible_wallets SET highest_score = GREATEST(highest_score, @score) WHERE address = @address",
                new { score, address = owned.Address });
        }

        session.Send(new CollectorScoreComposer(score, Math.Max(score, owned.HighestScore), book.Level));
    });

    public Task SendCollections(GameClient session, string wallet) => Run(session, habbo =>
    {
        using var connection = database.Connection();

        if (FindWallet(connection, null, habbo.Id, wallet) is { } owned) {
            session.Send(new NftCollectionsComposer(Book(connection, null, owned.Address), clock.GetUtcNow()));
        }
    });

    public Task ClaimCollectionBonus(GameClient session, string collectionId, string wallet) => Run(session, habbo =>
        session.Send(new NftCollectionBonusClaimResultComposer(collectionId, wallet, TryClaimSetPrize(habbo.Id, collectionId, wallet, SetPrize.Bonus))));

    public Task ClaimCollectionReward(GameClient session, string collectionId, string wallet) => Run(session, habbo =>
        session.Send(new NftCollectionRewardClaimResultComposer(collectionId, wallet, TryClaimSetPrize(habbo.Id, collectionId, wallet, SetPrize.Reward))));

    public Task SendStoreOffers(GameClient session) => Run(session, _ =>
    {
        using var connection = database.Connection();
        var products = Products(connection, null);
        var offers = connection.Query<NftStoreOffer>(StoreOfferSelect + " ORDER BY o.sort_order, o.product_code");
        session.Send(new NftStoreOffersComposer([.. offers.Select(offer => (offer, products[offer.ProductCode].Product))]));
    });

    public Task PurchaseStoreOffer(GameClient session, string productCode, string wallet) => Run(session, habbo =>
    {
        int? emeralds;

        lock (habbo.WalletSync) {
            emeralds = habbo.WalletClosed ? null : TryBuyStoreOffer(habbo, productCode, wallet);
        }

        session.Send(new NftStorePurchaseResultComposer((short)(emeralds is null ? 1 : 0)));

        if (emeralds is { } balance) {
            session.Send(new EmeraldBalanceComposer(balance));
        }
    });

    public Task SendMintableItemTypes(GameClient session) => Run(session, _ =>
    {
        using var connection = database.Connection();
        var items = connection.Query<MintableItem>(MintableSelect + " ORDER BY id", new { now = clock.GetUtcNow().UtcDateTime });
        session.Send(new CollectibleMintableItemTypesComposer([.. items]));
    });

    public Task SendMintingEnabled(GameClient session) => Run(session, _ => session.Send(new CollectibleMintingEnabledComposer(MintingEnabled)));

    public Task SendMintTokenOffers(GameClient session) => Run(session, _ =>
    {
        using var connection = database.Connection();
        session.Send(new MintTokenOffersComposer([.. connection.Query<MintTokenOffer>(TokenOfferSelect + " ORDER BY sort_order, id")]));
    });

    public Task SendMintTokens(GameClient session, string wallet) => Run(session, habbo =>
    {
        using var connection = database.Connection();

        if (FindWallet(connection, null, habbo.Id, wallet) is { } owned) {
            session.Send(new CollectibleMintTokensComposer(owned.MintTokens));
        }
    });

    public Task PurchaseMintTokens(GameClient session, int offerId, string wallet) => Run(session, habbo =>
    {
        (int Silver, int Tokens)? bought;

        lock (habbo.WalletSync) {
            bought = habbo.WalletClosed ? null : TryBuyMintTokens(habbo, offerId, wallet);
        }

        if (bought is { } result) {
            session.Send(new SilverBalanceComposer(result.Silver));
            session.Send(new CollectibleMintTokensComposer(result.Tokens));
        }
    });

    public Task Mint(GameClient session, int itemId, string wallet) => Run(session, habbo =>
    {
        lock (habbo.WalletSync) {
            if (habbo.WalletClosed || !MintingEnabled || habbo.Inventory is not { } inventory
                || inventory.Furniture.GetItem((uint)itemId) is not { } item || item.OwnerId != (uint)habbo.Id || !item.TryReserve()) {
                session.Send(new MintCollectibleResultComposer(0));

                return;
            }

            try {
                if (!ReferenceEquals(inventory.Furniture.GetItem(item.Id), item) || TryMint(habbo.Id, item, wallet) is not { } tokens) {
                    session.Send(new MintCollectibleResultComposer(0));

                    return;
                }

                inventory.Furniture.RemoveItem(item.Id);
                session.Send(new FurniListRemoveComposer(item.Id));
                session.Send(new CollectibleMintTokensComposer(tokens));
                session.Send(new MintCollectibleResultComposer(1));
            }
            finally {
                item.ReleaseReservation();
            }
        }
    });

    public Task SendTransferFee(GameClient session) => Run(session, _ => session.Send(new NftTransferFeeComposer(TransferFee)));

    public Task Transfer(GameClient session, string wallet) => Run(session, habbo =>
    {
        (TransferResult Result, int Silver)? transfer;

        lock (habbo.WalletSync) {
            transfer = habbo.WalletClosed ? null : TryTransfer(habbo, wallet);
        }

        if (transfer is not { } done) {
            return;
        }

        session.Send(new NftTransferResultComposer((short)done.Result));

        if (done.Result == TransferResult.Moved) {
            session.Send(new SilverBalanceComposer(done.Silver));
        }
    });

    public Task SendRewardClaims(GameClient session, string wallet) => Run(session, habbo =>
    {
        using var connection = database.Connection();

        if (FindWallet(connection, null, habbo.Id, wallet) is not { IsCollector: true } collector) {
            session.Send(new NftClaimsComposer(wallet, []));

            return;
        }

        var products = Products(connection, null);
        var claims = ReachableClaims(connection, null, collector.Address, Book(connection, null, collector.Address).Level);
        session.Send(new NftClaimsComposer(collector.Address, [.. claims.Select(claim => (claim, products[claim.ProductCode].Product))]));
    });

    public Task ClaimRewards(GameClient session) => Run(session, habbo =>
    {
        int given;

        using (var connection = database.Connection()) {
            connection.Open();
            using var transaction = connection.BeginTransaction();
            var collector = CollectorWallet(connection, transaction, habbo.Id, forUpdate: true);
            var open = ReachableClaims(connection, transaction, collector.Address, Book(connection, transaction, collector.Address).Level)
                .Where(claim => claim.Open).ToList();

            foreach (var claim in open) {
                connection.Execute("""
                    INSERT INTO user_collectible_reward_claims (wallet_address, claim_id, claimed_amount) VALUES (@address, @claimId, 1)
                    ON DUPLICATE KEY UPDATE claimed_amount = claimed_amount + 1
                    """, new { address = collector.Address, claimId = claim.ClaimId }, transaction);
                Grant(connection, transaction, collector.Address, claim.ProductCode, "reward_claim");
            }

            transaction.Commit();
            given = open.Count;
        }

        session.Send(new NftClaimResultComposer((short)(given > 0 ? 0 : 1)));
    });

    private bool MintingEnabled => settings.TryGetValue(MintingEnabledSetting) == "1";

    private int TransferFee => int.TryParse(settings.TryGetValue(TransferFeeSetting), out var fee) && fee > 0 ? fee : 0;

    private static Task Run(GameClient session, Action<Habbo> action)
    {
        if (session.GetHabbo() is { } habbo) {
            action(habbo);
        }

        return Task.CompletedTask;
    }

    private bool TryClaimSetPrize(int userId, string collectionId, string wallet, SetPrize prize)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (FindWallet(connection, transaction, userId, wallet, forUpdate: true) is not { } owned) {
            return false;
        }

        var book = Book(connection, transaction, owned.Address);

        if (book.Sets.FirstOrDefault(set => set.Id == collectionId) is not { } set || !book.CanClaim(set, prize, clock.GetUtcNow())) {
            return false;
        }

        connection.Execute("INSERT INTO user_collectible_collection_claims (wallet_address, collection_id, kind) VALUES (@address, @setId, @kind)",
            new { address = owned.Address, setId = set.Id, kind = prize == SetPrize.Bonus ? "bonus" : "reward" }, transaction);
        Grant(connection, transaction, owned.Address, set.Prize(prize)!.ProductCode, prize == SetPrize.Bonus ? "collection_bonus" : "collection_reward");
        transaction.Commit();

        return true;
    }

    private int? TryBuyStoreOffer(Habbo habbo, string productCode, string wallet)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (FindWallet(connection, transaction, habbo.Id, wallet, forUpdate: true) is not { } owned) {
            return null;
        }

        var offer = connection.QuerySingleOrDefault<NftStoreOffer>(StoreOfferSelect + " AND o.product_code = @productCode FOR UPDATE", new { productCode }, transaction);

        // The offer lock queues buyers; the locking count sees every committed sale, not this transaction's older snapshot.
        if (offer is null || offer.MintLimit >= 0 && connection.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM user_collectibles WHERE product_code = @productCode LOCK IN SHARE MODE", new { productCode }, transaction) >= offer.MintLimit) {
            return null;
        }

        if (Spend(connection, transaction, habbo, ActivityPointType.Emerald, offer.EmeraldPrice) is not { } emeralds) {
            return null;
        }

        Grant(connection, transaction, owned.Address, offer.ProductCode, "shop");
        transaction.Commit();
        habbo.Currencies[ActivityPointType.Emerald] = emeralds;

        return emeralds;
    }

    private (int Silver, int Tokens)? TryBuyMintTokens(Habbo habbo, int offerId, string wallet)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (FindWallet(connection, transaction, habbo.Id, wallet, forUpdate: true) is not { } owned
            || connection.QuerySingleOrDefault<MintTokenOffer>(TokenOfferSelect + " AND id = @offerId", new { offerId }, transaction) is not { AmountTokens: > 0 } offer
            || Spend(connection, transaction, habbo, ActivityPointType.Silver, offer.SilverPrice) is not { } silver) {
            return null;
        }

        connection.Execute("UPDATE collectible_wallets SET mint_tokens = mint_tokens + @amount WHERE address = @address",
            new { amount = offer.AmountTokens, address = owned.Address }, transaction);
        transaction.Commit();
        habbo.Currencies[ActivityPointType.Silver] = silver;

        return (silver, owned.MintTokens + offer.AmountTokens);
    }

    private int? TryMint(int userId, InventoryItem item, string wallet)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (FindWallet(connection, transaction, userId, wallet, forUpdate: true) is not { } owned) {
            return null;
        }

        var mintable = connection.QuerySingleOrDefault<MintableItem>(
            MintableSelect + " AND item_type = @type AND sprite_id = @spriteId AND (starts_at IS NULL OR starts_at <= @now)",
            new { now = clock.GetUtcNow().UtcDateTime, type = item.Definition.Type == ItemType.Wall ? "i" : "s", spriteId = item.Definition.SpriteId },
            transaction);

        if (mintable is null || owned.MintTokens < mintable.TokenPrice
            || connection.Execute("DELETE FROM items WHERE id = @id AND user_id = @userId AND room_id = 0", new { id = item.Id, userId }, transaction) != 1) {
            return null;
        }

        connection.Execute("UPDATE collectible_wallets SET mint_tokens = mint_tokens - @price WHERE address = @address",
            new { price = mintable.TokenPrice, address = owned.Address }, transaction);
        Grant(connection, transaction, owned.Address, mintable.ProductCode, "mint");
        transaction.Commit();

        return owned.MintTokens - mintable.TokenPrice;
    }

    private (TransferResult Result, int Silver) TryTransfer(Habbo habbo, string wallet)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (FindWallet(connection, transaction, habbo.Id, wallet, forUpdate: true) is not { IsCollector: false } linked) {
            return (TransferResult.NotALinkedWallet, 0);
        }

        var collector = CollectorWallet(connection, transaction, habbo.Id, forUpdate: true);

        if (Spend(connection, transaction, habbo, ActivityPointType.Silver, TransferFee) is not { } silver) {
            return (TransferResult.NotEnoughSilver, 0);
        }

        connection.Execute("UPDATE user_collectibles SET wallet_address = @collector WHERE wallet_address = @linked",
            new { collector = collector.Address, linked = linked.Address }, transaction);
        transaction.Commit();
        habbo.Currencies[ActivityPointType.Silver] = silver;

        return (TransferResult.Moved, silver);
    }

    // The caller stores the returned balance in habbo.Currencies once the transaction commits.
    private static int? Spend(IDbConnection connection, IDbTransaction transaction, Habbo habbo, int type, int price)
    {
        var balance = habbo.Currencies[type] - price;

        if (price < 0 || balance < 0) {
            return null;
        }

        UserCurrencyStore.Get(connection, habbo.Id, type, transaction, forUpdate: true);
        UserCurrencyStore.Set(connection, habbo.Id, type, balance, transaction);

        return balance;
    }

    // Locking the product row keeps serial numbers unique when two wallets get the same product at once.
    private static void Grant(IDbConnection connection, IDbTransaction transaction, string wallet, string productCode, string source)
    {
        connection.ExecuteScalar<string>("SELECT product_code FROM collectible_products WHERE product_code = @productCode FOR UPDATE", new { productCode }, transaction);
        connection.Execute("""
            INSERT INTO user_collectibles (wallet_address, product_code, serial_number, source)
            SELECT @wallet, @productCode, COALESCE(MAX(serial_number), 0) + 1, @source FROM user_collectibles WHERE product_code = @productCode
            """, new { wallet, productCode, source }, transaction);
    }

    private static WalletRow? FindWallet(IDbConnection connection, IDbTransaction? transaction, int userId, string address, bool forUpdate = false) =>
        connection.QuerySingleOrDefault<WalletRow>(
            "SELECT address AS Address, is_collector AS IsCollector, mint_tokens AS MintTokens, highest_score AS HighestScore FROM collectible_wallets " +
            "WHERE address = @address AND user_id = @userId" + (forUpdate ? " FOR UPDATE" : ""), new { address, userId }, transaction);

    // Made on first use. The unique collector key keeps it to one wallet per user, so a request that loses a race reads the winner's row.
    private static WalletRow CollectorWallet(IDbConnection connection, IDbTransaction? transaction, int userId, bool forUpdate = false)
    {
        const string select = "SELECT address AS Address, is_collector AS IsCollector, mint_tokens AS MintTokens, highest_score AS HighestScore " +
            "FROM collectible_wallets WHERE user_id = @userId AND is_collector = 1";
        var lockClause = forUpdate ? " FOR UPDATE" : "";

        if (connection.QuerySingleOrDefault<WalletRow>(select + lockClause, new { userId }, transaction) is { } wallet) {
            return wallet;
        }

        connection.Execute("INSERT IGNORE INTO collectible_wallets (address, user_id, is_collector) VALUES (@address, @userId, 1)",
            new { address = "0x" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20)), userId }, transaction);

        return connection.QuerySingle<WalletRow>(select + lockClause, new { userId }, transaction);
    }

    private static Dictionary<string, ProductRow> Products(IDbConnection connection, IDbTransaction? transaction) =>
        connection.Query<ProductRow>(ProductSelect, transaction: transaction).ToDictionary(row => row.ProductCode);

    private static CollectorBook Book(IDbConnection connection, IDbTransaction? transaction, string wallet)
    {
        var products = Products(connection, transaction);
        var sets = connection.Query<SetRow>("""
            SELECT id AS Id, name AS Name, boost_score AS BoostScore, bonus_product_code AS BonusProductCode, reward_product_code AS RewardProductCode,
                released_at AS ReleasedAt, snapshot_at AS SnapshotAt
            FROM collectible_collections WHERE enabled = 1 ORDER BY sort_order, id
            """, transaction: transaction)
            .Select(row => new CollectibleSet(row.Id, row.Name, row.BoostScore,
                [.. products.Values.Where(product => product.CollectionId == row.Id).Select(product => product.Product)],
                Prize(row.BonusProductCode), Prize(row.RewardProductCode), row.ReleasedAt, row.SnapshotAt));
        var levels = connection.Query<CollectorLevel>("SELECT level AS Level, min_score AS MinScore FROM collectible_levels", transaction: transaction);
        var owned = connection.Query<(string ProductCode, long Count)>(
            "SELECT product_code, COUNT(*) FROM user_collectibles WHERE wallet_address = @wallet GROUP BY product_code", new { wallet }, transaction);
        var claimed = connection.Query<(string SetId, string Kind)>(
            "SELECT collection_id, kind FROM user_collectible_collection_claims WHERE wallet_address = @wallet", new { wallet }, transaction);

        return new CollectorBook([.. sets], products.ToDictionary(pair => pair.Key, pair => pair.Value.Product), [.. levels],
            owned.ToDictionary(row => row.ProductCode, row => (int)row.Count),
            claimed.Select(row => (row.SetId, row.Kind == "bonus" ? SetPrize.Bonus : SetPrize.Reward)).ToHashSet());

        CollectibleProduct? Prize(string? productCode) => productCode != null && products.TryGetValue(productCode, out var row) ? row.Product : null;
    }

    private List<NftRewardClaim> ReachableClaims(IDbConnection connection, IDbTransaction? transaction, string wallet, int level) =>
        connection.Query<NftRewardClaim>("""
            SELECT r.claim_id AS ClaimId, r.product_code AS ProductCode, r.set_id AS SetId, r.set_name AS SetName, r.claim_limit AS ClaimLimit,
                r.valid_from AS ValidFrom, r.valid_to AS ValidTo, r.created_at AS CreatedAt, r.updated_at AS UpdatedAt,
                CAST(COALESCE(u.claimed_amount, 0) AS SIGNED) AS Claimed
            FROM collectible_reward_claims r
            LEFT JOIN user_collectible_reward_claims u ON u.claim_id = r.claim_id AND u.wallet_address = @wallet
            WHERE r.valid_from <= @now AND r.valid_to > @now AND r.min_level <= @level
            ORDER BY r.claim_id
            """, new { wallet, level, now = clock.GetUtcNow().UtcDateTime }, transaction).ToList();

    private sealed record WalletRow(string Address, bool IsCollector, int MintTokens, int HighestScore);

    private sealed record SetRow(string Id, string Name, int BoostScore, string? BonusProductCode, string? RewardProductCode,
        DateTimeOffset? ReleasedAt, DateTimeOffset? SnapshotAt);

    private sealed record ProductRow(string ProductCode, string? CollectionId, short ProductType, string ItemTypeId, int Score, string Rarity, string PetFigure,
        string FigureSetIds)
    {
        public CollectibleProduct Product { get; } = new(ProductCode, ProductType, ItemTypeId, Score, Rarity, PetFigure,
            [.. FigureSetIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse)]);
    }
}
