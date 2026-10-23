using System.Drawing;
using Plus.HabboHotel.Items.Interactor;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>
/// Executes the existing Volt movement editors. Resolved movers and targets are separate;
/// the caller owns source selection, scheduling and packet policy.
/// </summary>
public sealed class WiredMovementActions(Random? random = null)
{
    // Injected randomness also makes target distribution and execution order reproducible in tests.
    private readonly Random _random = random ?? Random.Shared;

    public delegate bool MoveFurniture(Item item, int x, int y, int rotation, double? height);
    public delegate bool MoveAvatar(RoomUser avatar, Item target, bool slide, bool fastTeleport, int walkMode);
    /// <summary>Runs each mover's step; the step commits through the given (x, y, rotation) move.</summary>
    public delegate bool MoveTogether(IReadOnlyList<Item> movers, Func<Item, Func<int, int, int, bool>, bool> step);

    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "wf_act_rel_mov", "wf_act_set_altitude", "wf_act_move_rotate", "wf_act_move_furni_as_group",
        "wf_act_furni_to_furni", "wf_act_furni_to_user", "wf_act_move_furni_to", "wf_act_match_to_sshot",
        "wf_act_toggle_state", "wf_act_toggle_to_rnd", "wf_act_teleport_to", "wf_act_user_to_furni"
    };

    /// <summary>
    /// Furniture movers stepping to a derived tile, where any other furniture blocks. wf_act_rel_mov is not one: it moves onto its own stackable marker and stacks on it, also without a physics add-on.
    /// </summary>
    public static readonly IReadOnlySet<string> Steps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "wf_act_move_rotate", "wf_act_move_furni_as_group", "wf_act_move_furni_to"
    };

    // Nothing else on the item's tile stands at or above it.
    private static bool OnTopOfItsTile(Item item) => item.GetRoom()?.GetGameMap()?.GetCoordinatedItems(new(item.GetX, item.GetY))
        .All(other => other.Id == item.Id || other.GetZ < item.GetZ) ?? true;

    // The top of the item's stack without the item itself: where a restack onto the same tile lands.
    internal static double? RestackHeight(Item item) => item.GetRoom()?.GetGameMap()?.GetCoordinatedItems(new(item.GetX, item.GetY))
        .Where(other => other.Id != item.Id).Select(other => other.TotalHeight).DefaultIfEmpty(item.GetZ).Max();

    public bool Execute(string name, WiredConfiguration configuration, IReadOnlyList<Item> movers,
        IReadOnlyList<Item> targets, IReadOnlyList<RoomUser> users,
        MoveFurniture move, MoveAvatar relocate, Action<Item, string> setState,
        Func<Item, Func<string, string?>, bool>? toggleState = null, MoveTogether? together = null,
        Func<int, int, bool>? inBounds = null, Func<Item, WiredFurniSnapshot, bool, bool, bool>? restoreWall = null,
        Func<Item, int, double, bool>? setWallAltitude = null)
    {
        if (!WiredMovementConfiguration.TryValidate(name, configuration, out configuration, out _)) {
            return false;
        }

        var p = configuration.IntParams;
        int Param(int index, int fallback = 0) => index < p.Length ? p[index] : fallback;
        var affected = false;
        together ??= (items, step) => items.Aggregate(false,
            (any, item) => step(item, (x, y, rotation) => move(item, x, y, rotation, null)) | any);

        switch (name.ToLowerInvariant()) {
            case "wf_act_rel_mov":
                var dx = (Param(0, 1) == 0 ? -1 : 1) * Param(1);
                var dy = (Param(2, 1) == 0 ? -1 : 1) * Param(3);

                if (dx == 0 && dy == 0) {
                    return false;
                }

                affected = together(movers, (item, step) => step(item.GetX + dx, item.GetY + dy, item.Rotation));

                break;
            case "wf_act_set_altitude":
                if (!WiredRoomOperations.TryAltitude(configuration.Text, out var altitude)) {
                    return false;
                }

                foreach (var item in movers) {
                    if (item.IsWallItem) {
                        affected |= setWallAltitude?.Invoke(item, Param(0, 2), altitude) == true;

                        continue;
                    }

                    var z = Param(0, 2) switch { 0 => item.GetZ + altitude, 1 => item.GetZ - altitude, _ => altitude };
                    affected |= move(item, item.GetX, item.GetY, item.Rotation, Math.Clamp(z, 0, Math.Max(0, 80 - item.CurrentHeight)));
                }

                break;
            case "wf_act_move_rotate":
                // With no movement and no rotation each picked furni is placed again on top of its stack, in a random order.
                // One already on top at its turn stays.
                if (Param(0) is < 0 or > 10 && Param(1) is not (2 or 4 or 6)) {
                    foreach (var item in Shuffled(movers)) {
                        if (!OnTopOfItsTile(item)) {
                            affected |= move(item, item.GetX, item.GetY, item.Rotation, RestackHeight(item));
                        }
                    }

                    break;
                }

                affected = together(movers, (item, step) =>
                {
                    var direction = MovementDirection(Param(0));
                    var offset = Rotation.Offset(direction);
                    // Turns step to the furni's next direction in 45 degree order; Random takes either step.
                    var rotation = Param(1) switch
                    {
                        2 => FurnitureDirections.Turn(item.Definition, item.Rotation, clockwise: true),
                        4 => FurnitureDirections.Turn(item.Definition, item.Rotation, clockwise: false),
                        6 => FurnitureDirections.Turn(item.Definition, item.Rotation, clockwise: Random.Shared.Next(2) == 0),
                        _ => item.Rotation
                    };

                    return step(item.GetX + offset.X, item.GetY + offset.Y, rotation);
                });

                break;
            case "wf_act_move_furni_as_group":
                if (p.Length == 6) {
                    if (movers.Count == 0 || (Param(0) == 1 ? users.Count == 0 : targets.Count == 0)) {
                        return false;
                    }

                    var anchorX = Param(0) == 1 ? users[0].X : targets[0].GetX;
                    var anchorY = Param(0) == 1 ? users[0].Y : targets[0].GetY;
                    var groupDx = anchorX + Param(1) - movers[0].GetX;
                    var groupDy = anchorY + Param(2) - movers[0].GetY;

                    if (groupDx == 0 && groupDy == 0
                        || inBounds != null && movers.Any(item => !inBounds(item.GetX + groupDx, item.GetY + groupDy))) {
                        return false;
                    }

                    // A member whose landing tile holds a user stays put; the rest of the group still moves.
                    foreach (var item in movers.OrderByDescending(item => item.GetX * Math.Sign(groupDx) + item.GetY * Math.Sign(groupDy))) {
                        affected |= move(item, item.GetX + groupDx, item.GetY + groupDy, item.Rotation, null);
                    }

                    break;
                }

                var groupOffset = Rotation.Offset(Param(0));

                // Volt/Polaris direction editor: leading edge first, blocked members skipped.
                affected = together(movers.OrderByDescending(item => item.GetX * groupOffset.X + item.GetY * groupOffset.Y).ToArray(),
                    (item, step) => step(item.GetX + groupOffset.X, item.GetY + groupOffset.Y, item.Rotation));

                break;
            case "wf_act_furni_to_furni":
                if (targets.Count == 0) {
                    return false;
                }

                var destinations = Shuffled(targets);
                var uses = new int[destinations.Length];
                var assignments = movers.Select(item => ReserveTarget(destinations, uses, item.Id)).ToArray();

                // Two movers spread one per target, two movers on one target stack, a stacked target is landed on top, and a target tile held by a user
                // refuses the move.
                // Assign before moving: every target gets a turn, including assignments whose move is blocked.
                // Execute separately in random order because earlier moves change the stack for later ones.
                foreach (var index in Shuffled(Enumerable.Range(0, movers.Count).ToArray())) {
                    var item = movers[index];
                    var assigned = assignments[index];

                    // A mover assigned the target it already stands on is moved again, back onto the top of that stack; only one already on top stays put.
                    if (assigned == null) {
                        continue;
                    }

                    if (item.GetX != assigned.GetX || item.GetY != assigned.GetY) {
                        affected |= move(item, assigned.GetX, assigned.GetY, item.Rotation, null);
                    }
                    else if (!OnTopOfItsTile(item)) {
                        // The room drops a move that keeps tile, rotation and height, so the restack names its new height.
                        affected |= move(item, assigned.GetX, assigned.GetY, item.Rotation, RestackHeight(item));
                    }
                }

                break;
            case "wf_act_move_furni_to":
                if (targets.Count == 0) {
                    return false;
                }

                var target = targets[0];
                var spacing = name.Equals("wf_act_move_furni_to", StringComparison.OrdinalIgnoreCase)
                    ? Rotation.Offset(Param(0)) : Point.Empty;

                var targetX = target.GetX + spacing.X * Param(1, 1);
                var targetY = target.GetY + spacing.Y * Param(1, 1);

                if (Steps.Contains(name) && p.Length == 3) {
                    affected = together(movers, (item, step) => step(targetX, targetY, item.Rotation));

                    break;
                }

                foreach (var item in movers) {
                    affected |= move(item, targetX, targetY, item.Rotation, null);
                }

                break;
            case "wf_act_furni_to_user":
                if (users.Count == 0) {
                    return false;
                }

                // Furni are dealt out so each selected user gets the same number, give or take one, in a new random order each firing.
                var dealtUsers = Dealt(users, movers.Count);

                // A walking user's furni goes to the step target, the mv tile of that tick.
                // The moves run in a random order, and a furni dealt the user it already stands under is restacked like Furni To Furni.
                foreach (var index in Shuffled(Enumerable.Range(0, movers.Count).ToArray())) {
                    var item = movers[index];
                    var user = dealtUsers[index];
                    var (x, y) = user.IsWalking && user.SetStep ? (user.SetX, user.SetY) : (user.X, user.Y);

                    if (item.GetX != x || item.GetY != y) {
                        affected |= move(item, x, y, item.Rotation, null);
                    }
                    else if (!OnTopOfItsTile(item)) {
                        affected |= move(item, x, y, item.Rotation, RestackHeight(item));
                    }
                }

                break;
            case "wf_act_match_to_sshot":
                // A furni outside a one-furni snapshot takes that furni's state; nothing else of it was recorded.
                var only = configuration.Snapshots.Length == 1 && configuration.Snapshots[0].Wall == null ? configuration.Snapshots[0] : null;
                // With the picked furni (source 100) each item returns to its own entry. With a selector pool (source 200) the saved floor spots
                // are dealt out in a random order, also to a selected furni without an entry, and reused when there are more furni than spots.
                var dealt = configuration.Origin?.Native == null || configuration.FurniSources.GetValueOrDefault("movers") != WiredSources.Selector ? null
                    : Shuffled(configuration.Snapshots.Where(entry => entry.Wall == null).ToArray());
                // Which furni shares a reused spot is random as well, and every spot is used each time.
                var ranks = Shuffled(Enumerable.Range(0, movers.Count).ToArray());
                var dealIndex = 0;

                // Furni put back on one tile by position only stack in a new order; with the altitude too the saved stack comes back.
                // Other restores keep the pick order.
                foreach (var item in Param(2) == 1 && Param(3) != 1 ? Shuffled(movers) : movers) {
                    var snapshot = dealt is { Length: > 0 } && item.IsFloorItem
                        ? dealt[ranks[dealIndex++] % dealt.Length]
                        : configuration.Snapshots.FirstOrDefault(entry => entry.ItemId == item.Id);

                    if (snapshot == null) {
                        if (only != null && Param(0) == 1 && item.IsFloorItem) {
                            var onlyState = only.StateFor(item.Definition);

                            if (toggleState != null) {
                                affected |= toggleState(item, current => string.Equals(current, onlyState, StringComparison.Ordinal) ? null : onlyState);
                            }
                            else if (!string.Equals(item.LegacyDataString, onlyState, StringComparison.Ordinal)) {
                                setState(item, onlyState);
                                affected = true;
                            }
                        }

                        continue;
                    }

                    if (item.IsWallItem) {
                        if (Param(0) == 0 && Param(1) == 0 && (Param(2) == 1 || Param(3) == 1) && restoreWall != null) {
                            affected |= restoreWall(item, snapshot, Param(2) == 1, Param(3) == 1);
                        }

                        continue;
                    }

                    if (!item.IsFloorItem || snapshot.Wall != null) {
                        continue;
                    }

                    // A dealt spot of another furni carries no state for this one.
                    var ownState = snapshot.ItemId == item.Id;
                    var savedState = snapshot.StateFor(item.Definition);

                    if (Param(0) == 1 && ownState && toggleState != null) {
                        affected |= toggleState(item, current => string.Equals(current, savedState, StringComparison.Ordinal) ? null : savedState);
                    }
                    else if (Param(0) == 1 && ownState && !string.Equals(item.LegacyDataString, savedState, StringComparison.Ordinal)) {
                        setState(item, savedState);
                        affected = true;
                    }

                    affected |= move(item, Param(2) == 1 ? snapshot.X : item.GetX,
                        Param(2) == 1 ? snapshot.Y : item.GetY,
                        Param(1) == 1 ? FurnitureDirections.Write(item.Definition, snapshot.Rotation) ?? item.Rotation : item.Rotation,
                        Param(3) == 1 ? snapshot.Z : null);
                }

                break;
            case "wf_act_toggle_state":
            case "wf_act_toggle_to_rnd":
                foreach (var item in movers) {
                    var states = item.Definition.Modes;

                    if (states <= 1) {
                        continue;
                    }

                    var random = name.Equals("wf_act_toggle_to_rnd", StringComparison.OrdinalIgnoreCase);
                    Func<string, string?> nextState = !random && Param(0) != 1 && item.Definition.InteractionType == InteractionType.RollerSpeedController
                        ? current => InteractorRollerSpeed.WiredToggle(item, current)
                        : current => NextToggleState(current, states, random, Param(0) == 1);

                    if (toggleState != null) {
                        affected |= toggleState(item, nextState);
                        continue;
                    }

                    if (nextState(item.LegacyDataString) is not { } next) {
                        continue;
                    }

                    setState(item, next);
                    affected = true;
                }

                break;
            case "wf_act_user_to_furni":
                if (targets.Count == 0) {
                    return false;
                }

                var userDestinations = Shuffled(targets);
                var userUses = new int[userDestinations.Length];

                foreach (var user in Shuffled(users)) {
                    var destination = ReserveTarget(userDestinations, userUses)!;
                    affected |= relocate(user, destination, true, false, Param(2, 1));
                }

                break;
            case "wf_act_teleport_to":
                if (targets.Count == 0) {
                    return false;
                }

                // Two selected users always go to two different furni, in a new random order each firing.
                var teleportTargets = Dealt(targets, users.Count);

                for (var index = 0; index < users.Count; index++) {
                    affected |= relocate(users[index], teleportTargets[index], false, Param(0) == 1, 2);
                }

                break;
            default:
                return false;
        }

        return affected;
    }

    // Deals count values in rounds: each round uses every value once, in a new random order.
    private T[] Dealt<T>(IReadOnlyList<T> values, int count)
    {
        var dealt = new List<T>(count);

        while (dealt.Count < count && values.Count > 0) {
            dealt.AddRange(Shuffled(values));
        }

        return dealt.Take(count).ToArray();
    }

    private T[] Shuffled<T>(IReadOnlyList<T> values)
    {
        var result = values.ToArray();
        _random.Shuffle(result.AsSpan());

        return result;
    }

    private static Item? ReserveTarget(Item[] targets, int[] uses, uint? excludedItemId = null)
    {
        var index = -1;

        for (var i = 0; i < targets.Length; i++) {
            if (targets[i].Id != excludedItemId && (index < 0 || uses[i] < uses[index])) {
                index = i;
            }
        }

        if (index < 0) {
            return null;
        }

        uses[index]++;

        return targets[index];
    }

    // v2 only. Null when the toggle would not change the state.
    private static string? NextToggleState(string current, int states, bool random, bool reverse)
    {
        _ = int.TryParse(current, out var oldState);
        var next = random ? Random.Shared.Next(states) : ((oldState + (reverse ? -1 : 1)) % states + states) % states;

        return next == oldState ? null : next.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private int MovementDirection(int movement) => movement switch
    {
        >= 0 and <= 7 => movement,
        // Stored movement choices include fixed, random and no movement.
        8 => _random.Next(8),
        9 => _random.Next(2) == 0 ? 2 : 6,
        10 => _random.Next(2) == 0 ? 0 : 4,
        _ => -1
    };
}
