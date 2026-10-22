using System.Data;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.Communication.Packets.Outgoing.Rooms.Notifications;
using Plus.Core.Settings;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.HabboHotel.Catalog.BuildersClub;

// What BuildersClubSubscriptionStatus carries. A user without a membership is on the trial: no seconds left.
public sealed record BuildersClubStatus(int SecondsLeft, int FurniLimit, int MaxFurniLimit, int SecondsLeftWithGrace)
{
    public bool IsMember => SecondsLeft > 0;
}

// The Builders Club catalogue page and offer a placement names, and the colour or other parameter the catalogue chose.
public readonly record struct BuildersClubPlacement(int PageId, int OfferId, string ExtraParam);

public interface IBuildersClubService
{
    BuildersClubStatus Status(Habbo habbo);
    void SendStatus(GameClient session);
    void SendFurniCount(GameClient session);
    // A placement that would hide the room is answered with BuildersClubPlacementWarning until the client sends it confirmed.
    void PlaceFloorItem(Room room, GameClient session, BuildersClubPlacement request, int x, int y, int rotation, bool confirmed = false);
    void PlaceWallItem(Room room, GameClient session, BuildersClubPlacement request, string wallLocation, bool confirmed = false);
    // Whether the room holds Builders Club furni of a user whose membership and its grace have run out: such a room is hidden.
    bool HidesRoom(Room room);
    // Removes a Builders Club furni from its room and deletes it; it never goes to an inventory.
    bool TryPickUp(GameClient session, Room room, Item item);
    // Whether the user may buy the product now: furni for the limit need a running membership.
    bool CanBuy(Habbo habbo, CatalogProduct product);
    // Delivers a bought membership or furni limit inside the purchase's transaction; false refuses the purchase.
    bool Deliver(IDbConnection connection, IDbTransaction transaction, Habbo habbo, CatalogProduct product);
}

/// <summary>
/// Builders Club furni are placed straight from the BUILDERS_CLUB catalogue into a room and count against the placer's furni
/// limit. The rules mirror AIR's HabboCatalog.getBuilderFurniPlaceableStatus (WIN63-202610151745-318700621,
/// com/sulake/habbo/catalog/HabboCatalog.as:2842-2889): the count must be under the limit, the placer must own the room or
/// control it as a group admin (room controller level 3 or more), and without a membership (the trial) nobody else may be in
/// the room.
/// </summary>
public sealed class BuildersClubService(ICatalogManager catalog, IBuildersClubStore store, ISettingsManager settings, IGameClientManager clients,
    TimeProvider clock, ILogger<BuildersClubService> logger) : IBuildersClubService
{
    // Observed on hhus at login for a member: furni limit 500, max furni limit 2000000.
    public const int DefaultMemberFurniLimit = 500;
    public const int DefaultTrialFurniLimit = 50;
    public const int DefaultMaxFurniLimit = 2_000_000;

    public BuildersClubStatus Status(Habbo habbo)
    {
        var now = clock.GetUtcNow();
        var membership = store.Membership(habbo.Id);
        var grace = TimeSpan.FromSeconds(Setting("builders_club.grace_seconds", 0));
        var secondsLeft = SecondsUntil(membership?.ExpiresAt, now);
        var withGrace = membership is { } running ? SecondsUntil(running.ExpiresAt + grace, now) : 0;
        var max = Setting("builders_club.max_furni_limit", DefaultMaxFurniLimit);
        // Bought furni (builders_club_addon offers) count while the membership or its grace runs.
        var limit = secondsLeft > 0 || withGrace > 0
            ? (int)Math.Min((long)Math.Max(Setting("builders_club.furni_limit", DefaultMemberFurniLimit), membership!.FurniLimit) + membership.FurniAddon, max)
            : Setting("builders_club.trial_furni_limit", DefaultTrialFurniLimit);

        return new(secondsLeft, limit, Math.Max(limit, max), Math.Max(secondsLeft, withGrace));
    }

    public bool CanBuy(Habbo habbo, CatalogProduct product) => product.Type switch
    {
        CatalogProductType.BuildersClub => product.Amount > 0,
        CatalogProductType.BuildersClubAddon => product.Amount > 0 && Status(habbo).IsMember,
        _ => false
    };

    public bool Deliver(IDbConnection connection, IDbTransaction transaction, Habbo habbo, CatalogProduct product)
    {
        switch (product.Type) {
            case CatalogProductType.BuildersClub when product.Amount > 0:
                store.Extend(connection, transaction, habbo.Id, product.Amount,
                    int.TryParse(product.ExtraParam, NumberStyles.None, CultureInfo.InvariantCulture, out var furniLimit) ? furniLimit : 0, clock.GetUtcNow());

                return true;
            case CatalogProductType.BuildersClubAddon when product.Amount > 0:
                return store.AddFurni(connection, transaction, habbo.Id, product.Amount, clock.GetUtcNow());
            default:
                return false;
        }
    }

    public void SendStatus(GameClient session) => session.Send(new BuildersClubSubscriptionStatusComposer(Status(session.GetHabbo())));

    public void SendFurniCount(GameClient session) => session.Send(new BuildersClubFurniCountComposer(store.FurniCount(session.GetHabbo().Id)));

    public void PlaceFloorItem(Room room, GameClient session, BuildersClubPlacement request, int x, int y, int rotation, bool confirmed = false)
    {
        if (Prepare(room, session, request, ItemType.Floor, confirmed,
                () => BuildersClubPlacementWarningComposer.Floor(request.PageId, request.OfferId, request.ExtraParam, x, y, rotation)) is not { } item) {
            return;
        }

        bool placed;

        try {
            // Observed on AIR WIN63-202610201032-107964540: the count that includes the new furni comes before its ObjectAdd. The
            // stored row counts only once the room saves its position, after ObjectAdd, so the new furni is added here.
            var count = new BuildersClubFurniCountComposer(store.FurniCount(item.UserId) + 1);
            placed = room.GetRoomItemHandler().SetFloorItem(session, item, x, y, rotation, true, false, true, announcing: () => session.Send(count));
        }
        catch (Exception exception) {
            logger.LogError(exception, "Failed to place Builders Club furni {ItemId} in room {RoomId}", item.Id, room.Id);
            placed = false;
        }

        if (!placed) {
            Delete(item.Id);
            PlacementError(session);

            return;
        }

        if (item.IsWired) {
            try {
                room.GetWired().LoadWiredBox(item);
            }
            catch (Exception exception) {
                logger.LogError(exception, "Failed to load placed Builders Club Wired item {ItemId} in room {RoomId}", item.Id, room.Id);
            }
        }
    }

    public void PlaceWallItem(Room room, GameClient session, BuildersClubPlacement request, string wallLocation, bool confirmed = false)
    {
        if (Prepare(room, session, request, ItemType.Wall, confirmed,
                () => BuildersClubPlacementWarningComposer.Wall(request.PageId, request.OfferId, request.ExtraParam, wallLocation)) is not { } item) {
            return;
        }

        bool placed;

        try {
            var position = wallLocation.StartsWith(':') ? room.GetRoomItemHandler().WallPositionCheck(wallLocation) : null;

            if (position == null) {
                placed = false;
            }
            else {
                item.WallCoordinates = position;
                placed = room.GetRoomItemHandler().SetWallItem(session, item);
            }
        }
        catch (Exception exception) {
            logger.LogError(exception, "Failed to place Builders Club wall furni {ItemId} in room {RoomId}", item.Id, room.Id);
            placed = false;
        }

        if (!placed) {
            Delete(item.Id);
            PlacementError(session);

            return;
        }

        SendFurniCount(session);
    }

    public bool TryPickUp(GameClient session, Room room, Item item)
    {
        var habbo = session.GetHabbo();

        if (habbo == null || habbo.AccessClosed || !item.IsBuildersClub || !ReferenceEquals(habbo.CurrentRoom, room) || item.RoomId != room.Id) {
            return false;
        }

        if (item.UserId != habbo.Id && !habbo.Access.Can(PermissionKeys.RoomItemTake) && !room.CheckRights(session, false) &&
            !(room.Group != null && room.CheckRights(session, false, true))) {
            return false;
        }

        var placer = clients.GetClientByUserId(item.UserId) is { } client && client.GetHabbo() != null ? client : null;

        // Observed on AIR WIN63-202610201032-107964540: the placer's new furni count comes before the floor furni's ObjectRemove, so
        // it is read before the stored row goes. No capture shows the order for wall furni, so they keep sending it after ItemRemove.
        lock (item) {
            if (!ReferenceEquals(room.GetRoomItemHandler().GetItem(item.Id), item)) {
                return false;
            }

            var count = placer != null && item.IsFloorItem ? new BuildersClubFurniCountComposer(store.FurniCount(item.UserId) - 1) : null;

            if (!room.GetRoomItemHandler().RemoveFurniture(session, item, count == null ? null : () => placer!.Send(count))) {
                return false;
            }

            item.RoomId = 0;
        }

        Delete(item.Id);

        if (placer != null && !item.IsFloorItem) {
            SendFurniCount(placer);
        }

        return true;
    }

    public bool HidesRoom(Room room)
    {
        var now = clock.GetUtcNow();
        var grace = TimeSpan.FromSeconds(Setting("builders_club.grace_seconds", 0));
        var placers = room.GetRoomItemHandler().GetWallAndFloor.Where(item => item.IsBuildersClub).Select(item => item.UserId).Distinct().ToArray();

        return placers.Any(userId => store.Membership(userId) is not { } membership || membership.ExpiresAt + grace <= now);
    }

    // The furni a valid placement creates, stored but not yet in the room; null when the placement is refused or waits for the
    // placer to confirm that it hides the room.
    private Item? Prepare(Room room, GameClient session, BuildersClubPlacement request, ItemType type, bool confirmed, Func<IServerPacket> warning)
    {
        var habbo = session.GetHabbo();

        if (habbo == null || habbo.AccessClosed || !ReferenceEquals(habbo.CurrentRoom, room) || !TryGetOffer(habbo, request, out var offer)) {
            return null;
        }

        if (offer.IsBundle || offer.IsLimited || offer.Products.Count != 1 || offer.Product.Type != CatalogProductType.Furni || offer.Product.Amount > 1 ||
            offer.Product.Definition is not { } definition || definition.Type != type || !CanPlace(definition)) {
            return null;
        }

        var status = Status(habbo);

        if (!MayPlaceIn(room, session, status)) {
            PlacementError(session);

            return null;
        }

        if (ConfigurationItemStates.TypeLimitReached(room, definition)) {
            ConfigurationItemStates.SendTypeLimit(session);

            return null;
        }

        if (store.FurniCount(habbo.Id) >= status.FurniLimit ||
            room.GetRoomItemHandler().GetWallAndFloor.Count() > Setting("room.item.placement_limit", int.MaxValue)) {
            PlacementError(session);

            return null;
        }

        // Without a membership (or its grace) Builders Club furni hide the room, which the client confirms first
        // (room.confirm.hide_room, AIR WIN63-202610151745-318700621 com/sulake/habbo/room/class_2005.as:366-397).
        if (status.SecondsLeftWithGrace <= 0 && !confirmed && !HidesRoom(room)) {
            session.Send(warning());

            return null;
        }

        var extraData = ExtraData(definition, offer.Product.ExtraParam, request.ExtraParam);
        var item = new Item
        {
            Id = store.Create(definition.Id, habbo.Id, extraData),
            OwnerId = (uint)habbo.Id,
            UserId = habbo.Id,
            Username = habbo.Username,
            Definition = definition,
            ExtraData = FurniExtraData.Load(definition, extraData, keepLegacy: true),
            IsBuildersClub = true
        };
        BuildersClubItemIds.Assign(item.Id);

        return item;
    }

    private void Delete(uint itemId)
    {
        store.Delete(itemId);
        BuildersClubItemIds.Release(itemId);
    }

    private bool TryGetOffer(Habbo habbo, BuildersClubPlacement request, out CatalogOffer offer)
    {
        // The info stand's copy and the search results name the offer without a page.
        if (request.PageId < 0) {
            return catalog.TryGetBuildersClubOffer(request.OfferId, habbo, out _, out offer!);
        }

        if (catalog.TryGetPage(request.PageId, out var page) && page.IsBuildersClub && page.CanOpen(habbo) &&
            page.Offers.TryGetValue(request.OfferId, out offer!) && offer.Enabled) {
            return true;
        }

        offer = null!;

        return false;
    }

    // Room controller level 3 or more: the owner, or a group admin in a group room. Without a membership nobody else may be in
    // the room (AIR status 6, "visitors in room"); the room user list never marks anyone as a moderator.
    private static bool MayPlaceIn(Room room, GameClient session, BuildersClubStatus status)
    {
        var habbo = session.GetHabbo();

        if (!room.CheckRights(session, true) && !(room.Group != null && room.CheckRights(session, false, true))) {
            return false;
        }

        return status.IsMember || !room.GetRoomUserManager().GetUserList().Any(user => !user.IsBot && user.HabboId != habbo.Id);
    }

    // Furni that carry value or links of their own are never handed out by the Builders Club.
    public static bool CanPlace(ItemDefinition definition) => definition.InteractionType is not (InteractionType.Exchange or InteractionType.CrackableEgg
        or InteractionType.Gift or InteractionType.Teleport or InteractionType.GuildItem or InteractionType.GuildGate or InteractionType.GuildForum
        or InteractionType.Wallpaper or InteractionType.Floor or InteractionType.Landscape or InteractionType.Postit or InteractionType.Trophy
        or InteractionType.BadgeDisplay or InteractionType.Moodlight or InteractionType.Toner or InteractionType.Hopper or InteractionType.Tent
        or InteractionType.TentSmall or InteractionType.Badge or InteractionType.Effect or InteractionType.Deal or InteractionType.Roomdeal
        or InteractionType.Bot or InteractionType.Pet or InteractionType.PetBreedingBox or InteractionType.GnomeBox or InteractionType.MonsterplantSeed
        or InteractionType.PurchasableClothing or InteractionType.HorseHairstyle or InteractionType.HorseBodyDye or InteractionType.HorseHairDye
        or InteractionType.MusicDisc or InteractionType.Jukebox or InteractionType.CameraPicture or InteractionType.Mannequin
        or InteractionType.WiredChestFurni or InteractionType.WiredChestCoins);

    // A recolorable starts in the colour the catalogue's palette chose (RecolorableFurnitureCatalogWidget). A poster takes its number
    // from the offer and ignores the client's: observed for one poster offer on AIR WIN63-202610201032-107964540 (client "" and "53"
    // both gave the offer's "52"); other poster offers are assumed to work the same. Nothing else takes a parameter.
    // A wall item with more than one state starts at "0": observed for cine_light1 on the same build; other multi-state wall items are
    // assumed to work the same. A normal (non Builders Club) purchase of such an item was not observed, so it still starts at "".
    private static string ExtraData(ItemDefinition definition, string productParam, string clientParam) => definition switch
    {
        { InteractionType: InteractionType.Recolorable } => RecolorableDataFormat.FormatCatalogueColor(RecolorablePalette.PurchaseColor(
            definition.ItemName, RecolorableDataFormat.TryParseCatalogueColor(clientParam, out var chosen) ? chosen : null)),
        { Type: ItemType.Wall, ItemName: "poster" } => productParam,
        { Type: ItemType.Wall, InteractionType: InteractionType.None, Modes: > 1 } => "0",
        _ => ""
    };

    private static void PlacementError(GameClient session) =>
        session.Send(new RoomNotificationComposer("furni_placement_error", "message", "${room.error.cant_set_item}"));

    private static int SecondsUntil(DateTimeOffset? end, DateTimeOffset now) =>
        end is { } value && value > now ? (int)Math.Min(int.MaxValue, Math.Ceiling((value - now).TotalSeconds)) : 0;

    private int Setting(string key, int fallback) =>
        int.TryParse(settings.GetOptionalValue(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : fallback;
}
