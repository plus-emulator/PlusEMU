using Dapper;
using Microsoft.Extensions.Logging;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Core;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.HabboHotel.Catalog.Marketplace;

public interface IMarketplaceExpiryService
{
    Task<int> RunBatch();
}

public sealed class MarketplaceExpiryService(IDatabase database, IItemDataManager items, IGameClientManager clients,
    IAccountSessionGate accounts, IMarketplaceFeePolicy policy, TimeProvider clock, ILogger<MarketplaceExpiryService> logger)
    : IMarketplaceExpiryService, IStartable, IDisposable
{
    private Timer? _timer;
    private int _busy;
    private uint _cursor;

    public int StartOrder => 95;
    public Task Start()
    {
        _timer = new Timer(_ => _ = RunBatch(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));

        return Task.CompletedTask;
    }

    public async Task<int> RunBatch()
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) {
            return 0;
        }

        var returned = 0;

        try {
            var cutoff = clock.GetUtcNow().AddHours(-policy.OfferExpirationHours);
            ExpiredOffer[] offers;

            using (var connection = database.Connection()) {
                offers = connection.Query<ExpiredOffer>("""
                    SELECT offer_id AS OfferId,user_id AS UserId FROM catalog_marketplace_offers
                    WHERE state='1' AND (listed_at<@cutoff OR listed_at IS NULL) AND offer_id>@cursor
                    ORDER BY offer_id LIMIT 100
                    """, new { cutoff, cursor = _cursor }).ToArray();
            }

            if (offers.Length == 0) {
                _cursor = 0;
            }

            foreach (var offer in offers) {
                _cursor = offer.OfferId;

                try {
                    using var held = await accounts.EnterAsync(offer.UserId);
                    var habbo = clients.GetClientByUserId(offer.UserId)?.GetHabbo();

                    if (habbo is null) {
                        if (Return(offer, cutoff) is not null) {
                            returned++;
                        }

                        continue;
                    }

                    lock (habbo.InventoryMutationSync)
                        lock (habbo.WalletSync) {
                            if (habbo.WalletClosed || habbo.Inventory is not { } inventory) {
                                continue;
                            }

                            if (MarketplaceCommit.Guard(habbo, () => Return(offer, cutoff)) is not { } item) {
                                continue;
                            }

                            if (!inventory.Furniture.AddItem(item)) {
                                throw new InvalidOperationException("Returned marketplace furniture was already loaded.");
                            }

                            returned++;
                            habbo.Client?.Send(new FurniListAddComposer(InventoryItemSnapshot.Capture(item)));
                            habbo.Client?.Send(new FurniListNotificationComposer(item.Id, item.IsFloorItem ? 1 : 2));
                            habbo.Client?.Send(new FurniListUpdateComposer());
                        }
                }
                catch (Exception exception) {
                    logger.LogError(exception, "Could not return expired marketplace offer {OfferId}", offer.OfferId);
                }
            }
        }
        catch (Exception exception) {
            logger.LogError(exception, "Could not process marketplace expirations");
        }
        finally {
            Volatile.Write(ref _busy, 0);
        }

        return returned;
    }

    private InventoryItem? Return(ExpiredOffer offer, DateTimeOffset cutoff) =>
        new MarketplaceCancellationStore(database).Cancel(offer.UserId, offer.OfferId, id => items.Items.GetValueOrDefault(id),
            delivery => new InventoryItem
            {
                Id = delivery.FurniId,
                OwnerId = (uint)offer.UserId,
                Definition = delivery.Definition,
                ExtraData = FurniExtraData.Load(delivery.Definition, delivery.ExtraData, keepLegacy: true),
                UniqueNumber = delivery.LimitedNumber,
                UniqueSeries = delivery.LimitedStack
            }, cutoff);

    public void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class ExpiredOffer
    {
        public uint OfferId { get; set; }
        public int UserId { get; set; }
    }
}
