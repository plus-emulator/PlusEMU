using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.MysteryBoxes;
using Plus.Communication.Packets.Outgoing.Rooms.Notifications;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Plus.Utilities.DependencyInjection;

namespace Plus.HabboHotel.Items.MysteryBoxes;

/// <summary>The colour of a user's own box and of the key they hold. An empty colour means none.</summary>
public sealed record MysteryBoxColors(string Box, string Key)
{
    public static readonly MysteryBoxColors None = new("", "");
}

public sealed record MysteryBoxPrize(ItemDefinition Definition, int Weight);

/// <summary>Opens a box: the box gets its opened state, the guest's key is used up and both users get their prize.</summary>
public sealed record MysteryBoxOpening(uint BoxItemId, string OpenedState, int OwnerId, int GuestId, string KeyColor,
    ItemDefinition OwnerPrize, ItemDefinition GuestPrize);

public sealed record MysteryBoxOpened(InventoryItem OwnerPrize, InventoryItem GuestPrize);

public interface IMysteryBoxStore
{
    MysteryBoxColors Colors(int userId);

    IReadOnlyList<MysteryBoxPrize> Prizes();

    /// <summary>Returns null when the guest no longer holds that key.</summary>
    MysteryBoxOpened? Open(MysteryBoxOpening opening);
}

[Singleton]
public interface IMysteryBoxService
{
    /// <summary>Uses the room item when it is a mystery box. Returns false when the item is not a mystery box.</summary>
    bool TryUse(Room room, GameClient session, uint itemId);

    void CancelWait(Room room, GameClient session, int boxOwnerId);

    /// <summary>Sends the user's box and key colours when they have either.</summary>
    void SendKeys(GameClient session);
}

/// <summary>
/// A box opens when its owner and a guest with a key of the box colour both use it, in either order.
/// The first one waits. A guest who waits puts the key in, so the box shows as unlocked until the guest cancels or leaves.
/// </summary>
public sealed class MysteryBoxService(IMysteryBoxStore store) : IMysteryBoxService
{
    private readonly Lock _sync = new();
    private readonly Dictionary<uint, Waiter> _waiting = [];

    public bool TryUse(Room room, GameClient session, uint itemId)
    {
        var box = room.GetRoomItemHandler().GetItem(itemId);

        if (box?.Definition?.InteractionType != InteractionType.MysteryBox) {
            return false;
        }

        var habbo = session.GetHabbo();

        lock (box) {
            if (!ReferenceEquals(habbo.CurrentRoom, room) || !ReferenceEquals(room.GetRoomItemHandler().GetItem(itemId), box)) {
                return true;
            }

            var isOwner = box.OwnerId == habbo.Id;
            var role = isOwner ? "owner" : "guest";
            var key = isOwner ? "" : store.Colors(habbo.Id).Key;

            if (!isOwner && key.Length == 0) {
                Notify(session, "no_key");

                return true;
            }

            var ownerBox = store.Colors((int)box.OwnerId).Box;
            var color = MysteryBoxColor.IndexOf(ownerBox);
            var state = MysteryBoxColor.State(box.LegacyDataString);

            if (color < 0 || state <= 0) {
                Notify(session, "inactive." + role);

                return true;
            }

            if (state == MysteryBoxColor.Opened(color)) {
                Notify(session, "already_opened." + role);

                return true;
            }

            var waiter = LiveWaiter(room, box);

            if (waiter == null && state == MysteryBoxColor.Unlocked(color)) {
                SetState(box, MysteryBoxColor.Locked(color));
            }

            if (isOwner) {
                if (waiter is { IsOwner: false }) {
                    Open(box, color, session, waiter.Session);
                }
                else {
                    Wait(room, box, session, true);
                }

                return true;
            }

            if (waiter is { IsOwner: false } && waiter.UserId != habbo.Id) {
                Notify(session, "already_unlocked.guest");
            }
            else if (MysteryBoxColor.IndexOf(key) != color) {
                Notify(session, "wrong_key");
            }
            else if (waiter is { IsOwner: true }) {
                Open(box, color, waiter.Session, session);
            }
            else {
                Wait(room, box, session, false);
                SetState(box, MysteryBoxColor.Unlocked(color));
            }
        }

        return true;
    }

    public void CancelWait(Room room, GameClient session, int boxOwnerId)
    {
        var userId = session.GetHabbo().Id;
        uint[] mine;

        lock (_sync) {
            mine = _waiting.Where(entry => entry.Value.UserId == userId).Select(entry => entry.Key).ToArray();
        }

        foreach (var itemId in mine) {
            var box = room.GetRoomItemHandler().GetItem(itemId);

            if (box == null || (boxOwnerId >= 0 && box.OwnerId != boxOwnerId)) {
                continue;
            }

            lock (box) {
                lock (_sync) {
                    if (!_waiting.TryGetValue(itemId, out var waiter) || waiter.UserId != userId) {
                        continue;
                    }

                    _waiting.Remove(itemId);
                }

                Relock(box);
            }
        }
    }

    public void SendKeys(GameClient session)
    {
        var colors = store.Colors(session.GetHabbo().Id);

        if (colors.Box.Length != 0 || colors.Key.Length != 0) {
            session.Send(new MysteryBoxKeysComposer(colors));
        }
    }

    private void Open(Item box, int color, GameClient owner, GameClient guest)
    {
        lock (_sync) {
            _waiting.Remove(box.Id);
        }

        var ownerId = owner.GetHabbo().Id;
        var guestId = guest.GetHabbo().Id;
        var prizes = store.Prizes();
        var ownerPrize = Pick(prizes);
        var guestPrize = Pick(prizes);
        var opened = ownerPrize == null || guestPrize == null
            ? null
            : store.Open(new(box.Id, MysteryBoxColor.Opened(color).ToString(), ownerId, guestId, store.Colors(guestId).Key, ownerPrize, guestPrize));

        if (opened == null) {
            Relock(box);

            foreach (var session in new[] { owner, guest }) {
                session.Send(new CancelMysteryBoxWaitComposer());
                Notify(session, "unexpected");
            }

            return;
        }

        SetState(box, MysteryBoxColor.Opened(color));
        Deliver(owner, opened.OwnerPrize);
        Deliver(guest, opened.GuestPrize);
        guest.Send(new MysteryBoxKeysComposer(store.Colors(guestId)));
    }

    private void Wait(Room room, Item box, GameClient session, bool isOwner)
    {
        lock (_sync) {
            _waiting[box.Id] = new(room, session, session.GetHabbo().Id, isOwner);
        }

        session.Send(new ShowMysteryBoxWaitComposer());
    }

    private Waiter? LiveWaiter(Room room, Item box)
    {
        lock (_sync) {
            foreach (var itemId in _waiting.Where(entry => !entry.Value.IsIn(entry.Value.Room)).Select(entry => entry.Key).ToArray()) {
                _waiting.Remove(itemId);
            }

            return _waiting.TryGetValue(box.Id, out var waiter) && ReferenceEquals(waiter.Room, room) ? waiter : null;
        }
    }

    private static void Relock(Item box)
    {
        var state = MysteryBoxColor.State(box.LegacyDataString);

        if (MysteryBoxColor.IsUnlocked(state)) {
            SetState(box, state - 1);
        }
    }

    private static void SetState(Item box, int state)
    {
        box.LegacyDataString = state.ToString();
        box.UpdateUsedState();
    }

    private static void Deliver(GameClient session, InventoryItem prize)
    {
        var habbo = session.GetHabbo();

        lock (habbo.InventoryMutationSync) {
            habbo.Inventory?.Furniture.AddItem(prize);
        }

        session.Send(new FurniListNotificationComposer(prize.Id, prize.IsFloorItem ? 1 : 2));
        session.Send(new FurniListAddComposer(InventoryItemSnapshot.Capture(prize)));
        session.Send(new GotMysteryBoxPrizeComposer(prize.Definition.ProductType, prize.Definition.SpriteId));
    }

    private static void Notify(GameClient session, string error) =>
        session.Send(new RoomNotificationComposer("mysterybox.error." + error));

    private static ItemDefinition? Pick(IReadOnlyList<MysteryBoxPrize> prizes)
    {
        var total = prizes.Where(prize => prize.Weight > 0).Sum(prize => (long)prize.Weight);

        if (total <= 0) {
            return null;
        }

        var roll = Random.Shared.NextInt64(total);

        foreach (var prize in prizes.Where(prize => prize.Weight > 0)) {
            if (roll < prize.Weight) {
                return prize.Definition;
            }

            roll -= prize.Weight;
        }

        return null;
    }

    private sealed record Waiter(Room Room, GameClient Session, int UserId, bool IsOwner)
    {
        public bool IsIn(Room room) =>
            Session.GetHabbo() is { } habbo && habbo.Id == UserId && ReferenceEquals(habbo.CurrentRoom, room)
            && room.GetRoomUserManager().GetRoomUserByHabbo(UserId) != null;
    }
}

/// <summary>
/// Box colours in furni state order. State 0 is an inactive box. Each colour then has three states: locked, unlocked and opened.
/// </summary>
public static class MysteryBoxColor
{
    public static readonly IReadOnlyList<string> Names = ["purple", "blue", "green", "yellow", "lilac", "orange", "turquoise", "red"];

    public static int IndexOf(string? name) =>
        string.IsNullOrWhiteSpace(name) ? -1 : Names.ToList().IndexOf(name.Trim().ToLowerInvariant());

    public static int Locked(int color) => 1 + 3 * color;

    public static int Unlocked(int color) => 2 + 3 * color;

    public static int Opened(int color) => 3 + 3 * color;

    public static bool IsUnlocked(int state) => state > 0 && state % 3 == 2;

    public static int State(string? data) => int.TryParse(data, out var state) && state >= 0 && state <= 3 * Names.Count ? state : 0;
}
