using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>Output only, for one engine-owned continuous movement segment. Room commits remain immediate.</summary>
internal sealed class WiredFurniturePublication(Room room, long deadline, long epoch)
{
    private readonly Dictionary<Item, (WiredMovementComposer Movement, WiredMoveStyleComposer Style, long Placement, ItemDefinition Definition)> _entries = new(ReferenceEqualityComparer.Instance);
    private readonly List<Item> _order = [];
    private readonly List<(RoomUser Actor, long Lifetime, WiredMovementComposer Movement)> _avatars = [];
    private readonly List<(RoomUser Actor, long Lifetime, WiredUserDirectionComposer Turn)> _directions = [];
    // Firings that moved each furni, and furni whose firing ran with Execute in Order.
    private readonly Dictionary<Item, HashSet<object?>> _firings = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Item> _ordered = new(ReferenceEqualityComparer.Instance);
    internal long Deadline { get; } = deadline;
    internal long Epoch { get; } = epoch;
    internal bool Open { get; private set; } = true;

    internal bool Append(Item item, WiredMovementComposer movement, WiredMoveStyleComposer style, object? firing = null, bool executeInOrder = false)
    {
        if (!Open || !ReferenceEquals(item.GetRoom(), room) || !ReferenceEquals(room.GetRoomItemHandler().GetItem(item.Id), item)) {
            return false;
        }

        if (_entries.TryGetValue(item, out var previous)) {
            if (previous.Placement != item.Placement || !ReferenceEquals(previous.Definition, item.Definition)) {
                Flush();

                return false;
            }

            movement = movement with
            {
                FromX = previous.Movement.FromX,
                FromY = previous.Movement.FromY,
                FromZ = previous.Movement.FromZ
            };
        }

        // Observed on AIR WIN63-202610151745-318700621: the entry order does not follow which stack ran first, and no rule matched every
        // capture, so entries go in item id order. A group move puts its own entries back in pick order (see Reorder).
        if (!_entries.ContainsKey(item)) {
            var slot = _order.FindIndex(listed => listed.Id > item.Id);
            _order.Insert(slot < 0 ? _order.Count : slot, item);
        }

        _entries[item] = (movement, style, item.Placement, item.Definition);

        if (!_firings.TryGetValue(item, out var firings)) {
            _firings[item] = firings = new(ReferenceEqualityComparer.Instance);
        }

        firings.Add(firing);

        if (executeInOrder) {
            _ordered.Add(item);
        }

        return true;
    }

    /// <summary>Puts the given furni's entries in the given order, in the slots they already hold.</summary>
    internal void Reorder(IReadOnlyList<Item> order)
    {
        var wanted = order.Where(_entries.ContainsKey).Distinct(ReferenceEqualityComparer.Instance).Cast<Item>().ToArray();
        var slots = _order.Select((item, index) => (item, index)).Where(pair => wanted.Contains(pair.item, ReferenceEqualityComparer.Instance))
            .Select(pair => pair.index).ToArray();

        for (var i = 0; i < slots.Length && i < wanted.Length; i++) {
            _order[slots[i]] = wanted[i];
        }
    }

    internal bool HasAvatarEntries => _avatars.Count != 0;
    internal bool ContainsActor(RoomUser actor) => _avatars.Any(entry => ReferenceEquals(entry.Actor, actor));

    internal bool AppendAvatar(RoomUser actor, WiredMovementComposer movement, WiredMoveStyleComposer style)
    {
        var lifetime = actor.Movement.LifetimeId;

        if (!Open || ContainsActor(actor) || !LiveActor(actor, lifetime)) {
            return false;
        }

        _avatars.Add((actor, lifetime, movement));
        // Retention owns the animation even if this original hint's callback seals the segment.
        room.SendPacket(style);

        return true;
    }

    // Observed on AIR WIN63-202610151745-318700621: with the projectile add-on, the shooter's direction is the last entry of the
    // firing's WiredMovements.
    internal void AppendDirection(RoomUser shooter, long lifetime, WiredUserDirectionComposer turn)
    {
        _directions.RemoveAll(entry => ReferenceEquals(entry.Actor, shooter));
        _directions.Add((shooter, lifetime, turn));
    }

    private bool LiveActor(RoomUser actor, long lifetime) => actor.Movement.State != NavState.Removing
        && actor.Movement.LifetimeId == lifetime
        && ReferenceEquals(room.GetRoomUserManager().GetRoomUserByVirtualId(actor.VirtualId), actor);

    // Observed on AIR WIN63-202610131318-860371703: a furni that ends a firing where it started gets no movement entry without
    // Execute in Order, and one from == to entry with it. Moves of several firings (a Fast Queue signal chain) keep their entries.
    private bool NetZeroInOneFiring(Item item, WiredMovementComposer movement) => !_ordered.Contains(item)
        && movement.FromX == movement.ToX && movement.FromY == movement.ToY && movement.FromZ == movement.ToZ
        && _firings[item] is { Count: 1 } firings && firings.Single() is not null;

    internal void Flush()
    {
        if (!Open) {
            return;
        }

        Open = false;
        var live = _order.Where(item => ReferenceEquals(room.GetRoomItemHandler().GetItem(item.Id), item)
                && ReferenceEquals(item.GetRoom(), room) && item.Placement == _entries[item].Placement
                && ReferenceEquals(item.Definition, _entries[item].Definition))
            .Select(item => (Item: item, Entry: _entries[item])).ToArray();
        var furniMoves = live.Where(entry => !NetZeroInOneFiring(entry.Item, entry.Entry.Movement))
            .Select(entry => entry.Entry.Movement).ToArray();
        var avatars = _avatars.ToArray();
        var directions = _directions.ToArray();
        _entries.Clear();
        _order.Clear();
        _avatars.Clear();
        _directions.Clear();
        _firings.Clear();
        _ordered.Clear();

        var flushes = live.Length != 0 || avatars.Length != 0;

        if (flushes) {
            room.FlushWiredFlashData();
        }

        foreach (var entry in live) {
            room.SendPacket(entry.Entry.Style);
        }

        // A style callback can remove/reuse an actor; validate again immediately before the batch.
        var movements = furniMoves
            .Concat(avatars.Where(entry => LiveActor(entry.Actor, entry.Lifetime)).Select(entry => entry.Movement)).ToArray();
        var turns = directions.Where(entry => LiveActor(entry.Actor, entry.Lifetime)).Select(entry => entry.Turn).ToArray();

        if (movements.Length > 0 || turns.Length > 0) {
            room.SendPacket(new WiredMovementBatchComposer(movements, turns));
        }

        if (flushes) {
            room.FlushWiredFlashes();
        }
    }
}
