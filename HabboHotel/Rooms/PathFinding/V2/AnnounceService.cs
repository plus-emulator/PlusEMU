using Plus.Utilities;

namespace Plus.HabboHotel.Rooms.PathFinding;

internal sealed class AnnounceService(Room room, RoomNavigation navigation, MovementContext context,
    MovementCancellation cancellation, RouteFallbackService fallback, MovementSearch search)
{
    private readonly BlockedStepPolicy _blocked = new(navigation.Settings, cancellation, fallback);
    private readonly MovementRules _rules = new(navigation.Grid, navigation.Settings);
    private readonly WalkSurfaceResolver _surfaces = new(context);
    public void Announce(RoomUser actor, bool committed)
    {
        var state = actor.Movement;
        _blocked.TickStall(actor, committed);

        if (!state.HasIntent || state.Cursor >= state.Route.Count || state.PendingCount != 0) {
            ClearCompletedAnnouncement(actor);
            ApplyIdleEffects(actor, committed);

            return;
        }

        if (actor.Freezed) {
            actor.RemoveStatus("mv");
            actor.UpdateNeeded = true;
            ApplyIdleEffects(actor, committed);

            return;
        }

        while (state.HasIntent) {
            var target = BuildBatch(actor, out var temporaryBlock, out var rejected);

            if (state.PendingCount != 0) {
                Publish(actor, target);

                return;
            }

            if (temporaryBlock && state.WaitTicks++ < navigation.Settings.BlockWaitTicks) {
                actor.RemoveStatus("mv");
                actor.UpdateNeeded = true;

                break;
            }

            if (state.Plan is not { } plan || rejected is not { } failure) {
                cancellation.Cancel(actor);

                break;
            }

            var discovered = Remember(plan, failure);
            var retry = plan.RetryRecovery;
            plan.RetryRecovery = false;

            if (!discovered && !retry) {
                cancellation.Cancel(actor);

                break;
            }

            if (TryRecoveryStep(actor, failure, out target)) {
                plan.PendingRecovery = true;
                Publish(actor, target);

                return;
            }

            plan.DiscoverNeighbours(navigation.Grid, _rules, context.Profiles.Refresh(actor), failure.From);
            fallback.OnRouteBlocked(actor);
            search.Recover(actor);
        }

        ApplyIdleEffects(actor, committed);
    }
    private static void ClearCompletedAnnouncement(RoomUser actor)
    {
        if (actor.IsBot && actor.RidingHorse) {
            return;
        }

        if (actor.Movement.PendingCount != 0 || !actor.HasStatus("mv")) {
            return;
        }

        actor.RemoveStatus("mv");
        actor.UpdateNeeded = true;
    }
    private NavPosition BuildBatch(RoomUser actor, out bool temporaryBlock, out RejectedStep? rejected)
    {
        temporaryBlock = false;
        rejected = null;
        var state = actor.Movement;
        var profile = context.Profiles.Refresh(actor);
        state.PendingView = state.Route.View;
        var from = new NavPosition(actor.X, actor.Y, state.SupportZ);
        var limit = actor.SuperFastWalking ? 3 : actor.FastWalking ? 2 : 1;

        for (var i = state.Cursor; i < state.Route.Count && state.PendingCount < limit; i++) {
            var surface = state.Route.Steps[i];

            var purpose = state.Route.PurposeAt(i, state.Origin);
            var landing = _surfaces.Resolve(actor, i, from, surface, state.Route.View, state.Route.AdvisoryZ(i), purpose);
            var target = landing.Position;
            var reason = StepReason.NotStandable;

            if (landing.Support == null && !profile.LegacyOverride && purpose != StepPurpose.Interaction
                || !ClaimStep(actor, profile, from, target, purpose, out temporaryBlock, out reason)) {
                rejected = new(from, surface, reason);

                break;
            }

            state.PendingPurpose[state.PendingCount] = purpose;
            state.PendingZ[state.PendingCount] = target.Z;
            state.Pending[state.PendingCount++] = landing.Support ?? surface;
            from = target;
        }

        return from;
    }
    private bool ClaimStep(RoomUser actor, ActorProfile profile, NavPosition from, NavPosition target,
        StepPurpose purpose, out bool temporaryBlock, out StepReason reason)
    {
        temporaryBlock = false;
        reason = StepReason.Ok;
        var slot = target.Slot;
        var occupancy = context.OccupancyAt(actor, slot);
        var mask = ClaimMatrix.BlockingMask(profile, navigation.Grid.Flags[slot], purpose, OccupancyView.Execution);
        var result = _rules.CanStep(profile, from, target, purpose, OccupancyView.Execution, occupancy);

        if (!result.Ok) {
            reason = result.Reason;
            var blockers = occupancy.Targets[slot] & mask;
            var transient = TargetOccupancy.Walking | TargetOccupancy.ExclusiveClaim | TargetOccupancy.GoalClaim | TargetOccupancy.SharedClaim | TargetOccupancy.RollerClaim;
            temporaryBlock = result.Reason == StepReason.Occupied && (blockers & ~transient) == 0;

            return false;
        }

        if (!context.Claims.TryClaim(actor, slot, ClaimMatrix.KindFor(profile, navigation.Grid.Flags[slot], purpose), mask)) {
            reason = StepReason.Occupied;
            temporaryBlock = true;

            return false;
        }

        context.GuildGates.Accept(actor, profile, slot, purpose);

        return true;
    }
    private readonly record struct RejectedStep(NavPosition From, SurfaceRef Target, StepReason Reason);

    private static bool Remember(WalkPlan plan, RejectedStep failure) => failure.Reason switch
    {
        StepReason.TooHigh or StepReason.TooLow or StepReason.CornerBlocked or StepReason.CornerVoid
            => plan.Exclusions.Block(failure.From, failure.Target),
        StepReason.Occupied => plan.Exclusions.BlockUntilNextStep(failure.Target),
        _ => plan.Exclusions.Block(failure.Target)
    };

    private bool TryRecoveryStep(RoomUser actor, RejectedStep failure, out NavPosition target)
    {
        target = failure.From;
        var grid = navigation.Grid;
        var tx = failure.Target.Tile % grid.Width;
        var ty = failure.Target.Tile / grid.Width;

        if (tx == failure.From.X || ty == failure.From.Y || actor.Movement.Origin == MoveOrigin.Interaction) {
            return false;
        }

        // Observed on AIR WIN63-202610151745-318700621: try the Y component, then X.
        return TryRecoveryTile(actor, failure.From, failure.From.X, ty, out target)
            || TryRecoveryTile(actor, failure.From, tx, failure.From.Y, out target);
    }

    private bool TryRecoveryTile(RoomUser actor, NavPosition from, int x, int y, out NavPosition target)
    {
        target = from;
        var grid = navigation.Grid;
        var tile = grid.Tile(x, y);
        var profile = context.Profiles.Refresh(actor);

        // Prefer the original level; a separate deck at this XY is not an escape from a floor blocker.
        var slot = SurfaceSelection.Resting(grid, tile, from.Z);
        SurfaceRef? planned = null;

        if (actor.Movement.Plan is { } plan) {
            var originalSlot = slot < 0 ? -1 : plan.Grid.SlotOf(grid.Reference(slot));

            if (originalSlot < 0 || !plan.Grid.Active(originalSlot)) {
                originalSlot = SurfaceSelection.Resting(plan.Grid, tile, from.Z);
            }

            if (originalSlot < 0) {
                return false;
            }

            var original = plan.Grid.Position(originalSlot);
            var originalFrom = new NavPosition(from.X, from.Y, original.Z);
            var originalRules = new MovementRules(plan.Grid, navigation.Settings);

            // Preserve the original tile's eligibility. Height comes from the live step below,
            // so lowering two supports together cannot create a height mismatch across versions.
            if (!plan.Exclusions.Allows(from, plan.Grid.Reference(originalSlot))
                || !originalRules.CanStep(profile, originalFrom, original,
                    StepPurpose.Transit, OccupancyView.Planning).Ok) {
                return false;
            }

            planned = plan.Grid.Reference(originalSlot);
        }

        var reason = StepReason.NotStandable;

        if (slot < 0 || !ClaimStep(actor, profile, from, grid.Position(slot), StepPurpose.Transit, out _, out reason)) {
            // Inferred from routes observed on AIR WIN63-202610151745-318700621: later repairs avoid a fallback tile found blocked.
            if (actor.Movement.Plan is { } known && planned is { } surface) {
                Remember(known, new(from, surface, reason));
            }

            return false;
        }

        var candidate = grid.Position(slot);
        var state = actor.Movement;
        state.PendingView = GraphView.Surface;
        state.Pending[0] = grid.Reference(slot);
        state.PendingZ[0] = candidate.Z;
        state.PendingPurpose[0] = StepPurpose.Transit;
        state.PendingCount = 1;
        target = candidate;

        return true;
    }
    private void ApplyIdleEffects(RoomUser actor, bool committed)
    {
        if (!committed && actor.Movement.PendingCount == 0) {
            context.FloorEffects.Apply(actor, actor.X, actor.Y, actor.Movement.CurrentRef);
        }
    }
    private void Publish(RoomUser actor, NavPosition target)
    {
        var state = actor.Movement;
        state.Plan?.Exclusions.NextStep();
        actor.Statusses.Remove("sit");
        actor.Statusses.Remove("lay");
        actor.IsSitting = actor.IsLying = false;
        EndArrival(actor);
        var z = target.Z + context.Graph.RiderOffset(actor, target.Slot);
        actor.SetStatus("mv", $"{target.X},{target.Y},{TextHandling.GetString(z)}");
        actor.RotBody = actor.RotHead = Rotation.Calculate(actor.X, actor.Y, target.X, target.Y, actor.MoonwalkEnabled);
        actor.SetStep = true;
        actor.SetX = target.X;
        actor.SetY = target.Y;
        actor.SetZ = target.Z;
        actor.IsWalking = true;
        actor.UpdateNeeded = true;
        context.FloorEffects.Apply(actor, target.X, target.Y, state.Pending[state.PendingCount - 1]);
        AnnounceWiredWalk(actor, target);
    }
    // A teleport or hopper arrival ends with the first step out; until then the teleporters and doorbell treat the user as arriving.
    private static void EndArrival(RoomUser actor)
    {
        if (actor.IsBot || actor.IsPet || actor.GetClient()?.GetHabbo() is not { } habbo) {
            return;
        }

        if (habbo.IsTeleporting) {
            habbo.IsTeleporting = false;
            habbo.TeleporterId = 0;
        }
        else if (habbo.IsHopping) {
            habbo.IsHopping = false;
            habbo.HopperId = 0;
        }
    }
    // Observed on hhus (AIR WIN63-202610131318-860371703): Wired walk-off and walk-on fire in the tick whose UserUpdate announces the
    // step (mv towards the next tile), one walk tick before the landing. The landing still runs the furni interactions.
    private void AnnounceWiredWalk(RoomUser actor, NavPosition target)
    {
        var state = actor.Movement;
        var revision = state.LocationRevision;
        var map = room.GetGameMap();
        var grid = navigation.Grid;
        var origin = SurfaceContacts.Of(room, actor, map.GetCoordinatedItems(new(actor.X, actor.Y)));
        var contact = SurfaceContacts.ContactSlot(grid, target.X, target.Y, state.Pending[state.PendingCount - 1], target.Z);
        var landing = SurfaceContacts.Filter(grid, target.X, target.Y, contact, map.GetCoordinatedItems(new(target.X, target.Y)));

        foreach (var item in origin) {
            item.WiredWalksOffFurni(actor);

            if (state.LocationRevision != revision || state.State != NavState.Active) {
                return;
            }
        }

        foreach (var item in landing) {
            item.WiredWalksOnFurni(actor);

            if (state.LocationRevision != revision || state.State != NavState.Active) {
                return;
            }
        }
    }
    public void SynchronizeHorse(RoomUser actor)
    {
        if (!actor.RidingHorse || actor.IsBot) {
            return;
        }

        var horse = room.GetRoomUserManager().GetRoomUserByVirtualId(actor.HorseId);

        // A rolling pair glides on its slides; a status now would snap the horse ahead of its rider.
        if (horse == null || actor.IsRolling || horse.IsRolling) {
            return;
        }

        if (actor.HasStatus("mv")) {
            horse.SetStatus("mv", $"{actor.SetX},{actor.SetY},{TextHandling.GetString(actor.SetZ)}");
            horse.SetStep = actor.SetStep;
            horse.SetX = actor.SetX;
            horse.SetY = actor.SetY;
            horse.SetZ = actor.SetZ;
            horse.RotBody = actor.RotBody;
            horse.RotHead = actor.RotHead;
            horse.IsWalking = actor.IsWalking;
        }
        else {
            horse.RemoveStatus("mv");
            horse.SetStep = false;
            horse.IsWalking = false;
        }

        // The horse's status follows its rider's, not every tick.
        horse.UpdateNeeded |= actor.UpdateNeeded;
        context.RefreshMembership(horse);
    }
}
