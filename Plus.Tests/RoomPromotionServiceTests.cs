using System.Reflection;
using Plus.Communication.Packets.Incoming.Catalog;
using Plus.Communication.Packets.Incoming.Navigator;
using Plus.Communication.Packets.Outgoing;
using Plus.Core.Settings;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Friends;
using Plus.HabboHotel.Navigator;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Badges;
using Xunit;

namespace Plus.Tests;

public sealed class RoomPromotionServiceTests
{
    private static readonly DateTimeOffset Now = new(2040, 12, 31, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NonOwnerAndMissingRoomDoNotPersistOrSend()
    {
        var context = new Context();
        context.Data.OwnerId = 99;
        await context.Service.Purchase(context.Client, new(1, 2, 42, "title", "details", 9));
        context.Service.Edit(context.Client, new(42, "title", "details"));
        context.Loader.Data = null;
        await context.Service.Purchase(context.Client, new(1, 2, 42, "title", "details", 9));
        Assert.Equal(0, context.Store.Writes);
        Assert.Equal(0, context.Rewards.Charges);
        Assert.Equal(0, context.Clock.Reads);
        Assert.Empty(context.Sent);
    }

    [Fact]
    public async Task PurchaseWithoutACatalogOfferIsNotFree()
    {
        var context = new Context(credits: 3, points: 15);

        await context.Service.Purchase(context.Client, new(0, 0, 42, "title", "details", 9));

        AssertNothingBought(context);
    }

    [Theory]
    [InlineData("missing offer")]
    [InlineData("missing page")]
    [InlineData("offer on another page layout")]
    [InlineData("offer on a page with the room ad link as layout")]
    [InlineData("disabled offer")]
    [InlineData("disabled page")]
    [InlineData("club offer")]
    public async Task WrongOfferBuysNothing(string wrong)
    {
        var context = new Context(credits: 3, points: 15);
        var pageId = 1;
        var offerId = 2;

        switch (wrong) {
            case "missing offer":
                offerId = 3;
                break;
            case "missing page":
                pageId = 5;
                break;
            case "offer on another page layout":
                context.Page.Layout = "default_3x3";
                break;
            case "offer on a page with the room ad link as layout":
                context.Page.Layout = "room_ad";
                break;
            case "disabled offer":
                context.Offer.Enabled = false;
                break;
            case "disabled page":
                context.Page.Enabled = false;
                break;
            case "club offer":
                context.Offer.ClubLevel = 2;
                break;
        }

        await context.Service.Purchase(context.Client, new(pageId, offerId, 42, "title", "details", 9));

        AssertNothingBought(context);
    }

    [Theory]
    [InlineData(2, 50)]
    [InlineData(100, 14)]
    public async Task NotEnoughBalanceBuysNothing(int credits, int duckets)
    {
        var context = new Context(credits: 3, points: 15);
        context.Habbo.Credits = credits;
        context.Habbo.Duckets = duckets;

        await context.Service.Purchase(context.Client, new(1, 2, 42, "title", "details", 9));

        AssertNothingBought(context, credits, duckets);
    }

    [Theory]
    [InlineData(9, "abcd", "details")]
    [InlineData(9, "abcdefghijklmnopqrstuvwxyz", "details")]
    [InlineData(9, "title", "")]
    [InlineData(11, "title", "details")]
    [InlineData(-1, "title", "details")]
    public async Task InvalidCategoryOrTextBuysNothing(int categoryId, string name, string description)
    {
        var context = new Context(credits: 3, points: 15);
        description = description.Length == 0 ? new string('d', 101) : description;

        await context.Service.Purchase(context.Client, new(1, 2, 42, name, description, categoryId));

        AssertNothingBought(context);
    }

    [Fact]
    public async Task PaidPurchaseChargesTheOfferInTheSameTransactionAndEachExtensionPaysAgain()
    {
        var context = new Context(credits: 3, points: 15);
        context.Habbo.Duckets = 40;
        var name = new string('n', 25);
        var description = new string('d', 100);

        await context.Service.Purchase(context.Client, new(1, 2, 42, name, description, 9));

        Assert.Equal(1, context.Rewards.Charges);
        Assert.Equal((97, 25), (context.Habbo.Credits, context.Habbo.Duckets));
        Assert.Same(context.Rewards.Transaction, Assert.Single(context.Store.Transactions));
        Assert.Equal((name, description, 9), (context.Data.Promotion!.Name, context.Data.Promotion.Description, context.Data.Promotion.CategoryId));
        Assert.Equal(Now.AddMinutes(120), context.Data.Promotion.ExpiresAt);
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.CreditBalanceComposer);
        Assert.Contains(context.Sent, packet => packet.Header == ServerPacketHeader.HabboActivityPointNotificationComposer);
        Assert.Equal(1, context.StatusUpdates);

        await context.Service.Purchase(context.Client, new(1, 2, 42, "extended", "details", 10));

        Assert.Equal(2, context.Rewards.Charges);
        Assert.Equal((94, 10), (context.Habbo.Credits, context.Habbo.Duckets));
        Assert.Equal(Now.AddHours(4), context.Data.Promotion.ExpiresAt);

        context.Sent.Clear();
        await context.Service.Purchase(context.Client, new(1, 2, 42, "extended", "details", 10));

        Assert.Equal((94, 10), (context.Habbo.Credits, context.Habbo.Duckets));
        Assert.Equal(Now.AddHours(4), context.Data.Promotion.ExpiresAt);
        Assert.Equal(2, context.Store.Writes);
        Assert.DoesNotContain(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
    }

    [Fact]
    public async Task RefusedChargeSavesAndAnnouncesNothing()
    {
        var context = new Context(credits: 3, points: 15, chargeSucceeds: false);
        var existing = new RoomPromotion("old title", "details", 9, Now, Now.AddMinutes(15), context.Clock);
        context.Data.Promotion = existing;

        await context.Service.Purchase(context.Client, new(1, 2, 42, "new title", "new details", 10));

        Assert.Equal(1, context.Rewards.Charges);
        Assert.Equal(0, context.Store.Writes);
        Assert.Same(existing, context.Data.Promotion);
        Assert.Equal(("old title", 9, Now.AddMinutes(15)), (existing.Name, existing.CategoryId, existing.ExpiresAt));
        Assert.Equal((100, 50), (context.Habbo.Credits, context.Habbo.Duckets));
        Assert.Empty(context.Sent);
        Assert.Equal(0, context.StatusUpdates);
    }

    private static void AssertNothingBought(Context context, int credits = 100, int duckets = 50)
    {
        Assert.Equal(0, context.Store.Writes);
        Assert.Null(context.Data.Promotion);
        Assert.Equal((credits, duckets), (context.Habbo.Credits, context.Habbo.Duckets));
        Assert.DoesNotContain(context.Sent, packet => packet.Header == ServerPacketHeader.PurchaseOKComposer);
    }

    [Fact]
    public async Task FailedPersistenceLeavesPromotionAndPacketsUnchanged()
    {
        var context = new Context();
        var existing = new RoomPromotion("old", "details", 3, Now, Now.AddMinutes(15), context.Clock);
        context.Data.Promotion = existing;
        context.Store.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.Purchase(context.Client, new(1, 2, 42, "new title", "new details", 9)));
        Assert.Throws<InvalidOperationException>(() => context.Service.Edit(context.Client, new(42, "edited", "edited details")));
        Assert.Same(existing, context.Data.Promotion);
        Assert.Equal("old", existing.Name);
        Assert.Equal(Now.AddMinutes(15), existing.ExpiresAt);
        Assert.Empty(context.Sent);
    }

    [Fact]
    public async Task PurchaseAndExtensionPublishAfterPersistenceUsingOneClockRead()
    {
        var context = new Context();
        context.Store.BeforeWrite = () =>
        {
            Assert.Null(context.Data.Promotion);
            Assert.Empty(context.Sent);
        };
        await context.Service.Purchase(context.Client, new(1, 2, 42, "title", "details", 9));
        var first = context.Data.Promotion;
        Assert.Equal(Now, first.StartedAt);
        Assert.Equal(Now.AddMinutes(120), first.ExpiresAt);
        Assert.Equal(9, first.CategoryId);
        Assert.Equal(1, context.Clock.Reads);
        Assert.Single(context.Sent);

        context.Store.BeforeWrite = () => Assert.Same(first, context.Data.Promotion);
        await context.Service.Purchase(context.Client, new(1, 2, 42, "extended", "details", 10));
        Assert.Equal(Now, context.Data.Promotion.StartedAt);
        Assert.Equal(Now.AddHours(4), context.Data.Promotion.ExpiresAt);
        Assert.Equal(10, context.Data.Promotion.CategoryId);
        Assert.Equal(2, context.Clock.Reads);
        Assert.Equal(2, context.Store.Writes);
    }

    [Fact]
    public void EditPersistsBeforeChangingTheExistingPromotion()
    {
        var context = new Context();
        var existing = new RoomPromotion("old", "details", 3, Now, Now.AddHours(1), context.Clock);
        context.Data.Promotion = existing;
        context.Store.BeforeWrite = () => Assert.Equal("old", existing.Name);
        context.Service.Edit(context.Client, new(42, "edited", "edited details"));
        Assert.Equal("edited", existing.Name);
        Assert.Equal("edited details", existing.Description);
        Assert.Equal(Now.AddHours(1), existing.ExpiresAt);
        Assert.Equal(0, context.Clock.Reads);
    }

    [Fact]
    public async Task HandlersDecodeEveryWireFieldAndDelegate()
    {
        var context = new Context();
        var capture = new CaptureService();
        await new PurchaseRoomAdEvent(capture).Parse(context.Client,
            HabbiconTestSupport.Incoming(7, 8, 42, "title", false, "details", 9));
        await new EditRoomPromotionEvent(capture).Parse(context.Client,
            HabbiconTestSupport.Incoming(42, "edited", "more details"));
        Assert.Equal(new PurchaseRoomPromotionRequest(7, 8, 42, "title", "details", 9), capture.PurchaseRequest);
        Assert.Equal(new EditRoomPromotionRequest(42, "edited", "more details"), capture.EditRequest);
    }

    private sealed class Context
    {
        public RoomData Data { get; } = new() { Id = 42, OwnerId = 7, OwnerName = "owner" };
        public Loader Loader { get; }
        public Store Store { get; } = new();
        public CatalogPurchaseServiceTests.RecordingRewards Rewards { get; }
        public int StatusUpdates { get; private set; }
        public Clock Clock { get; } = new();
        public Plus.HabboHotel.GameClients.GameClient Client { get; }
        public List<(uint Header, byte[] Payload)> Sent { get; }
        public RoomPromotionService Service { get; }
        public Habbo Habbo { get; }
        public CatalogPage Page { get; } = new() { Id = 1, Enabled = true, Layout = "roomads" };
        public CatalogOffer Offer { get; }

        public Context(int credits = 0, int points = 0, bool chargeSucceeds = true)
        {
            Loader = new() { Data = Data };
            Rewards = new(chargeSucceeds);
            Habbo = new Habbo
            {
                Id = 7,
                Credits = 100,
                Duckets = 50,
                Inventory = new InventoryComponent
                {
                    Badges = new BadgesInventoryComponent(new() { ["RADZZ"] = new("RADZZ", 0) })
                }
            };
            (Client, Sent) = HabbiconTestSupport.Client(Habbo);
            Offer = new CatalogOffer
            {
                Id = 2,
                LocalizationKey = "room_ad_plus_badge",
                CostCredits = credits,
                CostPoints = points,
                PointsType = 0,
                Products = [new CatalogProduct { Type = CatalogProductType.Badge, BadgeCode = "RADZZ" }]
            };
            Page.Offers.Add(Offer.Id, Offer);
            var catalog = System.Reflection.DispatchProxy.Create<ICatalogManager, CatalogPurchaseServiceTests.CatalogProxy>();
            ((CatalogPurchaseServiceTests.CatalogProxy)(object)catalog).Page = Page;
            SearchResultList[] categories =
            [
                new(9, "roomads_view", "promotion_parties_music", "Parties & Music", true, 1, "", NavigatorViewMode.Regular, "promotion_category", "SHOW_MORE", 1),
                new(10, "roomads_view", "promotion_roleplay", "Role Play", true, 1, "", NavigatorViewMode.Regular, "promotion_category", "SHOW_MORE", 2)
            ];
            Service = new(Loader, Proxy<IRoomManager>((_, args) => { args[1] = null; return false; }), Store,
                Proxy<IWordFilterManager>((_, args) => args[0]),
                Proxy<ISettingsManager>((_, _) => "120"),
                Proxy<IBadgeManager>((_, _) => throw new InvalidOperationException("Unexpected badge write.")),
                Proxy<IMessengerDataLoader>((method, _) =>
                {
                    if (method == nameof(IMessengerDataLoader.BroadcastStatusUpdate)) {
                        StatusUpdates++;
                    }

                    return null;
                }), catalog, Rewards,
                Proxy<INavigatorManager>((method, _) => method == "get_EventCategories" ? categories : null), Clock);
        }
    }

    private sealed class Loader : IRoomDataLoader
    {
        public RoomData? Data { get; set; }
        public bool TryGetData(uint roomId, out RoomData? data)
        {
            data = Data;

            return data != null;
        }
        public int? LastOwnerId { get; private set; }
        public List<RoomData> GetRoomsDataByOwnerSortByName(int ownerId)
        {
            LastOwnerId = ownerId;

            return Data == null ? [] : [Data];
        }
    }

    private sealed class Store : IRoomPromotionStore
    {
        public int Writes { get; private set; }
        public bool Fail { get; set; }
        public Action? BeforeWrite { get; set; }
        public List<System.Data.IDbTransaction> Transactions { get; } = [];
        public void Save(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, uint roomId, int ownerId, RoomPromotion promotion)
        {
            Write();
            Transactions.Add(transaction);
        }
        public void Edit(uint roomId, int ownerId, string name, string description) => Write();
        private void Write()
        {
            BeforeWrite?.Invoke();

            if (Fail) {
                throw new InvalidOperationException("Forced persistence failure.");
            }

            Writes++;
        }
    }

    private sealed class Clock : TimeProvider
    {
        public int Reads { get; private set; }
        public override DateTimeOffset GetUtcNow()
        {
            Reads++;

            return Now;
        }
    }

    private sealed class CaptureService : IRoomPromotionService
    {
        public PurchaseRoomPromotionRequest? PurchaseRequest { get; private set; }
        public EditRoomPromotionRequest? EditRequest { get; private set; }
        public Task Purchase(Plus.HabboHotel.GameClients.GameClient session, PurchaseRoomPromotionRequest request)
        {
            PurchaseRequest = request;

            return Task.CompletedTask;
        }
        public void Edit(Plus.HabboHotel.GameClients.GameClient session, EditRoomPromotionRequest request) => EditRequest = request;
    }

    private static T Proxy<T>(Func<string, object?[], object?> callback) where T : class
    {
        var proxy = DispatchProxy.Create<T, CallbackProxy>();
        ((CallbackProxy)(object)proxy).Callback = callback;

        return proxy;
    }

    public class CallbackProxy : DispatchProxy
    {
        public Func<string, object?[], object?> Callback { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Callback(targetMethod!.Name, args!);
    }
}
