using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.Items.Wired.Modern.Addons;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>Mutates the actual room and emits the active renderer's animation format.</summary>
public sealed class WiredRoomMovement(Action<RoomUser, IEnumerable<Item>, IEnumerable<Item>> walkTransition, bool transparentWalkTransition = false)
{
    private const int ProjectileOvershootMs = 800;
    private Action<WiredRuntimeContext, IReadOnlyList<RoomUser>>? _prepareCarryPublication;
    private Func<WiredRuntimeContext, RoomUser, WiredMovementComposer, WiredMoveStyleComposer, bool>? _appendCarryPublication;

    internal bool WalkBindingIsCurrent(Action<RoomUser, IEnumerable<Item>, IEnumerable<Item>> walk) =>
        transparentWalkTransition && walkTransition.Equals(walk);

    internal void BindCarryPublication(Action<WiredRuntimeContext, IReadOnlyList<RoomUser>> prepare,
        Func<WiredRuntimeContext, RoomUser, WiredMovementComposer, WiredMoveStyleComposer, bool> append)
    {
        _prepareCarryPublication = prepare;
        _appendCarryPublication = append;
    }

    // A pressure pad only acts through Wired walk triggers, which the engine already seals the segment for.
    internal static bool PlainPublicationItem(Item item) => item.IsFloorItem
        && item.Definition.InteractionType is InteractionType.None or InteractionType.PressurePad or InteractionType.Recolorable && !item.Definition.IsSeat;

    public bool RestoreWallSnapshot(WiredRuntimeContext context, Item item, WiredFurniSnapshot snapshot,
        bool position = true, bool altitude = true)
    {
        if (!item.IsWallItem || snapshot.ItemId != item.Id || snapshot.DefinitionId != item.Definition.Id
            || snapshot.Wall is not { } wall || !wall.IsWithinLimits()
            || !context.FurniIdentity.TryGetValue(item.Id, out var captured) || !ReferenceEquals(item, captured)) {
            return false;
        }

        return MoveWall(context, item, current =>
        {
            if (position && altitude) {
                return wall;
            }

            var model = context.Room.GetGameMap().StaticModel;
            var targetAltitude = altitude ? WiredWallGeometry.SavedAltitude(model, wall) : WiredWallGeometry.Altitude(model, current);

            return WiredWallGeometry.TryProject(model, position ? wall : current, targetAltitude, out var projected) ? projected : null;
        });
    }

    public bool SetWallAltitude(WiredRuntimeContext context, Item item, int operation, double amount) => MoveWall(context, item, current =>
    {
        var model = context.Room.GetGameMap().StaticModel;
        var altitude = WiredWallGeometry.Altitude(model, current);
        var target = operation switch { 0 => altitude + amount, 1 => altitude - amount, _ => amount };

        return WiredWallGeometry.TryProject(model, current, Math.Clamp(target, 0, 80), out var projected) ? projected : null;
    });

    internal bool WriteWallBuiltin(WiredRuntimeContext context, Item item, string key, int value) => MoveWall(context, item, current =>
    {
        var model = context.Room.GetGameMap().StaticModel;
        var altitude = WiredWallGeometry.Altitude(model, current);
        var halfScale = model.Presentation.Scale / 2;
        var position = current;

        switch (key) {
            case "@position":
                position = current with { TileX = (value >> 8) & 255, TileY = value & 255 };
                break;
            case "@occupation":
                var side = value & 255;

                if (side is not (0 or 1)) {
                    return null;
                }

                position = current with { TileX = (value >> 16) & 255, TileY = (value >> 8) & 255, Left = side == 0 };
                break;
            case "@position.x":
                position = current with { TileX = value };
                break;
            case "@position.y":
                position = current with { TileY = value };
                break;
            case "@wallitem_offset" when value >= 0 && value <= halfScale:
                position = current with { LocalX = value };
                break;
            case "@altitude":
                altitude = value / 100.0;
                break;
            case "@rotation" when value is 0 or 1:
                position = current with { Left = value == 0 };
                break;
            default:
                return null;
        }

        if (position.Left != current.Left) {
            position = position with { LocalX = halfScale - current.LocalX };

            if (position.LocalX < 0 || position.LocalX > halfScale) {
                return null;
            }
        }

        return WiredWallGeometry.TryProject(model, position, altitude, out var projected, canonicalize: false) ? projected : null;
    });

    private static bool MoveWall(WiredRuntimeContext context, Item item, Func<WiredWallSnapshot, WiredWallSnapshot?> target)
    {
        lock (item.NavSync) {
            if (!item.IsWallItem || !context.FurniIdentity.TryGetValue(item.Id, out var captured) || !ReferenceEquals(item, captured)
                || !WiredWallSnapshot.TryParse(item.WallCoordinates, out var current)) {
                return false;
            }

            var projected = target(current!);

            if (projected == null) {
                return false;
            }

            var animate = !context.Policy.Addons.DisableAnimation && current!.Left == projected.Left;

            if (!context.Room.GetRoomItemHandler().MoveWallItem(item, projected.ToString(), announce: !animate)) {
                return false;
            }

            if (animate) {
                context.Room.SendPacket(new WiredWallMovementComposer((int)item.Id, current!, projected, context.Policy.Addons.AnimationTimeMs));
            }

            return true;
        }
    }

    /// <summary>A step to another tile is blocked by any other furniture unless physics moves it through; the unit's stack never is.</summary>
    public bool MoveFurniture(WiredRuntimeContext context, Item item, int x, int y, int rotation, double? height, bool step = false,
        IReadOnlySet<uint>? unit = null, PassengerSnapshot? passengers = null, IReadOnlySet<int>? landingUsers = null)
    {
        if (!item.IsFloorItem) {
            return false;
        }

        var policy = context.Policy.Addons;
        var room = context.Room;
        var source = Furniture(item);
        var targetHeight = height ?? room.GetGameMap().Model.SqFloorHeight[Math.Clamp(x, 0, room.GetGameMap().Model.MapSizeX - 1), Math.Clamp(y, 0, room.GetGameMap().Model.MapSizeY - 1)];
        var options = WiredMovementPolicy.Resolve(policy, source, x, y, targetHeight, rotation, explicitHeight: height.HasValue);

        // A projectile lands on its heading when the furni has it, otherwise on its furnidata defaultdir (see
        // FurnitureDirections.Heading); with neither it keeps its rotation instead of refusing the move.
        if (options.Rotation is { } turned && turned != rotation) {
            options = options with { Rotation = FurnitureDirections.Heading(item.Definition, turned) ?? rotation };
        }

        // A turn on the spot is a plain ObjectUpdate. Observed r92g r92-move-rotate mrCw-w1/mrCcw-w1 (AIR WIN63-202610131318-860371703,
        // frames 39269/39535): no WiredMovements for a rotation without a move.
        // Observed on AIR WIN63-202610201032-107964540: furni stacked on one tile that turn together also send ObjectUpdate, although
        // the ones above the bottom one land on a new height.
        if (x == item.GetX && y == item.GetY && rotation != item.Rotation) {
            options = options with { Animate = false };
        }

        var physics = policy.Physics;
        var collision = Collision(physics, step && (x != item.GetX || y != item.GetY), unit);

        if (landingUsers is { Count: > 0 }) {
            var through = collision?.ThroughUsers.Union(landingUsers).ToHashSet() ?? [.. landingUsers];
            collision = (collision ?? new(new HashSet<uint>(), new HashSet<int>(), new HashSet<uint>())) with { ThroughUsers = through, OntoDoor = true };
        }

        var carried = passengers?.For(context, item) ?? Carried(context, item);
        var dx = x - item.GetX;
        var dy = y - item.GetY;

        // A carry must validate every passenger destination before the furniture commits.
        if (carried.Any(user => !ValidAvatarDestination(room, user.X + dx, user.Y + dy))) {
            return false;
        }

        if (carried.Length > 0 && (!transparentWalkTransition || carried.Any(user =>
                room.GetGameMap().GetCoordinatedItems(user.Coordinate)
                    .Concat(room.GetGameMap().GetCoordinatedItems(new(user.X + dx, user.Y + dy)))
                    .Any(item => !PlainPublicationItem(item))))) {
            context.Publication?.Flush();
        }

        if (passengers != null && carried.Length > 0) {
            _prepareCarryPublication?.Invoke(context, carried);
        }

        // Placement immediately rebinds support heights; carry animation starts at the moved furniture's prior top.
        var sourceTop = source.Z + source.Height;
        var carrySources = carried.ToDictionary(user => user, user =>
        {
            var pose = passengers?.SourcePose(user) ?? (user.X, user.Y, user.Z);

            return (pose.X, pose.Y, Z: sourceTop + user.Z - WiredRoomOperations.StandZ(room, user));
        });
        var z = height ?? (physics?.KeepAltitude == true ? item.GetZ : (double?)null);

        if (!WiredRoomOperations.MoveItem(room, item, x, y, options.Rotation, z, animate: false,
                collision: collision, announce: !options.Animate)) {
            return false;
        }

        var projectile = policy.Projectile?.AppliesTo(item.Id) == true ? policy.Projectile : null;
        var overshootMs = ProjectileOvershootMs;

        if (projectile != null) {
            var duration = projectile.Duration((long)item.GetX - source.X,
                (long)item.GetY - source.Y, item.GetZ - source.Z, options.AnimationTimeMs);

            // A shot that flies on sends the first step and the rest of the flight in one packet.
            if (projectile.Distance is WiredProjectileDistance.Overshoot or WiredProjectileDistance.Fixed
                && projectile.Flight((long)item.GetX - source.X, (long)item.GetY - source.Y, item.GetZ - source.Z, options.AnimationDistanceOffset) is { } flight) {
                duration = flight.AnimationMs;
                overshootMs = flight.OvershootMs;
            }

            // Observed on AIR WIN63-202610201032-107964540: always shooting a negative number of tiles moves the furni at once.
            if (projectile.Distance == WiredProjectileDistance.Fixed && projectile.DistanceTiles < 0) {
                duration = 0;
            }

            var publishImmediate = options.Animate && duration == 0;
            options = options with
            {
                AnimationTimeMs = duration,
                Animate = options.Animate && duration > 0
            };

            if (publishImmediate) {
                context.Publication?.Flush();
                var furnitureSnapshot = RoomItemSnapshot.Capture(item);
                room.SendPacket(new ObjectUpdateComposer(furnitureSnapshot));
                room.SendPacket(new FurnitureMetadataComposer([furnitureSnapshot]));
            }

            WiredProjectileFlights.For(room).Begin(item, source.X, source.Y, source.Z,
                options.Animate ? options.AnimationTimeMs : 0, context.NowMilliseconds, projectile.VariableMask,
                options.CurveType == 7 ? options.CurveStrength : null);
        }

        if (options.Animate) {
            var style = new WiredMoveStyleComposer((int)item.Id, options.CurveType,
                options.CurveType == 7 ? options.CurveStrength : options.CurveIntensity, options.AnimationDistanceOffset);
            var movement = new WiredMovementComposer(1, (int)item.Id, source.X, source.Y, source.Z,
                item.GetX, item.GetY, item.GetZ, item.Rotation, item.Rotation, options.AnimationTimeMs)
            {
                CurveStrength = options.CurveType == 7 && options.CurveStrength != 0 ? options.CurveStrength : null,
                // Observed on AIR WIN63-202610201032-107964540: the overshoot modes send the time of the rest of the flight. A shot with no
                // time per tile keeps the value 800.
                OvershootTimeMs = projectile?.Distance is WiredProjectileDistance.Overshoot or WiredProjectileDistance.Fixed ? overshootMs : null
            };

            // The shooter faces the exact heading, diagonals included, on every fire, also when the furni lands on the rotation
            // it had. Observed on AIR WIN63-202610151745-318700621 with wf_antenna2 and wf_floor_switch1. A furni with a single
            // direction cannot turn and leaves the shooter as is: a wf_colortile flying alone sent no avatar direction.
            var aimed = projectile == null || item.Definition.Directions.Count == 1 ? null
                : AimShooter(context, projectile, (long)item.GetX - source.X, (long)item.GetY - source.Y);

            if (context.Publication is { } publication && publication.Append(item, movement, style, context.Firing, policy.ExecuteInOrder)) {
                if (aimed is { } late) {
                    publication.AppendDirection(late.Shooter, late.Lifetime, late.Turn);
                }
            }
            else {
                room.FlushWiredFlashes();
                room.SendPacket(style);

                // One packet: the furniture body, then the shooter's direction (re-checked after the style callback).
                var turn = aimed is { } pending && IsCurrentAim(room, pending) ? pending.Turn : null;
                room.SendPacket(new WiredMovementBatchComposer([movement], turn == null ? null : new[] { turn }));
            }
        }

        foreach (var user in carried) {
            MoveAvatar(context, user, user.X + dx, user.Y + dy, options.Animate, 1, true, animationTimeMs: options.AnimationTimeMs, passengers: passengers, carrySource: carrySources[user]);
        }

        return true;
    }

    private static bool IsCurrentAim(Room room, (RoomUser Shooter, long Lifetime, WiredUserDirectionComposer Turn) aim) =>
        aim.Shooter.Movement.State != Plus.HabboHotel.Rooms.PathFinding.NavState.Removing
        && aim.Shooter.Movement.LifetimeId == aim.Lifetime
        && ReferenceEquals(room.GetRoomUserManager().GetRoomUserByVirtualId(aim.Shooter.VirtualId), aim.Shooter);

    private static (RoomUser Shooter, long Lifetime, WiredUserDirectionComposer Turn)? AimShooter(WiredRuntimeContext context, WiredProjectilePolicy projectile, long dx, long dy)
    {
        if (projectile.ShooterSource is not { } source || projectile.DirectionSystem is not { } system
            || projectile.ShooterAimed || WiredMovementPolicy.Direction(system, dx, dy) is not { } direction) {
            return null;
        }

        // Aims the first present shooter once per firing, without the item offset; the once-per-firing and source policy are not proven.
        projectile.ShooterAimed = true;
        var room = context.Room;
        var shooter = context.Targets.ResolveUsers(context, [], source).FirstOrDefault(user =>
            ReferenceEquals(room.GetRoomUserManager().GetRoomUserByVirtualId(user.VirtualId), user));

        if (shooter == null) {
            return null;
        }

        shooter.RotBody = direction;
        shooter.RotHead = direction;

        return (shooter, shooter.Movement.LifetimeId, new WiredUserDirectionComposer(shooter.VirtualId, direction, direction));
    }

    /// <summary>
    /// Resolves one execution's furniture steps together. Movers stacked on each other step as one unit, decided by its
    /// bottom item. Before a unit enters a tile, a still-pending unit there that would block it takes its own step first,
    /// so a line follows its front or waits behind it in any order. Without step rules (wf_act_rel_mov) the units move in the
    /// movers' order and land on stackable furniture like any non-step move. Observed r92k r92h-rel-wall (AIR WIN63-202610131318-860371703):
    /// a column picked m3, m2, m1 moving into the wall left m1, stacked m2 on m1 and m3 on m2 before m2 left (m3 kept z 1.0).
    /// </summary>
    public bool MoveTogether(WiredRuntimeContext context, IReadOnlyList<Item> movers, Func<Item, Func<int, int, int, bool>, bool> step,
        PassengerSnapshot? passengers = null, bool stepRules = true, bool stackableBlocks = true)
    {
        var physics = context.Policy.Addons.Physics;
        var units = Units(movers);
        var unitOf = units.SelectMany(unit => unit.Select(item => (Item: item, Unit: unit))).ToDictionary(pair => pair.Item, pair => pair.Unit);
        var pending = units.ToHashSet();
        var changed = false;

        void Step(Item[] unit)
        {
            if (pending.Remove(unit) && step(unit[0], (x, y, rotation) => Move(unit, x, y, rotation))) {
                changed = true;
            }
        }

        bool Move(Item[] unit, int x, int y, int rotation)
        {
            var collision = Collision(physics, stepRules && (x != unit[0].GetX || y != unit[0].GetY));

            foreach (var ahead in Destinations(unit, x, y, rotation)
                         .SelectMany(move => WiredRoomOperations.Footprint(move.Item, move.X, move.Y, move.Rotation))
                         .SelectMany(point => context.Room.GetGameMap().GetCoordinatedItems(point)).Distinct().ToArray()) {
                if (unitOf.TryGetValue(ahead, out var other) && pending.Contains(other)
                    && stepRules && (collision?.BlocksFurni(ahead) ?? !ahead.Definition.Stackable)) {
                    Step(other);
                }
            }

            return MoveUnit(context, unit, x, y, rotation, passengers, stepRules && stackableBlocks);
        }

        foreach (var unit in units) {
            Step(unit);
        }

        return changed;
    }

    /// <summary>
    /// Movers sharing a footprint tile, directly or through each other, in first-mover order. Each unit lists its
    /// bottom item (lowest, then lowest id) first.
    /// </summary>
    public static IReadOnlyList<Item[]> Units(IEnumerable<Item> movers)
    {
        var units = new List<List<Item>>();

        foreach (var item in movers.Distinct()) {
            var tiles = WiredRoomOperations.Footprint(item, item.GetX, item.GetY, item.Rotation).ToHashSet();
            var joined = units.Where(unit => unit.Any(other =>
                WiredRoomOperations.Footprint(other, other.GetX, other.GetY, other.Rotation).Any(tiles.Contains))).ToArray();
            var index = joined.Length == 0 ? units.Count : units.IndexOf(joined[0]);
            units.RemoveAll(joined.Contains);
            units.Insert(index, [.. joined.SelectMany(unit => unit), item]);
        }

        return units.Select(unit => unit.OrderBy(item => item.GetZ).ThenBy(item => item.Id).ToArray()).ToArray();
    }

    /// <summary>
    /// The whole unit moves or none of it does. The bottom lands as a single item would; the others keep their height
    /// above it, and every member turns by the bottom's turn.
    /// </summary>
    private bool MoveUnit(WiredRuntimeContext context, Item[] unit, int x, int y, int rotation, PassengerSnapshot? passengers, bool stepRules = true)
    {
        var bottom = unit[0];

        if (unit.Length == 1) {
            return MoveFurniture(context, bottom, x, y, rotation, null, step: stepRules, passengers: passengers);
        }

        var moves = Destinations(unit, x, y, rotation);
        var ids = unit.Select(item => item.Id).ToHashSet();
        var keepAltitude = context.Policy.Addons.Physics?.KeepAltitude == true;
        var collision = Collision(context.Policy.Addons.Physics, stepRules && (x != bottom.GetX || y != bottom.GetY), ids);

        if (moves.Any(move => !WiredRoomOperations.CanMoveItem(context.Room, move.Item, move.X, move.Y, move.Rotation,
                    keepAltitude ? move.Item.GetZ : null, collision: collision)
                || (passengers?.For(context, move.Item) ?? Carried(context, move.Item)).Any(user => !ValidAvatarDestination(context.Room,
                    user.X + move.X - move.Item.GetX, user.Y + move.Y - move.Item.GetY)))) {
            return false;
        }

        var offsets = unit.ToDictionary(item => item, item => item.GetZ - bottom.GetZ);

        if (!MoveFurniture(context, bottom, x, y, rotation, null, step: stepRules, ids, passengers)) {
            return false;
        }

        foreach (var move in moves.Skip(1)) {
            MoveFurniture(context, move.Item, move.X, move.Y, move.Rotation, bottom.GetZ + offsets[move.Item], step: stepRules, ids, passengers);
        }

        return true;
    }

    private static (Item Item, int X, int Y, int Rotation)[] Destinations(Item[] unit, int x, int y, int rotation) =>
        unit.Select(item => (item, item.GetX + x - unit[0].GetX, item.GetY + y - unit[0].GetY,
            (item.Rotation + rotation - unit[0].Rotation + 8) % 8)).ToArray();

    /// <summary>Passenger membership belongs to one action, before any mover changes the room.</summary>
    public static PassengerSnapshot CapturePassengers(WiredRuntimeContext context, IEnumerable<Item> movers) =>
        new(context, movers);

    public sealed class PassengerSnapshot
    {
        private readonly Dictionary<Item, RoomUser[]> _passengers;
        private readonly Dictionary<RoomUser, (int X, int Y, double Z, long Lifetime, long Revision)> _origins;
        private readonly ConcurrentDictionary<RoomUser, byte> _moved = new(ReferenceEqualityComparer.Instance);

        internal PassengerSnapshot(WiredRuntimeContext context, IEnumerable<Item> movers)
        {
            _passengers = context.Policy.Addons.Carry == null ? []
                : movers.Where(item => item.IsFloorItem).Distinct().ToDictionary(item => item, item => Carried(context, item));
            _origins = _passengers.Values.SelectMany(users => users).Distinct().ToDictionary(user => user,
                user => (user.X, user.Y, user.Z, user.Movement.LifetimeId, user.Movement.LocationRevision));
        }

        internal RoomUser[] For(WiredRuntimeContext context, Item item) =>
            _passengers.TryGetValue(item, out var users)
                ? users.Where(user => CanMove(context, user) && WiredRoomOperations.IsOnItem(user, item)
                    && (context.Policy.Addons.Carry?.SameTile == true
                        || Math.Abs(WiredRoomOperations.StandZ(context.Room, user) - item.TotalHeight) < 0.001)).ToArray() : [];

        // Queued owner callbacks retain the original support evidence; the furniture has already moved.
        internal bool CanMove(WiredRuntimeContext context, RoomUser user) =>
            _origins.TryGetValue(user, out var origin) && !_moved.ContainsKey(user)
            && ReferenceEquals(context.Room.GetRoomUserManager().GetRoomUserByVirtualId(user.VirtualId), user)
            && user.Movement.LifetimeId == origin.Lifetime && user.Movement.LocationRevision == origin.Revision
            && user.X == origin.X && user.Y == origin.Y;

        internal (int X, int Y, double Z) SourcePose(RoomUser user)
        {
            var origin = _origins[user];

            return (origin.X, origin.Y, origin.Z);
        }

        internal void Moved(RoomUser user) => _moved.TryAdd(user, 0);
    }

    // Observed on AIR WIN63-202610131318-860371703 (Fast Queue, 200 ms): a user walking against the queue walks tile by tile with no
    // carry entries; carrying resumes once the walk has ended. A walking user, or one whose walk request is still queued, is not a
    // passenger: carrying them would discard that request.
    private static RoomUser[] Carried(WiredRuntimeContext context, Item item) => context.Room.GetRoomUserManager().GetRoomUsers()
        .Where(user => context.Policy.Addons.Carry?.UserIds.Contains(user.VirtualId) == true
            && !user.IsWalking && !HasQueuedWalk(user)
            && WiredRoomOperations.IsOnItem(user, item)
            && (context.Policy.Addons.Carry.SameTile || Math.Abs(WiredRoomOperations.StandZ(context.Room, user) - item.TotalHeight) < 0.001))
        .Select(user => WiredRoomOperations.Rider(context.Room, user)).Distinct().ToArray();

    private static bool HasQueuedWalk(RoomUser user) => user.Movement.Commands.Read() is { Origin: not MoveOrigin.Wired } command
        && command.Sequence > user.Movement.ConsumedSequence;

    public bool MoveAvatar(WiredRuntimeContext context, RoomUser user, int x, int y, bool animate,
        int walkMode = 2, bool throughUsers = false, bool ignoreOccupants = false, int? animationTimeMs = null,
        PassengerSnapshot? passengers = null, (int X, int Y, double Z)? carrySource = null, bool replaceStep = false)
    {
        var room = context.Room;
        user = WiredRoomOperations.Rider(room, user);

        if (!carrySource.HasValue) {
            // Ordinary relocation can invoke landing/walk work before its immediate animation.
            context.Publication?.Flush();
        }

        bool Move(RoomUser actor, long? sequence)
        {
            if (passengers != null && !passengers.CanMove(context, actor)) {
                return false;
            }

            var moved = MoveAvatarOwned(context, actor, x, y, animate, walkMode, throughUsers, ignoreOccupants, animationTimeMs, sequence, carrySource,
                replaceStep);

            if (moved) {
                passengers?.Moved(actor);
            }

            return moved;
        }

        if (!RoomOwnerScope.IsOwner(room)) {
            // Placement, walk hooks and packets must observe the same owner-side move.
            room.GetGameMap().Navigation!.RunOwner(user, (actor, sequence) => Move(actor, sequence));

            return true;
        }

        return Move(user, null);
    }

    private bool MoveAvatarOwned(WiredRuntimeContext context, RoomUser user, int x, int y, bool animate,
        int walkMode, bool throughUsers, bool ignoreOccupants, int? animationTimeMs, long? discardThrough, (int X, int Y, double Z)? carrySource,
        bool replaceStep = false)
    {
        var room = context.Room;
        var curve = context.Policy.Addons.Curve;

        if (context.Policy.Addons.Physics is { } physics) {
            if (!ValidAvatarDestination(room, x, y)
                || room.GetGameMap().GetCoordinatedItems(new(x, y)).Any(item => physics.BlockingFurni.Contains(item.Id))
                || !WiredRoomOperations.AvatarCanStandWithPhysics(room.GetGameMap(), x, y, physics.ThroughFurni)) {
                return false;
            }

            // ignoreOccupants skips people only. Blocking furniture and a closed tile still refuse.
            if (!ignoreOccupants) {
                var occupants = room.GetGameMap().GetRoomUsers(new(x, y)).Where(other => !ReferenceEquals(other, user)).ToArray();

                if (!throughUsers && occupants.Any(other => !physics.ThroughUsers.Contains(other.VirtualId))) {
                    return false;
                }

                throughUsers |= occupants.Length > 0 && occupants.All(other => physics.ThroughUsers.Contains(other.VirtualId));
            }

            // The scoped physics checks above have independently admitted furniture and occupants.
            // CanWalk combines both, so it must not reject furniture explicitly passed by this policy.
            throughUsers = true;
            ignoreOccupants = false;
        }

        var oldX = user.X;
        var oldY = user.Y;
        var oldZ = user.Z;
        var horse = WiredRoomOperations.Horse(room, user);
        var horseSeat = horse == null ? 0 : oldZ - horse.Z;
        var wasWalking = user.IsWalking;
        var resumeThrough = discardThrough ?? (user.Movement.Commands.Read()?.Sequence ?? 0);
        var goalX = user.GoalX;
        var goalY = user.GoalY;
        var oldItems = room.GetGameMap().GetCoordinatedItems(user.Coordinate).DistinctBy(item => item.Id).ToArray();

        // A push that replaces a step in progress may land back on the step's start tile.
        var inPlace = replaceStep && user.SetStep && user.X == x && user.Y == y;

        if (!WiredRoomOperations.RelocateAvatar(room, user, x, y, false, throughUsers, ignoreOccupants, discardThrough, inPlace)) {
            return false;
        }

        var newItems = room.GetGameMap().GetCoordinatedItems(user.Coordinate).DistinctBy(item => item.Id).ToArray();

        if (!transparentWalkTransition || oldItems.Concat(newItems).Any(item => !PlainPublicationItem(item))) {
            context.Publication?.Flush();
        }

        walkTransition(user, oldItems, newItems);

        if (animate && !context.Policy.Addons.DisableAnimation) {
            var style = new WiredMoveStyleComposer(user.VirtualId, curve?.Type ?? 0,
                curve?.Type == 7 ? curve.Strength : curve?.Intensity ?? 100, 0, true);
            var source = carrySource ?? (X: oldX, Y: oldY, Z: oldZ);
            var movement = new WiredMovementComposer(0, user.VirtualId, source.X, source.Y, source.Z, user.X, user.Y, user.Z,
                user.RotBody, user.RotHead, animationTimeMs ?? context.Policy.Addons.AnimationTimeMs)
            {
                JumpPower = curve?.Type == 7 ? curve.Strength : null
            };

            if (!carrySource.HasValue || _appendCarryPublication?.Invoke(context, user, movement, style) != true) {
                room.FlushWiredFlashes();
                room.SendPacket(style);
                room.SendPacket(movement);
            }

            // The horse moved with its rider; it animates along the same path below the rider.
            if (horse != null && horse.X == user.X && horse.Y == user.Y) {
                room.SendPacket(new WiredMoveStyleComposer(horse.VirtualId, curve?.Type ?? 0,
                    curve?.Type == 7 ? curve.Strength : curve?.Intensity ?? 100, 0, true));
                room.SendPacket(new WiredMovementComposer(0, horse.VirtualId, source.X, source.Y, source.Z - horseSeat,
                    horse.X, horse.Y, horse.Z, horse.RotBody, horse.RotHead, animationTimeMs ?? context.Policy.Addons.AnimationTimeMs)
                {
                    JumpPower = curve?.Type == 7 ? curve.Strength : null
                });
            }
        }

        // An animated wired move shows "wf" on the avatar for one status update (r92m r92m-wf-status).
        if (animate && !user.IsBot) {
            user.MarkWiredMove();
        }

        // A newer walk request takes precedence over restoring the route captured by this move.
        if (wasWalking && (user.Movement.Commands.Read()?.Sequence ?? 0) <= resumeThrough && (walkMode == 1 || walkMode == 0
            && Math.Max(Math.Abs(goalX - x), Math.Abs(goalY - y)) < Math.Max(Math.Abs(goalX - oldX), Math.Abs(goalY - oldY)))) {
            user.MoveTo(goalX, goalY);
        }

        return true;
    }

    internal static WiredCollisionPolicy? Collision(WiredPhysicsPolicy? physics, bool step, IReadOnlySet<uint>? unit = null) => physics == null
        ? step || unit != null ? new(new HashSet<uint>(), new HashSet<int>(), new HashSet<uint>(), step, unit) : null
        : new(physics.ThroughFurni, physics.ThroughUsers, physics.BlockingFurni, step, unit);
    private static bool ValidAvatarDestination(Room room, int x, int y) => room.GetGameMap().ValidTile(x, y)
        && room.GetGameMap().Model.SqState[x, y] == SquareState.Open;
    public static WiredSelectorFurniture Furniture(Item item) => new(item.Id, (int)item.Definition.Id,
        item.Definition.DisplayName, item.LegacyDataString, item.GetX, item.GetY, item.GetZ, item.TotalHeight - item.GetZ,
        WiredRoomOperations.Footprint(item, item.GetX, item.GetY, item.Rotation).Select(point => (point.X, point.Y)).ToArray(),
        item.IsFloorItem, item.IsWired);
}
