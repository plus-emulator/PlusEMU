using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>Tracks only Wired-owned freeze state; room lifecycle must forget departing avatars.</summary>
public sealed class WiredAvatarState
{
    // Previous: the avatar effect worn before the first freeze, put back by Thaw (hhus restores e.g. the Wired team effect).
    private sealed record Freeze(bool Frozen, bool CanWalk, int Effect, bool CancelOnTeleport, int Previous);
    private readonly Dictionary<RoomUser, Freeze> _frozen = [];
    private Room? _executorRoom;
    private static readonly ConditionalWeakTable<Room, WiredAvatarState> Rooms = new();
    public static WiredAvatarState For(Room room) => room.UsesV2Movement
        ? Rooms.GetValue(room, value => new() { _executorRoom = value })
        : Rooms.GetValue(room, _ => new());
    public bool FreezeUser(RoomUser user, int effect, bool cancelOnTeleport)
    {
        if (_executorRoom?.GetGameMap()?.Navigation is { UsesExecutor: true } navigation) {
            // The tile the freeze stops the user on: the step announced when it fired, otherwise where the user stands.
            var stopAt = WiredRoomOperations.StandingTile(user);

            navigation.RunOwner(user, (actor, sequence) =>
            {
                // Observed on AIR WIN63-202610151745-318700621: a user frozen while walking finishes the step announced when the freeze
                // fired and stops there; a step announced after it is not taken.
                if (actor.SetStep && new System.Drawing.Point(actor.SetX, actor.SetY) == stopAt && navigation.StopAfterStep(actor)) {
                    FreezeActor(actor, effect, cancelOnTeleport, stopWalk: false);

                    return;
                }

                // The cancel always flags a status update; a standing user has nothing to stop, so keep its flag as it was.
                var moving = IsMoving(actor);
                var updateNeeded = actor.UpdateNeeded;
                navigation.CancelThrough(actor, sequence);

                if (!moving) {
                    actor.UpdateNeeded = updateNeeded;
                }

                FreezeActor(actor, effect, cancelOnTeleport);
            });

            return true;
        }

        var original = _frozen.GetValueOrDefault(user);
        _frozen[user] = new(original?.Frozen ?? user.Frozen, original?.CanWalk ?? user.CanWalk, effect, cancelOnTeleport,
            original?.Previous ?? user.GetClient()?.GetHabbo()?.Effects?.CurrentEffect ?? 0);
        var moving = IsMoving(user);
        user.IsWalking = false;
        user.PathRecalcNeeded = false;
        user.RemoveStatus("mv");
        user.GoalX = user.X;
        user.GoalY = user.Y;
        user.Frozen = true;
        user.CanWalk = false;

        // Observed on hhus (AIR WIN63-202610131318-860371703): freezing a standing user sends only the effect, no UserUpdate.
        // A walking user still needs one so its stop (mv removed) reaches the clients.
        if (moving) {
            user.UpdateNeeded = true;
        }

        // Plus bots have no stored effect/version to restore safely. Their actual movement freeze still applies.
        if (!user.IsBot && effect > 0) {
            user.ApplyEffect(effect);
        }

        return true;
    }
    private void FreezeActor(RoomUser user, int effect, bool cancelOnTeleport, bool stopWalk = true)
    {
        var original = _frozen.GetValueOrDefault(user);
        _frozen[user] = new(original?.Frozen ?? user.Frozen, original?.CanWalk ?? user.CanWalk, effect, cancelOnTeleport,
            original?.Previous ?? user.GetClient()?.GetHabbo()?.Effects?.CurrentEffect ?? 0);

        if (stopWalk) {
            user.IsWalking = false;
            user.PathRecalcNeeded = false;
            user.RemoveStatus("mv");
            user.GoalX = user.X;
            user.GoalY = user.Y;
        }

        user.Frozen = true;
        user.CanWalk = false;

        // Plus bots have no stored effect/version to restore safely. Their actual movement freeze still applies.
        if (!user.IsBot && effect > 0) {
            user.ApplyEffect(effect);
        }
    }
    private static bool IsMoving(RoomUser user) => user.IsWalking || user.HasStatus("mv");

    public bool Thaw(RoomUser user, bool teleport = false)
    {
        if (!_frozen.TryGetValue(user, out var state) || teleport && !state.CancelOnTeleport) {
            return false;
        }

        // Observed on AIR WIN63-202610151745-318700621 (wf_act_freeze.cov_effect_sources): Unfreeze lands on the room tick, like Freeze
        // (said at +0, effect 0 at +400 ms), and a teleport that cancels a freeze sends only the teleport effect, no effect 0 first.
        if (!teleport && _executorRoom?.GetGameMap()?.Navigation is { UsesExecutor: true } navigation) {
            navigation.RunOwner(user, (actor, _) =>
            {
                if (_frozen.TryGetValue(actor, out var current)) {
                    ThawActor(actor, current, restoreEffect: true);
                }
            });

            return true;
        }

        ThawActor(user, state, restoreEffect: !teleport);

        return true;
    }

    private void ThawActor(RoomUser user, Freeze state, bool restoreEffect)
    {
        _frozen.Remove(user);
        user.Frozen = state.Frozen;
        user.CanWalk = state.CanWalk;

        // Observed on hhus (AIR WIN63-202610131318-860371703): unfreeze sends only the restored effect and no UserUpdate;
        // a user in a Wired team gets the team effect (223) back on unfreeze, not 0.
        if (restoreEffect && !user.IsBot && state.Effect > 0 && user.GetClient()?.GetHabbo()?.Effects?.CurrentEffect == state.Effect) {
            user.ApplyEffect(state.Previous == state.Effect ? 0 : state.Previous);
        }
    }
    public void Forget(RoomUser user) => _frozen.Remove(user);
    public void Clear() => _frozen.Clear();
}
