using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>Tracks only Wired-owned freeze state; room lifecycle must forget departing avatars.</summary>
public sealed class WiredAvatarState
{
    // Previous: the avatar effect worn before the first freeze, or the team effect set by Join/Leave Team while frozen; Thaw puts it back (Habbo restores e.g. the Wired team effect).
    private sealed record Freeze(bool Frozen, bool CanWalk, int Effect, bool CancelOnTeleport, int Previous);
    private readonly Dictionary<RoomUser, Freeze> _frozen = [];
    // The freeze and unfreeze effects arrive with the next room tick (in the same batch as
    // a repeater's flash on that tick), not with the Wired flash itself.
    private readonly List<Action> _tickEffects = [];
    private Room? _executorRoom;
    private static readonly ConditionalWeakTable<Room, WiredAvatarState> Rooms = new();
    public static WiredAvatarState For(Room room) => Rooms.GetValue(room, value => new() { _executorRoom = value })
;
    public bool FreezeUser(RoomUser user, int effect, bool cancelOnTeleport)
    {
        if (_executorRoom?.GetGameMap()?.Navigation is { } navigation) {
            // The tile the freeze stops the user on: the step announced when it fired, otherwise where the user stands.
            var stopAt = WiredRoomOperations.StandingTile(user);

            navigation.RunOwner(user, (actor, sequence) =>
            {
                // A user frozen while walking finishes the step announced when the freeze
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

        // Freezing a standing user sends only the effect, no UserUpdate.
        // A walking user still needs one so its stop (mv removed) reaches the clients.
        if (moving) {
            user.UpdateNeeded = true;
        }

        // Plus bots have no stored effect/version to restore safely. Their actual movement freeze still applies.
        if (!user.IsBot && effect > 0) {
            OnNextTick(() => user.ApplyEffect(effect));
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
            OnNextTick(() => user.ApplyEffect(effect));
        }
    }
    private static bool IsMoving(RoomUser user) => user.IsWalking || user.HasStatus("mv");

    public bool Thaw(RoomUser user, bool teleport = false) => Thaw(user, teleport, out _);

    /// <param name="effectAfterTeleport">The effect a cancelling teleport puts back when it ends, while the user wears the freeze effect.</param>
    public bool Thaw(RoomUser user, bool teleport, out int? effectAfterTeleport)
    {
        effectAfterTeleport = null;

        if (!_frozen.TryGetValue(user, out var state) || teleport && !state.CancelOnTeleport) {
            return false;
        }

        // Unfreeze lands on the room tick, like Freeze
        // (said at +0, effect 0 at +400 ms), and a teleport that cancels a freeze sends only the teleport effect, no effect 0 first.
        if (!teleport && _executorRoom?.GetGameMap()?.Navigation is { } navigation) {
            navigation.RunOwner(user, (actor, _) =>
            {
                if (_frozen.TryGetValue(actor, out var current)) {
                    ThawActor(actor, current, restoreEffect: true);
                }
            });

            return true;
        }

        ThawActor(user, state, restoreEffect: !teleport);

        // The teleport that cancels a freeze ends with the effect worn before the freeze.
        if (teleport && WearsFreezeEffect(user, state)) {
            effectAfterTeleport = EffectAfterThaw(state);
        }

        return true;
    }

    private static bool WearsFreezeEffect(RoomUser user, Freeze state)
        => !user.IsBot && state.Effect > 0 && user.GetClient()?.GetHabbo()?.Effects?.CurrentEffect == state.Effect;

    private static int EffectAfterThaw(Freeze state) => state.Previous == state.Effect ? 0 : state.Previous;

    private void ThawActor(RoomUser user, Freeze state, bool restoreEffect)
    {
        _frozen.Remove(user);
        user.Frozen = state.Frozen;
        user.CanWalk = state.CanWalk;

        // Unfreeze sends only the restored effect and no UserUpdate;
        // a user in a Wired team gets the team effect back on unfreeze, not 0.
        if (restoreEffect && !user.IsBot && state.Effect > 0) {
            OnNextTick(() =>
            {
                if (WearsFreezeEffect(user, state)) {
                    user.ApplyEffect(EffectAfterThaw(state));
                }
            });
        }
    }
    /// <summary>Sets the effect Thaw puts back, while the user wears a freeze effect. False when that is not the case.</summary>
    public bool SetEffectAfterThaw(RoomUser user, Func<int, int> effect)
    {
        if (!_frozen.TryGetValue(user, out var state) || state.Effect <= 0) {
            return false;
        }

        _frozen[user] = state with { Previous = effect(state.Previous) };

        return true;
    }
    public void Forget(RoomUser user) => _frozen.Remove(user);
    private void OnNextTick(Action apply)
    {
        lock (_tickEffects) {
            _tickEffects.Add(apply);
        }
    }
    /// <summary>Applies the freeze and unfreeze effects queued since the last room tick; called once per room tick.</summary>
    public void FlushTickEffects()
    {
        Action[] due;

        lock (_tickEffects) {
            if (_tickEffects.Count == 0) {
                return;
            }

            due = [.. _tickEffects];
            _tickEffects.Clear();
        }

        foreach (var apply in due) {
            apply();
        }
    }
    public void Clear() => _frozen.Clear();
}
