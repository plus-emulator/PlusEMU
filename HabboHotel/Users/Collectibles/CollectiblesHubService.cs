using Plus.Core.Settings;
using Plus.Database;
using Plus.HabboHotel.GameClients;

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

public sealed class CollectiblesHubService(IDatabase database, ISettingsManager settings, TimeProvider clock) : ICollectiblesHubService
{
    public Task SendWallets(GameClient session) => throw new NotImplementedException();
    public Task SendCollectorScore(GameClient session, string wallet) => throw new NotImplementedException();
    public Task SendCollections(GameClient session, string wallet) => throw new NotImplementedException();
    public Task ClaimCollectionBonus(GameClient session, string collectionId, string wallet) => throw new NotImplementedException();
    public Task ClaimCollectionReward(GameClient session, string collectionId, string wallet) => throw new NotImplementedException();
    public Task SendStoreOffers(GameClient session) => throw new NotImplementedException();
    public Task PurchaseStoreOffer(GameClient session, string productCode, string wallet) => throw new NotImplementedException();
    public Task SendMintableItemTypes(GameClient session) => throw new NotImplementedException();
    public Task SendMintingEnabled(GameClient session) => throw new NotImplementedException();
    public Task SendMintTokenOffers(GameClient session) => throw new NotImplementedException();
    public Task SendMintTokens(GameClient session, string wallet) => throw new NotImplementedException();
    public Task PurchaseMintTokens(GameClient session, int offerId, string wallet) => throw new NotImplementedException();
    public Task Mint(GameClient session, int itemId, string wallet) => throw new NotImplementedException();
    public Task SendTransferFee(GameClient session) => throw new NotImplementedException();
    public Task Transfer(GameClient session, string wallet) => throw new NotImplementedException();
    public Task SendRewardClaims(GameClient session, string wallet) => throw new NotImplementedException();
    public Task ClaimRewards(GameClient session) => throw new NotImplementedException();
}
