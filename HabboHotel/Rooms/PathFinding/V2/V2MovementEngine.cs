using Plus.Database;
using Plus.HabboHotel.Quests;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms.PathFinding;

internal sealed class V2MovementEngine : IMovementEngine
{
    private readonly MovementExecutor _executor;
    private readonly MovementCancellation _cancellation;
    private readonly ForcePlacementService _placement;
    private readonly AdmissionService _admission;
    private readonly RebindService _rebind;
    internal ClaimLedger Claims => Context.Claims;
    internal MovementContext Context { get; }
    internal RollerCycle Rollers { get; }
    internal V2MovementEngine(Room room, RoomNavigation navigation, IDatabase database,
        IRewardTrackManager rewards, ActorAccessResolver access)
    {
        Context = new(room, navigation, new LandingEffects(room, database),
            new FloorEffectService(room, client => rewards.Progress(client, RewardTrackActions.Swim)),
            new MovementProfileService(room, navigation.Grid, navigation.Settings, access));
        navigation.Compiler.SurfacePinned = surface => Context.Claims.Pinned(navigation.Grid.SlotOf(surface));
        _cancellation = new(Context);
        _placement = new(room, navigation, Context, _cancellation);
        _admission = new(room, Context, _placement);
        _rebind = new RebindService(room, navigation.Grid);
        var fallback = new RouteFallbackService(Context, _cancellation);
        Context.Geometry = new(Context, _rebind);
        Rollers = new(room, new RollerTransport(navigation, Context, _placement));
        navigation.Inputs.ItemPublished = Context.Approaches.CancelItem;
        var approaches = new ApproachCompletion(room, navigation, Context.Approaches);
        var commit = new CommitService(room, navigation, Context, _cancellation, fallback, approaches);
        var intake = new CommandIntake(Context, _placement);
        var search = new MovementSearch(room, navigation, Context, _cancellation, fallback, approaches);
        var announce = new AnnounceService(room, navigation, Context, _cancellation, fallback, search);
        _executor = new(room, Context, _rebind, commit, intake, search, announce, new(room));
    }
    internal void RefreshPostures()
    {
        foreach (var actor in Context.Room.GetRoomUserManager().GetUserList()) {
            if (actor.Movement.State != NavState.Active) {
                continue;
            }

            _rebind.Rebind(actor);
            Context.RefreshMembership(actor);
        }
    }
    // The height the announced step lands on now, worked out as the landing does; true when its mv status changed.
    internal bool RefreshStepHeight(RoomUser actor)
    {
        var state = actor.Movement;

        if (!actor.IsWalking || !actor.SetStep || actor.RidingHorse || state.State != NavState.Active || state.PendingCount == 0
            || !actor.Statusses.TryGetValue("mv", out var announced)) {
            return false;
        }

        var surface = state.Pending[state.PendingCount - 1];
        var landing = Context.Graph.ResolveLanding(surface, state.PendingView, Context.Graph.Position(surface, state.PendingView).Z);
        var target = landing.Position;

        if (landing.Support == null || (target.X, target.Y) != (actor.SetX, actor.SetY)) {
            return false;
        }

        var mv = $"{target.X},{target.Y},{TextHandling.GetString(target.Z + Context.Graph.RiderOffset(actor, target.Slot))}";

        if (mv == announced) {
            return false;
        }

        actor.SetStatus("mv", mv);
        actor.SetZ = target.Z;

        return true;
    }
    public void Tick() => _executor.Tick();
    internal void LandDeferredRollers() => _executor.LandDeferredRollers();
    internal void Handle(RoomCommand command)
    {
        var actor = command.Actor;

        if (command.LifetimeId != actor.Movement.LifetimeId) {
            return;
        }

        if (command.Kind == RoomCommandKind.Remove) {
            _cancellation.Remove(actor);

            return;
        }

        if (actor.Movement.State == NavState.Removing) {
            return;
        }

        switch (command.Kind) {
            case RoomCommandKind.ActorAction:
                command.Action?.Invoke(actor, command.CommandSequence);
                break;
            case RoomCommandKind.Admit:
                _admission.Admit(actor);
                break;
            case RoomCommandKind.Cancel:
                _cancellation.Cancel(actor, command.CommandSequence);
                break;
            case RoomCommandKind.ForcePlace:
                _placement.Place(actor, command);
                break;
        }
    }
}
