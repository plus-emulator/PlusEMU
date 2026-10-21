using Microsoft.IO;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming.Catalog;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Subscriptions;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public class CatalogStructureWireTests
{
    [Fact]
    public void MissingCatalogTextIsWrittenAsTheSameEmptyStrings()
    {
        static CatalogIndexSnapshot Index(string? text) =>
            new("NORMAL", [new CatalogIndexNode(true, 1, 2, -1, text, text, [], [])]);
        static CatalogPageSnapshot Page(string? text, int itemType) =>
            new(9, "NORMAL", "frontpage4", [], [], [], -1, [new CatalogPromotionSnapshot(1, text, text, itemType, 0, null, text, 60)]);

        Assert.Equal(Encode(new CatalogIndexComposer(Index(""))), Encode(new CatalogIndexComposer(Index(null))));
        Assert.Equal(Encode(new CatalogPageComposer(Page("", CatalogPromotion.CataloguePageItem))),
            Encode(new CatalogPageComposer(Page(null, CatalogPromotion.CataloguePageItem))));
    }

    private static byte[] Encode(IServerPacket composer)
    {
        using var stream = (RecyclableMemoryStream)new RecyclableMemoryStreamManager().GetStream();
        composer.Compose(new FlashOutgoingPacket(stream));

        return stream.ToArray()[6..];
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UserPerksListTheHabboPerksInOrderWithADenialKeyOnlyWhenDenied(bool cameraAllowed)
    {
        var packet = new HabbiconTestSupport.RecordingPacket();

        new UserPerksComposer(cameraAllowed).Compose(packet);

        // Observed on AIR WIN63-202610201032-107964540: 12 perks, and the denial key is empty for an allowed perk.
        var perks = packet.Writes.Skip(1).Chunk(3).Select(perk => ((string)perk[0], (string)perk[1], (bool)perk[2])).ToArray();
        Assert.Equal(12, packet.Writes[0]);
        Assert.Equal(1 + 12 * 3, packet.Writes.Count);
        Assert.Equal(
            [
                ("JUDGE_CHAT_REVIEWS", "", true),
                ("NAVIGATOR_PHASE_TWO_2014", "", true),
                ("TRADE", "", true),
                ("CALL_ON_HELPERS", "", false),
                ("CITIZEN", "", true),
                ("BUILDER_AT_WORK", "requirement.unfulfilled.group_membership", false),
                ("HABBO_CLUB_OFFER_BETA", "", false),
                ("USE_GUIDE_TOOL", "requirement.unfulfilled.helper_level_4", false),
                ("MOUSE_ZOOM", "", true),
                ("NAVIGATOR_ROOM_THUMBNAIL_CAMERA", "", cameraAllowed),
                ("VOTE_IN_COMPETITIONS", "", true),
                ("CAMERA", "", cameraAllowed)
            ],
            perks);
    }

    [Fact]
    public void TheBuildersClubIndexHasOnlyBuildersClubPages()
    {
        var pages = new[] { Page(1, -1) };
        var catalog = CatalogSnapshotTestSupport.Proxy<ICatalogManager>((method, _) => method == "get_Pages" ? pages : throw new InvalidOperationException(method));
        var (client, sent) = HabbiconTestSupport.Client(EditorTestSupport.Player());
        var packet = HabbiconTestSupport.Incoming("BUILDERS_CLUB");

        var service = new CatalogBrowsingService(null!, null!, null!, TimeProvider.System, catalog, CatalogSnapshotTestSupport.Snapshots());
        service.ShowIndex(client, packet.ReadString());

        Assert.False(packet.HasDataRemaining());
        Assert.Equal([ServerPacketHeader.CatalogIndexComposer, ServerPacketHeader.CatalogItemDiscountComposer], sent.Select(value => value.Header));
        Assert.Equal(Encode(new CatalogIndexComposer(new CatalogIndexSnapshot(CatalogModes.BuildersClub, []))), sent[0].Payload);
    }

    [Fact]
    public async Task EmptyBundleDiscountRequestRepliesWithTheRuleset()
    {
        var (client, sent) = HabbiconTestSupport.Client(EditorTestSupport.Player());

        await new GetBundleDiscountRulesetEvent().Parse(client, HabbiconTestSupport.Incoming());

        Assert.Equal([ServerPacketHeader.CatalogItemDiscountComposer], sent.Select(value => value.Header));
    }

    private static CatalogPage Page(int id, int parentId, bool enabled = true, string? requiredPermission = null, params int[] offerIds)
    {
        var page = new CatalogPage { Id = id, ParentId = parentId, Enabled = enabled, Visible = true, Icon = id, Link = "page" + id, Caption = "Page " + id, Layout = "default_3x3", RequiredPermission = requiredPermission };

        foreach (var offerId in offerIds) {
            page.Offers[offerId] = new CatalogOffer { Id = offerId };
        }

        return page;
    }

    [Fact]
    public void IndexWritesEveryLevelForTheRequestedModeAndHidesPagesRequiringMissingPermissions()
    {
        var (client, _) = HabbiconTestSupport.Client(EditorTestSupport.Player());
        CatalogPage[] pages =
        [
            Page(1, -1), Page(2, 1, offerIds: 7), Page(3, 2), Page(4, 3, enabled: false), Page(5, 4),
            Page(6, 1, requiredPermission: EditorTestSupport.RestrictedPagePermission)
        ];
        var packet = new HabbiconTestSupport.RecordingPacket();

        new CatalogIndexComposer(CatalogSnapshotTestSupport.Snapshots().CaptureIndex(client.GetHabbo(), pages, CatalogModes.Normal)).Compose(packet);

        Assert.Equal(new List<object>
        {
            true, 0, -1, "root", "", 0, 1,
            true, 1, 1, "page1", "Page 1", 0, 1,
            true, 2, 2, "page2", "Page 2", 1, 7, 1,
            true, 3, 3, "page3", "Page 3", 0, 1,
            true, 4, -1, "page4", "Page 4", 0, 1,
            true, 5, 5, "page5", "Page 5", 0, 0,
            false, "NORMAL"
        }, packet.Writes);
    }

    [Fact]
    public void HiddenPagesStayOpenableWhileDisabledAndPermissionLockedPagesDoNot()
    {
        var user = EditorTestSupport.Player();
        var hidden = Page(1, -1);
        hidden.Visible = false;

        Assert.True(hidden.CanOpen(user));
        Assert.False(Page(2, -1, enabled: false).CanOpen(user));
        Assert.False(Page(3, -1, requiredPermission: EditorTestSupport.RestrictedPagePermission).CanOpen(user));
    }

    [Fact]
    public void PageAccessFollowsPermissionGrantsRegardlessOfRoleWeight()
    {
        var page = Page(3, -1, requiredPermission: EditorTestSupport.RestrictedPagePermission);
        var lowWeight = new Habbo { Access = EditorTestSupport.Access([EditorTestSupport.RestrictedPagePermission], weight: 1) };
        var highWeight = new Habbo { Access = EditorTestSupport.Access([], weight: 1000) };

        Assert.True(page.CanOpen(lowWeight));
        Assert.False(page.CanOpen(highWeight));
    }

    [Fact]
    public void DisabledOffersAreLeftOutOfPagesIndexAndClubGifts()
    {
        var (client, _) = HabbiconTestSupport.Client(EditorTestSupport.Player());
        var page = Page(1, -1, offerIds: [7, 8]);

        foreach (var offer in page.Offers.Values) {
            offer.Products = [new CatalogProduct { Type = CatalogProductType.Furni, Definition = new ItemDefinition { ItemName = "chair", SpriteId = 3, Type = ItemType.Floor } }];
        }

        page.Offers[8].Enabled = false;
        var snapshots = new CatalogSnapshotService(CatalogSnapshotTestSupport.Proxy<ICatalogManager>((method, _) =>
            method == "get_Promotions" ? Array.Empty<CatalogPromotion>() : throw new InvalidOperationException(method)), TimeProvider.System);

        Assert.Equal([7], snapshots.CapturePage(page, -1).Offers.Select(offer => offer.OfferId));
        Assert.Equal([7], Assert.Single(snapshots.CaptureIndex(client.GetHabbo(), [page], CatalogModes.Normal).Roots).OfferIds.ToArray());

        var gifts = snapshots.CaptureClubGifts(new ClubGiftInfo(1, 1, 0, [new ClubGift(page.Offers[7], 0), new ClubGift(page.Offers[8], 0)]));
        Assert.Equal([7], gifts.Offers.Select(offer => offer.OfferId));
        Assert.Equal([7], gifts.Gifts.Select(gift => gift.OfferId));
    }

    [Fact]
    public void FrontPagePromotionsWriteTheFieldTheirTypeNeeds()
    {
        var now = new DateTimeOffset(2040, 1, 2, 3, 4, 5, TimeSpan.FromHours(9));
        var page = new CatalogPromotion { Position = 2, Title = "Offer", Image = "a.png", ItemType = CatalogPromotion.ProductOfferItem, OfferId = 77, ExpiresAt = now.AddSeconds(60) };

        Assert.False(page.HasExpiredAt(now));
        Assert.Equal(TimeSpan.FromMinutes(1), page.RemainingAt(now));
        Assert.True(new CatalogPromotion { ExpiresAt = now.AddSeconds(-1) }.HasExpiredAt(now));
        Assert.Equal(TimeSpan.Zero, new CatalogPromotion().RemainingAt(now));
    }

    [Fact]
    public void SharedOffersStayOnEveryPageAndLookupsSkipPagesTheUserCannotOpen()
    {
        var user = EditorTestSupport.Player();
        var staff = Page(1, -1, requiredPermission: EditorTestSupport.RestrictedPagePermission);
        var normal = Page(2, -1);
        var shared = new CatalogOffer { Id = 6 };
        staff.Offers[6] = shared;
        normal.Offers[6] = shared;
        var index = new CatalogOfferIndex();
        index.Build([staff, normal]);

        Assert.True(index.TryGet(6, user, out var found, out var offer));
        Assert.Equal(2, found.Id);
        Assert.Same(shared, offer);
        Assert.False(index.TryGet(99, user, out _, out _));
    }
}
