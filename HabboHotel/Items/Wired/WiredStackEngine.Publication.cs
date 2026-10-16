using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Items.Wired;

internal sealed partial class WiredStackEngine
{
    private WiredFurniturePublication? _activePublication;
    private long _publicationEpoch;
    private Func<bool>? _movementPublicationBindingsCurrent;
    internal bool PublicationBridgesAreTrusted { get; set; }
    internal Func<IWiredItem, bool>? PublicationEvaluationIsSilent { get; set; }
    internal Func<WiredRuntimeEvent, bool>? PublicationObserverIsSilent { get; set; }
    internal Func<bool>? PublicationPollIsSilent { get; set; }
    internal Func<bool>? PublicationFlushIsSilent { get; set; }

    internal void SealExternalPublication() => SealPublication();

    private void SealPublication()
    {
        var publication = _activePublication;
        _activePublication = null;
        publication?.Flush();
    }

    private WiredFurniturePublication? PublicationFor(IWiredItem box, WiredRuntimeContext? context)
    {
        var scheduled = _executingAction;

        // Observed on AIR WIN63-202610131318-860371703: a Write to Logs box on a running Fast Queue stack does not split the pulse;
        // every pulse still publishes one home-to-home entry per tile. A box that sends nothing to the room leaves the open
        // publication as it is.
        if (box is WiredModernAction { Descriptor.CanonicalName: "wf_act_log" or "wf_act_neg_log" }) {
            return null;
        }

        if (context == null || scheduled?.Callback != null || box is not WiredModernAction action
            || !action.SupportsPublication(context) || action.PublicationBridge && !PublicationBridgesAreTrusted || scheduled == null) {
            SealPublication();

            return null;
        }

        var inherited = context.InheritedPublication;
        var deadline = scheduled.Chain.FirstResumeAt!.Value + scheduled.DelayMilliseconds;

        if (scheduled.DelayMilliseconds == 0 && inherited is { Open: true } && inherited.Epoch == _publicationEpoch
            && ReferenceEquals(inherited, _activePublication)) {
            return inherited;
        }

        if (_activePublication is { Open: true } active && ReferenceEquals(active.Root, scheduled.Chain)
            && active.Deadline == deadline && active.Epoch == _publicationEpoch) {
            return active;
        }

        SealPublication();
        _activePublication = new(context.Room, scheduled.Chain, deadline, _publicationEpoch);

        return _activePublication;
    }

    private void BeforePublicationDispatch(PendingDispatch pending)
    {
        if (_activePublication == null) {
            return;
        }

        if (pending.Event.Kind == WiredEventKind.Collision) {
            if (pending.CollisionProof is not { } collision || !CollisionStillSilent(collision)) {
                SealPublication();
            }

            return;
        }

        var inherited = pending.Publication;
        var silentObserver = ObserveEvent == null || PublicationObserverIsSilent?.Invoke(pending.Event) == true;
        var empty = pending.Call == null && pending.Signal == null && pending.Current == null
            && pending.Triggers is { Length: 0 } && pending.LegacyTriggers.Length == 0;

        if (silentObserver && (empty || inherited is { Open: true } && ReferenceEquals(inherited, _activePublication))) {
            return;
        }

        // Unrelated walk selectors must not expose an intermediate queue pose. The concrete
        // observer is silent; evaluation/actions still seal normally if a walk stack actually runs.
        if (silentObserver && pending.Event.Kind is WiredEventKind.WalkOn or WiredEventKind.WalkOff
            && _movementPublicationBindingsCurrent?.Invoke() == true && pending.LegacyTriggers.Length == 0
            && pending.Triggers is { } triggers && triggers.All(trigger => trigger.GetType() == typeof(WiredModernTrigger))) {
            return;
        }

        SealPublication();
    }

    // Only the concrete room factory binds this path. Custom action publishers keep Dispatch/Mutate.
    internal Action<WiredModernAction> CreateMovementPublicationFactory(IWiredRuntimeOperations operations,
        Action<RoomUser, IEnumerable<Item>, IEnumerable<Item>> walk)
    {
        var room = _runtimeRoom;
        var targets = _targets;
        var runtimeOperations = _operations;
        var attached = _attached;
        var actorPresent = _actorPresent;
        var actorVisit = _actorVisit;
        var observer = ObserveEvent;
        var classifier = PublicationObserverIsSilent;
        var evaluation = PublicationEvaluationIsSilent;
        var poll = _pollExternal;
        var flush = _flushExternal;
        var needsFast = _externalFastWork;
        var silentPoll = PublicationPollIsSilent;
        var silentFlush = PublicationFlushIsSilent;
        var bridges = PublicationBridgesAreTrusted;
        bool BindingsCurrent() => ReferenceEquals(room, _runtimeRoom) && ReferenceEquals(targets, _targets)
            && ReferenceEquals(runtimeOperations, _operations) && ReferenceEquals(operations, runtimeOperations)
            && ReferenceEquals(attached, _attached)
            && ReferenceEquals(actorPresent, _actorPresent) && ReferenceEquals(actorVisit, _actorVisit)
            && ReferenceEquals(observer, ObserveEvent) && ReferenceEquals(classifier, PublicationObserverIsSilent)
            && ReferenceEquals(evaluation, PublicationEvaluationIsSilent) && ReferenceEquals(poll, _pollExternal)
            && ReferenceEquals(flush, _flushExternal) && ReferenceEquals(needsFast, _externalFastWork)
            && ReferenceEquals(silentPoll, PublicationPollIsSilent) && ReferenceEquals(silentFlush, PublicationFlushIsSilent)
            && bridges == PublicationBridgesAreTrusted;

        _movementPublicationBindingsCurrent = BindingsCurrent;

        return action =>
        {
            BindHeadingCollision(action, operations, BindingsCurrent);
            BindCarryPublication(action, operations, walk, BindingsCurrent);
        };
    }

    private void BindCarryPublication(WiredModernAction action, IWiredRuntimeOperations operations,
        Action<RoomUser, IEnumerable<Item>, IEnumerable<Item>> walk, Func<bool> bindingsCurrent)
    {
        var now = _now;
        bool Current(WiredRuntimeContext context) => bindingsCurrent() && ReferenceEquals(now, _now)
            && RoomOwnerScope.IsOwner(context.Room) && ReferenceEquals(context.Room, _runtimeRoom)
            && ReferenceEquals(context, _runtimeContext) && ReferenceEquals(context.Operations, operations)
            && ReferenceEquals(_executingAction?.Box, action)
            && _items.TryGetValue(action.Item.Id, out var registered) && ReferenceEquals(action, registered)
            && action.WalkBindingIsCurrent(walk);
        WiredFurniturePublication? Token(WiredRuntimeContext context) => Current(context)
            && context.Publication is { Open: true } publication && ReferenceEquals(publication, _activePublication)
            && publication.Epoch == _publicationEpoch && action.SupportsPublication(context) ? publication : null;
        void Seal(WiredRuntimeContext context)
        {
            context.Publication?.Flush();
            SealPublication();
        }

        LegacyCarryProof? legacy = null;
        bool LegacyCurrent(WiredRuntimeContext context, WiredFurniturePublication publication)
        {
            // Bindings must be authoritative before any replaceable accessor or classifier runs.
            if (!bindingsCurrent() || !ReferenceEquals(now, _now) || !Monitor.IsEntered(_sync) || _passDepth == 0
                || !ReferenceEquals(context.Room, _runtimeRoom) || !ReferenceEquals(context, _runtimeContext)
                || !ReferenceEquals(context.Operations, operations) || !ReferenceEquals(_executingAction?.Box, action)
                || !_items.TryGetValue(action.Item.Id, out var registered) || !ReferenceEquals(action, registered)
                || !action.WalkBindingIsCurrent(walk)) {
                return false;
            }

            var map = context.Room.GetGameMap();

            if (context.Room.UsesV2Movement || map == null || map.Navigation != null
                || map.Model.MapSizeX is <= 0 or > 256 || map.Model.MapSizeY is <= 0 or > 256
                || !publication.Open || publication.HasAvatarEntries || !ReferenceEquals(publication, _activePublication)
                || publication.Epoch != _publicationEpoch || !CollisionRegistryUnchanged()
                || _items.Values.OfType<WiredModernTrigger>().Any(trigger => trigger.Events.Contains(WiredEventKind.WalkOn)
                    || trigger.Events.Contains(WiredEventKind.WalkOff) || trigger.Events.Contains(WiredEventKind.Collision))) {
                return false;
            }

            return action.SupportsPublication(context);
        }
        bool LiveActor(WiredRuntimeContext context, RoomUser actor, long lifetime) =>
            actor.Movement.LifetimeId == lifetime && actor.Movement.State != NavState.Removing
            && context.UserIdentity.TryGetValue(actor.VirtualId, out var captured) && ReferenceEquals(actor, captured)
            && ReferenceEquals(context.Room.GetRoomUserManager().GetRoomUserByVirtualId(actor.VirtualId), actor);
        bool PreparedLegacy(WiredRuntimeContext context, RoomUser actor)
        {
            var proof = legacy;

            if (proof == null || !bindingsCurrent() || !ReferenceEquals(now, _now)
                || !ReferenceEquals(proof.Context, context) || !ReferenceEquals(proof.Execution, _executingAction)
                || !ReferenceEquals(context.Publication, proof.Publication)
                || !ReferenceEquals(proof.Root, proof.Publication.Root) || proof.Deadline != proof.Publication.Deadline
                || proof.Epoch != proof.Publication.Epoch || !ReferenceEquals(proof.Registry, _stackSnapshot)
                || proof.Boxes.Length != _items.Count || proof.Boxes.Any(entry =>
                    !_items.TryGetValue(entry.Box.Item.Id, out var box) || !ReferenceEquals(entry.Box, box)
                    || !ReferenceEquals(entry.Configuration, entry.Box.Configuration))
                || action.Item.Placement != proof.Placement || !ReferenceEquals(action.Item.Definition, proof.Definition)
                || !proof.Actors.TryGetValue(actor, out var lifetime) || !LegacyCurrent(context, proof.Publication)
                || !LiveActor(context, actor, lifetime)) {
                legacy = null;

                return false;
            }

            proof.Actors.Remove(actor);

            if (proof.Actors.Count == 0) {
                legacy = null;
            }

            return true;
        }

        action.BindCarryPublication((context, actors) =>
        {
            // Every attempt replaces the stamp, including failed and reentrant moves.
            legacy = null;
            var token = Token(context);

            if (token == null && context.Publication is { } original && LegacyCurrent(context, original)
                && actors.All(actor => LiveActor(context, actor, actor.Movement.LifetimeId))) {
                legacy = new(context, _executingAction!, original, original.Root, original.Deadline, original.Epoch,
                    _stackSnapshot, _items.Values.Cast<IWiredConfiguredItem>().Select(box => (box, box.Configuration)).ToArray(),
                    action.Item.Placement, action.Item.Definition,
                    actors.ToDictionary(actor => actor, actor => actor.Movement.LifetimeId));

                return;
            }

            if (token == null || actors.Any(token.ContainsActor)) {
                // Split before any later furniture commit or actor style, even for equal styles.
                Seal(context);
            }
        }, (context, actor, movement, style) =>
        {
            var token = Token(context);

            if (token != null && context.UserIdentity.TryGetValue(actor.VirtualId, out var captured)
                && ReferenceEquals(actor, captured) && token.AppendAvatar(actor, movement, style)) {
                // Append owns publication even if the original hint reentrantly sealed the token.
                return true;
            }

            if (!PreparedLegacy(context, actor)) {
                Seal(context);
            }

            // Legacy keeps its original immediate hint/body; only furniture remains collected.
            return false;
        });
    }

    private void BindHeadingCollision(WiredModernAction action, IWiredRuntimeOperations operations, Func<bool> bindingsCurrent)
    {
        // The readonly clock binding belongs to this factory-created action.
        var now = _now;
        bool BindingIsCurrent() => bindingsCurrent() && ReferenceEquals(now, _now);
        action.BindHeadingCollision((context, evt) => Pass<bool?>(() =>
        {
            if (!ReferenceEquals(context, _runtimeContext) || !ReferenceEquals(context.Room, _runtimeRoom)
                || !ReferenceEquals(context.Operations, operations) || !ReferenceEquals(_executingAction?.Box, action)
                || !_items.TryGetValue(action.Item.Id, out var registered) || !ReferenceEquals(action, registered)) {
                return null;
            }

            HeadingCollisionProof? proof = null;

            // No replaceable predicate, accessor or observer is invoked before binding/registry checks.
            if (BindingIsCurrent() && context.Publication is { Open: true } publication
                && ReferenceEquals(publication, _activePublication) && publication.Epoch == _publicationEpoch
                && CollisionRegistryUnchanged() && action.SupportsPublication(context)
                && evt is { Kind: WiredEventKind.Collision, EventItem: { } source, Actor: { } actor }
                && context.FurniIdentity.TryGetValue(source.Id, out var sourceIdentity) && ReferenceEquals(source, sourceIdentity)
                && context.UserIdentity.TryGetValue(actor.VirtualId, out var actorIdentity) && ReferenceEquals(actor, actorIdentity)) {
                proof = new(publication, BindingIsCurrent, source, source.Placement, source.Definition,
                    actor, actor.Movement.LifetimeId, _stackSnapshot, _items.Values.Cast<IWiredConfiguredItem>()
                        .Select(box => (box, box.Configuration)).ToArray());

                if (!CollisionStillSilent(proof)) {
                    proof = null;
                }
            }

            if (proof == null) {
                context.Publication?.Flush();
                SealPublication();
            }

            return EnqueueCore(evt, collision: proof, sealOnRejection: true);
        }));
    }

    private bool CollisionRegistryUnchanged()
    {
        // Reject dirty/opaque/legacy registries BEFORE normal RefreshStacks can invoke callbacks.
        if (_stacksDirty || _items.Count != _stackSnapshot.Length || _fastWork != 1) {
            return false;
        }

        var index = 0;
        var keepsFast = false;

        foreach (var box in _items.Values) {
            if (SplitsCarriedPulse(box)) {
                return false;
            }

            keepsFast |= box is WiredModernTimedTrigger timer && RuntimeSupported(timer);
            var previous = _stackSnapshot[index++];

            if (!ReferenceEquals(box, previous.Box) || !_attached(box)
                || (box.Item.GetX, box.Item.GetY, box.Item.GetZ) != (previous.X, previous.Y, previous.Z)
                || box is IWiredContextualTrigger trigger && trigger.Events.Contains(WiredEventKind.Collision)
                    && (!_stacks.TryGetValue((box.Item.GetX, box.Item.GetY), out var stack)
                        || stack.Any(candidate => !ReferenceEquals(candidate, box)))) {
                return false;
            }
        }

        // Even after the empty envelope leaves the queue, this known timer keeps fast-work unchanged.
        // Otherwise an opaque fast-work observer could run before the open segment is sealed.
        return keepsFast;
    }

    // Only a box that can observe a carried tile landing on a user, or publish movement outside its own stack, splits the open
    // pulse. Actions, conditions, selectors, add-ons, variable boxes, chests and non-collision triggers run only when their own
    // stack fires, and that dispatch seals the publication first.
    private static bool SplitsCarriedPulse(IWiredItem box) => box switch
    {
        // Legacy boxes, and box types from outside the server, run callbacks the engine cannot vouch for: an Events getter,
        // a poll or a reset could publish before the open segment is sealed.
        not IWiredConfiguredItem => true,
        _ when box.GetType().Assembly != typeof(WiredStackEngine).Assembly => true,
        // The native timer only reads deadlines when polled; any other timer's poll may publish.
        IWiredTimedTrigger and not WiredModernTimedTrigger => true,
        _ => false
    };

    private bool CollisionStillSilent(HeadingCollisionProof proof) => proof.BindingsCurrent()
        && proof.Publication.Open && ReferenceEquals(proof.Publication, _activePublication)
        && proof.Publication.Epoch == _publicationEpoch && CollisionRegistryUnchanged()
        && ReferenceEquals(proof.RegistrySnapshot, _stackSnapshot) && proof.Boxes.Length == _items.Count
        && proof.Boxes.All(entry => _items.TryGetValue(entry.Box.Item.Id, out var box) && ReferenceEquals(entry.Box, box)
            && ReferenceEquals(entry.Configuration, entry.Box.Configuration))
        && proof.Source.Placement == proof.Placement && ReferenceEquals(proof.Source.Definition, proof.Definition)
        && ReferenceEquals(_runtimeRoom!.GetRoomItemHandler().GetItem(proof.Source.Id), proof.Source)
        && proof.Actor.Movement.LifetimeId == proof.ActorLifetime
        && ReferenceEquals(_runtimeRoom.GetRoomUserManager().GetRoomUserByVirtualId(proof.Actor.VirtualId), proof.Actor);

    private sealed record LegacyCarryProof(WiredRuntimeContext Context, ScheduledAction Execution,
        WiredFurniturePublication Publication, object Root, long Deadline, long Epoch, object Registry,
        (IWiredConfiguredItem Box, WiredConfiguration Configuration)[] Boxes, long Placement, ItemDefinition Definition,
        Dictionary<RoomUser, long> Actors);

    private sealed record HeadingCollisionProof(WiredFurniturePublication Publication, Func<bool> BindingsCurrent,
        Item Source, long Placement, ItemDefinition Definition, RoomUser Actor, long ActorLifetime, object RegistrySnapshot,
        (IWiredConfiguredItem Box, WiredConfiguration Configuration)[] Boxes);
}
