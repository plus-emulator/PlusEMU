using System.Drawing;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Notifications;
using Plus.Core;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Interactor;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Games.Freeze;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.HabboHotel.Items;

public class Item
{
    private object? _navSync;
    internal object NavSync => LazyInitializer.EnsureInitialized(ref _navSync);
    internal bool HasNavigationLock => _navSync != null;
    // Enabled before room admission; attachment may follow or be removed later.
    // PlacementSync may nest NavSync. NavSync never nests PlacementSync or callbacks.
    private bool _navigationSynchronized;
    internal void EnableNavigationSynchronization() => Volatile.Write(ref _navigationSynchronized, true);
    private NavInputs? _navigationInputs;
    internal NavInputs? NavigationInputs
    {
        get => Volatile.Read(ref _navigationInputs);
        set => Volatile.Write(ref _navigationInputs, value);
    }

    private void PublishIfAttached(bool stateOnly = false)
    {
        if (!Volatile.Read(ref _navigationSynchronized)) {
            return;
        }

        lock (NavSync) {
            if (NavigationInputs is { } inputs && (!stateOnly || NavItemRecord.StateRelevant(Definition))) {
                inputs.PublishCurrent(this);
            }
        }
    }
    internal int NavMutationDepth;
    private long _stateGeneration;
    // Bumped when the stored state value changes and by every UpdateState; lets queued approach intents notice state changes that never reach a nav record.
    internal long StateGeneration => Volatile.Read(ref _stateGeneration);
    private long _placement;
    // Changes on every attach and detach, so writes from an earlier placement of this instance are never reported.
    internal long Placement => Volatile.Read(ref _placement);

    public uint Id { get; set; }
    public bool IsTemporary { get; internal init; }
    /// <summary>True while a room move (not a new placement) runs the interactor's placement hook.</summary>
    internal bool Moving { get; set; }
    // Placed from the Builders Club catalogue: it counts against the placer's Builders Club furni limit and is deleted,
    // never stored, when it leaves the room.
    public bool IsBuildersClub { get; init; }
    public uint OwnerId { get; set; }
    public uint RoomId { get; set; }
    public ItemDefinition Definition { get; set; }
    private IFurniObjectData _extraData = FurniObjectData.Empty;
    public IFurniObjectData ExtraData
    {
        get => _extraData;
        set
        {
            if (!Volatile.Read(ref _navigationSynchronized)) {
                StoreExtraData(value);

                return;
            }

            lock (NavSync) {
                StoreExtraData(value);

                if (NavigationInputs is { } inputs && NavItemRecord.StateRelevant(Definition)) {
                    inputs.PublishCurrent(this);
                }
            }
        }
    }
    /// <summary>Seconds a native game timer restarts and resets to. 0 until the timer is attached.</summary>
    public int TimerDurationSeconds { get; set; }
    public uint UniqueNumber { get; set; }
    public uint UniqueSeries { get; set; }
    public string WallCoordinates = string.Empty;

    public string LegacyDataString
    {
        get
        {
            if (ExtraData is LegacyDataFormat data) {
                return data.Data;
            }

            return string.Empty;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set
        {
            if (!Volatile.Read(ref _navigationSynchronized)) {
                if (_extraData is LegacyDataFormat data) {
                    var before = data.Data;
                    var changed = before != value;
                    data.Data = value;

                    if (changed) {
                        MarkInteractionStateChanged();
                        NoteStateWrite();
                    }
                }

                return;
            }

            SetNavigationState(value);
        }
    }



    private void SetNavigationState(string value)
    {
        LegacyDataFormat? changed = null;

        lock (NavSync) {
            if (_extraData is LegacyDataFormat data) {
                var before = data.Data;
                var different = before != value;
                data.StoreWithoutNotification(value);

                if (different) {
                    MarkInteractionStateChanged();
                    NoteStateWrite();
                }

                changed = data;
            }

            if (NavigationInputs is { } inputs && NavItemRecord.StateRelevant(Definition)) {
                inputs.PublishCurrent(this);
            }
        }

        changed?.NotifyDataUpdated();
    }

    // v2 gate sequencer: the same write, but the notification is left to the caller (after its locks are released).
    internal LegacyDataFormat? StoreStateQuietly(string value)
    {
        if (!Volatile.Read(ref _navigationSynchronized)) {
            if (_extraData is not LegacyDataFormat plain) {
                return null;
            }

            var before = plain.Data;
            var different = before != value;
            plain.StoreWithoutNotification(value);

            if (different) {
                MarkInteractionStateChanged();
                NoteStateWrite();
            }

            return plain;
        }

        LegacyDataFormat? changed = null;

        lock (NavSync) {
            if (_extraData is LegacyDataFormat data) {
                var before = data.Data;
                var different = before != value;
                data.StoreWithoutNotification(value);

                if (different) {
                    MarkInteractionStateChanged();
                    NoteStateWrite();
                }

                changed = data;
            }

            if (NavigationInputs is { } inputs && NavItemRecord.StateRelevant(Definition)) {
                inputs.PublishCurrent(this);
            }
        }

        return changed;
    }

    private void StoreExtraData(IFurniObjectData value)
    {
        var replaced = !ReferenceEquals(_extraData, value);
        var previous = _extraData;
        _extraData = value;

        if (!replaced) {
            return;
        }

        MarkInteractionStateChanged();

        // Only a legacy state replaced by another legacy state is a state change; format swaps are not.
        if (previous is LegacyDataFormat before && value is LegacyDataFormat after
            && !string.Equals(before.Data, after.Data, StringComparison.Ordinal)) {
            NoteStateWrite();
        }
    }

    // Store first, then bump: a click that captured the old generation can only be invalidated, never wrongly kept.
    private void MarkInteractionStateChanged() => Interlocked.Increment(ref _stateGeneration);

    // Every actual legacy state write, for Wired to report once. Callers may hold NavSync: this only enqueues.
    private void NoteStateWrite()
    {
        if (_room is { } room) {
            FurnitureStateEvents.Record(room, this);
        }
    }

    /// TODO @80O: Cleanup shit below
    private Room? _room;
    private TimeProvider? _interactionClock;
    private IItemInteractorFactory? _interactors;
    private IItemTravelStore? _travelStore;
    private IRewardTrackManager? _rewards;
    private bool _updateNeeded;
    [Obsolete]
    public int BaseItem;
    public FreezePowerUp FreezePowerUp;
    private int _groupId;
    public int GroupId
    {
        get => _groupId;
        set
        {
            if (!Volatile.Read(ref _navigationSynchronized)) {
                _groupId = value;

                return;
            }

            lock (NavSync) {
                if (_groupId == value) {
                    return;
                }

                _groupId = value;
                NavigationInputs?.PublishCurrent(this);
            }
        }
    }
    public int InteractingBallUser;
    public int InteractingUser;
    public int InteractingUser2;
    public byte InteractionCount;
    public byte InteractionCountHelper;
    public bool MagicRemove = false;
    public bool PendingReset = false;
    private int _rotation;
    public int Rotation
    {
        get => _rotation;
        set
        {
            if (!Volatile.Read(ref _navigationSynchronized)) {
                _rotation = value;

                return;
            }

            lock (NavSync) {
                if (_rotation == value) {
                    return;
                }

                _rotation = value;
                NavigationInputs?.PublishCurrent(this);
            }
        }
    }

    public Team Team;
    public int UpdateCounter;
    public int UserId;
    public string Username = string.Empty;


    public int Value;

    private sealed record Footprint(int Length, int Width, int X, int Y, int Rotation, IReadOnlyDictionary<int, ThreeDCoord> Tiles);
    private Footprint? _footprint;

    /// <summary>
    /// The tiles past the anchor that the furni covers. Always derived from its definition, position and rotation, so no load, move or
    /// rotate path can leave it stale.
    /// </summary>
    public IReadOnlyDictionary<int, ThreeDCoord> GetAffectedTiles
    {
        get
        {
            if (Definition is not { } definition) {
                return EmptyFootprint;
            }

            var (x, y, rotation) = (_getX, _getY, _rotation);
            var cached = Volatile.Read(ref _footprint);

            if (cached is not null && cached.Length == definition.Length && cached.Width == definition.Width && cached.X == x && cached.Y == y &&
                cached.Rotation == rotation) {
                return cached.Tiles;
            }

            var tiles = Gamemap.GetAffectedTiles(definition.Length, definition.Width, x, y, rotation);
            Volatile.Write(ref _footprint, new(definition.Length, definition.Width, x, y, rotation, tiles));

            return tiles;
        }
    }

    private static readonly IReadOnlyDictionary<int, ThreeDCoord> EmptyFootprint = new Dictionary<int, ThreeDCoord>();

    private int _getX;
    public int GetX
    {
        get => _getX;
        set
        {
            if (!Volatile.Read(ref _navigationSynchronized)) {
                _getX = value;

                return;
            }

            lock (NavSync) {
                if (_getX == value) {
                    return;
                }

                _getX = value;
                NavigationInputs?.PublishCurrent(this);
            }
        }
    }

    private int _getY;
    public int GetY
    {
        get => _getY;
        set
        {
            if (!Volatile.Read(ref _navigationSynchronized)) {
                _getY = value;

                return;
            }

            lock (NavSync) {
                if (_getY == value) {
                    return;
                }

                _getY = value;
                NavigationInputs?.PublishCurrent(this);
            }
        }
    }

    private double _getZ;
    public double GetZ
    {
        get => _getZ;
        set
        {
            if (!Volatile.Read(ref _navigationSynchronized)) {
                _getZ = value;

                return;
            }

            lock (NavSync) {
                if (_getZ == value) {
                    return;
                }

                _getZ = value;
                NavigationInputs?.PublishCurrent(this);
            }
        }
    }

    public bool UpdateNeeded
    {
        get => _updateNeeded;
        set
        {
            if (value && GetRoom() is { } room) {
                room.GetRoomItemHandler().QueueRoomItemUpdate(this);
            }

            _updateNeeded = value;
        }
    }

    [Obsolete("Will be removed in near future. @80O")]
    public bool IsRoller { get; }

    [Obsolete("Will be removed in near future. @80O")]
    public Point Coordinate => new(GetX, GetY);

    [Obsolete("Will be removed in near future. @80O")]
    public List<Point> GetCoords
    {
        get
        {
            var toReturn = new List<Point>
            {
                Coordinate
            };

            foreach (var tile in GetAffectedTiles.Values) {
                toReturn.Add(new(tile.X, tile.Y));
            }

            return toReturn;
        }
    }

    /// <summary>A stacking height set by the Override Height effect; null keeps the definition's height. Not persisted.</summary>
    internal double? WiredHeightOverride { get; set; }

    public double CurrentHeight
    {
        get
        {
            if (WiredHeightOverride is { } overridden) {
                return overridden;
            }

            if (Definition.AdjustableHeights is { Count: > 1 } heights
                && int.TryParse(LegacyDataString, out var index) && index >= 0 && index < heights.Count
                && GetZ + heights[index] >= 0) {
                return heights[index];
            }

            return Definition.Height;
        }
    }

    public double TotalHeight => GetZ + CurrentHeight;

    public bool IsWallItem => Definition.Type == ItemType.Wall;

    public bool IsFloorItem => Definition.Type == ItemType.Floor;

    public Point SquareInFront
    {
        get
        {
            var sq = new Point(GetX, GetY);

            if (Rotation == 0) {
                sq.Y--;
            }
            else if (Rotation == 2) {
                sq.X++;
            }
            else if (Rotation == 4) {
                sq.Y++;
            }
            else if (Rotation == 6) {
                sq.X--;
            }

            return sq;
        }
    }

    public Point SquareBehind
    {
        get
        {
            var sq = new Point(GetX, GetY);

            if (Rotation == 0) {
                sq.Y++;
            }
            else if (Rotation == 2) {
                sq.X--;
            }
            else if (Rotation == 4) {
                sq.Y--;
            }
            else if (Rotation == 6) {
                sq.X++;
            }

            return sq;
        }
    }

    public Point SquareLeft
    {
        get
        {
            var sq = new Point(GetX, GetY);

            if (Rotation == 0) {
                sq.X++;
            }
            else if (Rotation == 2) {
                sq.Y--;
            }
            else if (Rotation == 4) {
                sq.X--;
            }
            else if (Rotation == 6) {
                sq.Y++;
            }

            return sq;
        }
    }

    public Point SquareRight
    {
        get
        {
            var sq = new Point(GetX, GetY);

            if (Rotation == 0) {
                sq.X--;
            }
            else if (Rotation == 2) {
                sq.Y++;
            }
            else if (Rotation == 4) {
                sq.X++;
            }
            else if (Rotation == 6) {
                sq.Y--;
            }

            return sq;
        }
    }

    public IFurniInteractor Interactor => (_interactors
        ?? throw new InvalidOperationException("Item must be attached to a room before accessing its interactor."))
        .Create(this, InteractionClock());

    private TimeProvider InteractionClock() => _interactionClock
        ?? throw new InvalidOperationException("A room-bound interaction clock is required.");

    internal void BindInteractionClock(TimeProvider clock) => _interactionClock = clock;

    public bool IsWired
    {
        get
        {
            if (Definition.WiredDescriptor != null) {
                return true;
            }

            switch (Definition.InteractionType) {
                case InteractionType.WiredSelector:
                case InteractionType.WiredAddon:
                case InteractionType.WiredVariable:
                case InteractionType.WiredEffect:
                case InteractionType.WiredTrigger:
                case InteractionType.WiredCondition:
                    return true;
            }

            return false;
        }
    }

    public List<Point> GetSides()
    {
        var sides = new List<Point>
        {
            SquareBehind,
            SquareInFront,
            SquareLeft,
            SquareRight,
            Coordinate
        };

        return sides;
    }

    private long _movementGeneration;
    internal long MovementGeneration => Interlocked.Read(ref _movementGeneration);

    public void SetState(int pX, int pY, double pZ)
        => SetPlacementState(pX, pY, pZ, null);

    internal void SetPlacementState(int pX, int pY, double pZ, int? rotation)
    {
        if (!Volatile.Read(ref _navigationSynchronized)) {
            WritePlacement(pX, pY, pZ, rotation);

            return;
        }

        lock (NavSync) {
            WritePlacement(pX, pY, pZ, rotation);
            NavigationInputs?.PublishCurrent(this);
        }
    }

    // Field writes only: no room, Wired, map, database or network callbacks under NavSync.
    private void WritePlacement(int pX, int pY, double pZ, int? rotation)
    {
        if (_getX != pX || _getY != pY || !double.IsInfinity(pZ) && _getZ != pZ) {
            Interlocked.Increment(ref _movementGeneration);
        }

        _getX = pX;
        _getY = pY;

        if (!double.IsInfinity(pZ)) {
            _getZ = pZ;
        }

        if (rotation is { } direction) {
            _rotation = direction;
        }

        MagicTileHeight.Sync(this);
    }

    // v2: a refused automatic close keeps its update request, so the gate never stays open for good.
    private void CloseAutomatically(int retryCycles)
    {
        if (GateTransitionService.Apply(this, "0", GateCloseReason.Automatic, persist: false) == GateTransition.Refused) {
            RequestUpdate(retryCycles, false);
        }
    }

    public void ProcessUpdates()
    {
        var room = GetRoom();

        if (room == null) {
            // Detached items leave the update queue, so a later placement can restart them.
            UpdateNeeded = false;
            UpdateCounter = 0;

            return;
        }

        try {
            UpdateCounter--;

            if (UpdateCounter <= 0) {
                UpdateNeeded = false;
                UpdateCounter = 0;
                RoomUser? user = null;
                RoomUser? user2 = null;

                switch (Definition.InteractionType) {
                    case InteractionType.GuildGate: {
                            if (LegacyDataString == "1") {
                                if (GateTransitionService.For(this) != null) {
                                    CloseAutomatically(2);
                                }
                                else if (room.GetRoomUserManager().GetUserForSquare(GetX, GetY) == null) {
                                    LegacyDataString = "0";
                                    UpdateState(false, true);
                                }
                                else {
                                    RequestUpdate(2, false);
                                }
                            }

                            break;
                        }
                    case InteractionType.Effect: {
                            if (LegacyDataString == "1") {
                                if (room.GetRoomUserManager().GetUserForSquare(GetX, GetY) == null) {
                                    LegacyDataString = "0";
                                    UpdateState(false, true);
                                }
                                else {
                                    RequestUpdate(2, false);
                                }
                            }

                            break;
                        }
                    case InteractionType.OneWayGate:
                        user = null;

                        if (InteractingUser > 0) {
                            user = room.GetRoomUserManager().GetRoomUserByHabbo(InteractingUser);
                        }

                        if (user != null && user.X == GetX && user.Y == GetY) {
                            LegacyDataString = "1";
                            user.RequestInteractionStep(room, SquareBehind);

                            user.InteractingGate = false;
                            user.GateId = 0;
                            RequestUpdate(1, false);
                            UpdateState(false, true);
                        }
                        else if (user != null && user.Coordinate == SquareBehind) {
                            user.UnlockWalking();
                            LegacyDataString = "0";
                            InteractingUser = 0;
                            user.InteractingGate = false;
                            user.GateId = 0;
                            UpdateState(false, true);
                        }
                        else if (LegacyDataString == "1") {
                            LegacyDataString = "0";
                            UpdateState(false, true);
                        }

                        if (user == null) {
                            InteractingUser = 0;
                        }

                        break;
                    case InteractionType.GateVip:
                        user = null;

                        if (InteractingUser > 0) {
                            user = room.GetRoomUserManager().GetRoomUserByHabbo(InteractingUser);
                        }

                        var newY = 0;
                        var newX = 0;

                        if (user != null && user.X == GetX && user.Y == GetY) {
                            if (user.RotBody == 4) {
                                newY = 1;
                            }
                            else if (user.RotBody == 0) {
                                newY = -1;
                            }
                            else if (user.RotBody == 6) {
                                newX = -1;
                            }
                            else if (user.RotBody == 2) {
                                newX = 1;
                            }

                            user.MoveTo(user.X + newX, user.Y + newY);
                            RequestUpdate(1, false);
                        }
                        else if (user != null && (user.Coordinate == SquareBehind || user.Coordinate == SquareInFront)) {
                            user.UnlockWalking();

                            if (GateTransitionService.For(this) != null) {
                                InteractingUser = 0;
                                CloseAutomatically(1);
                            }
                            else {
                                LegacyDataString = "0";
                                InteractingUser = 0;
                                UpdateState(false, true);
                            }
                        }
                        else if (LegacyDataString == "1") {
                            if (GateTransitionService.For(this) != null) {
                                CloseAutomatically(1);
                            }
                            else {
                                LegacyDataString = "0";
                                UpdateState(false, true);
                            }
                        }

                        if (user == null) {
                            InteractingUser = 0;
                        }

                        break;
                    case InteractionType.Hopper: {
                            user = null;
                            user2 = null;
                            var showHopperEffect = false;
                            var keepDoorOpen = false;
                            var pause = 0;

                            // Do we have a primary user that wants to go somewhere?
                            if (InteractingUser > 0) {
                                user = room.GetRoomUserManager().GetRoomUserByHabbo(InteractingUser);

                                // Is this user okay?
                                if (user != null) {
                                    // Is he in the tele?
                                    if (user.Coordinate == Coordinate) {
                                        //Remove the user from the square
                                        user.AllowOverride = false;

                                        if (user.TeleDelay == 0) {
                                            var roomHopId = TravelStore().FindOtherHopperRoom(user.RoomId);
                                            var nextHopperId = TravelStore().FindHopper(roomHopId);

                                            if (!user.IsBot && user.GetClient() is { } hoppingClient &&
                                                hoppingClient.GetHabbo() != null) {
                                                hoppingClient.GetHabbo().IsHopping = true;
                                                hoppingClient.GetHabbo().HopperId = nextHopperId;
                                                room.GetGameMap().Navigation!.Remove(user);

                                                hoppingClient.GetHabbo().PrepareRoom(roomHopId, "");
                                                //User.GetClient().SendMessage(new RoomForwardComposer(RoomHopId));
                                                InteractingUser = 0;
                                            }
                                        }
                                        else {
                                            user.TeleDelay--;
                                            showHopperEffect = true;
                                        }
                                    }
                                    // Is he in front of the tele?
                                    else if (user.Coordinate == SquareInFront) {
                                        user.AllowOverride = false;

                                        keepDoorOpen = true;

                                        // Lock his walking. We're taking control over him. Allow overriding so he can get in the tele.
                                        if (user.IsWalking && (user.GoalX != GetX || user.GoalY != GetY)) {
                                            user.ClearMovement(true);
                                        }

                                        user.CanWalk = false;
                                        user.AllowOverride = false;
                                        user.RequestInteractionStep(room, Coordinate);
                                    }
                                    // Not even near, do nothing and move on for the next user.
                                    else {
                                        InteractingUser = 0;
                                    }
                                }
                                else {
                                    // Invalid user, do nothing and move on for the next user.
                                    InteractingUser = 0;
                                }
                            }

                            if (InteractingUser2 > 0) {
                                user2 = room.GetRoomUserManager().GetRoomUserByHabbo(InteractingUser2);

                                // Is this user okay?
                                if (user2 != null) {
                                    // If so, open the door, unlock the user's walking, and try to push him out in the right direction. We're done with him!
                                    keepDoorOpen = true;
                                    user2.UnlockWalking();
                                    user2.RequestInteractionStep(room, SquareInFront);
                                }

                                // This is a one time thing, whether the user's valid or not.
                                InteractingUser2 = 0;
                            }

                            // Set the new item state, by priority
                            if (keepDoorOpen) {
                                if (LegacyDataString != "1") {
                                    LegacyDataString = "1";
                                    UpdateState(false, true);
                                }
                            }
                            else if (showHopperEffect) {
                                if (LegacyDataString != "2") {
                                    LegacyDataString = "2";
                                    UpdateState(false, true);
                                }
                            }
                            else {
                                if (LegacyDataString != "0") {
                                    if (pause == 0) {
                                        LegacyDataString = "0";
                                        UpdateState(false, true);
                                        pause = 2;
                                    }
                                    else {
                                        pause--;
                                    }
                                }
                            }

                            // We're constantly going!
                            RequestUpdate(1, false);
                            break;
                        }
                    case InteractionType.Teleport: {
                            user = null;
                            user2 = null;
                            var keepDoorOpen = false;
                            var showTeleEffect = false;

                            // Do we have a primary user that wants to go somewhere?
                            if (InteractingUser > 0) {
                                user = room.GetRoomUserManager().GetRoomUserByHabbo(InteractingUser);

                                // Is this user okay?
                                if (user != null) {
                                    // Is he in the tele?
                                    if (user.Coordinate == Coordinate) {
                                        //Remove the user from the square
                                        user.AllowOverride = false;

                                        if (ItemTeleporterFinder.IsTeleLinked(Id, room, TravelStore())) {
                                            showTeleEffect = true;
                                            // Woop! No more delay.
                                            var teleId = TravelStore().FindLinkedTeleporter(Id);
                                            var roomId = ItemTeleporterFinder.GetTeleRoomId(teleId, room, TravelStore());

                                            // Do we need to tele to the same room or gtf to another?
                                            if (roomId == RoomId) {
                                                var item = room.GetRoomItemHandler().GetItem(teleId);

                                                if (item == null) {
                                                    user.UnlockWalking();
                                                }
                                                else {
                                                    // Set pos
                                                    user.SetPos(item.GetX, item.GetY, item.GetZ);
                                                    user.SetRot(item.Rotation, false);

                                                    if (!user.IsBot) {
                                                        _rewards!.Progress(user.GetClient(), RewardTrackActions.Teleport);
                                                    }

                                                    // Force tele effect update (dirty)
                                                    item.LegacyDataString = "2";
                                                    item.UpdateState(false, true);

                                                    // Set secondary interacting user
                                                    item.InteractingUser2 = InteractingUser;
                                                    InteractingUser = 0;
                                                }
                                            }
                                            else {
                                                if (user.TeleDelay == 0) {
                                                    // Let's run the teleport delegate to take futher care of this.. WHY DARIO?!
                                                    if (!user.IsBot && user.GetClient() is { } teleportClient &&
                                                        teleportClient.GetHabbo() != null) {
                                                        teleportClient.GetHabbo().IsTeleporting = true;
                                                        teleportClient.GetHabbo().TeleportingRoomId = roomId;
                                                        teleportClient.GetHabbo().TeleporterId = teleId;
                                                        room.GetGameMap().Navigation!.Remove(user);

                                                        teleportClient.GetHabbo().PrepareRoom(roomId, "");
                                                        //User.GetClient().SendMessage(new RoomForwardComposer(RoomId));
                                                        InteractingUser = 0;
                                                    }
                                                }
                                                else {
                                                    user.TeleDelay--;
                                                    showTeleEffect = true;
                                                }
                                                //PlusEnvironment.GetGame().GetRoomManager().AddTeleAction(new TeleUserData(User.GetClient().GetMessageHandler(), User.GetClient().GetHabbo(), RoomId, TeleId));
                                            }

                                            room.GetGameMap().GenerateMaps();
                                        }
                                        else {
                                            // This tele is not linked, so let's gtfo.
                                            user.UnlockWalking();
                                            InteractingUser = 0;
                                        }
                                    }
                                    // Is he in front of the tele?
                                    else if (user.Coordinate == SquareInFront) {
                                        user.AllowOverride = false;

                                        // Open the door
                                        keepDoorOpen = true;

                                        // Lock his walking. We're taking control over him. Allow overriding so he can get in the tele.
                                        if (user.IsWalking && (user.GoalX != GetX || user.GoalY != GetY)) {
                                            user.ClearMovement(true);
                                        }

                                        user.CanWalk = false;
                                        user.AllowOverride = false;
                                        user.RequestInteractionStep(room, Coordinate);
                                    }
                                    // Not even near, do nothing and move on for the next user.
                                    else {
                                        InteractingUser = 0;
                                    }
                                }
                                else {
                                    // Invalid user, do nothing and move on for the next user.
                                    InteractingUser = 0;
                                }
                            }

                            // Do we have a secondary user that wants to get out of the tele?
                            if (InteractingUser2 > 0) {
                                user2 = room.GetRoomUserManager().GetRoomUserByHabbo(InteractingUser2);

                                // Is this user okay?
                                if (user2 != null) {
                                    // If so, open the door, unlock the user's walking, and try to push him out in the right direction. We're done with him!
                                    keepDoorOpen = true;
                                    user2.UnlockWalking();
                                    user2.RequestInteractionStep(room, SquareInFront);
                                }

                                // This is a one time thing, whether the user's valid or not.
                                InteractingUser2 = 0;
                            }

                            // Set the new item state, by priority
                            if (showTeleEffect) {
                                if (LegacyDataString != "2") {
                                    LegacyDataString = "2";
                                    UpdateState(false, true);
                                }
                            }
                            else if (keepDoorOpen) {
                                if (LegacyDataString != "1") {
                                    LegacyDataString = "1";
                                    UpdateState(false, true);
                                }
                            }
                            else {
                                if (LegacyDataString != "0") {
                                    LegacyDataString = "0";
                                    UpdateState(false, true);
                                }
                            }

                            // We're constantly going!
                            RequestUpdate(1, false);
                            break;
                        }
                    case InteractionType.Bottle:
                        LegacyDataString = Random.Shared.Next(0, 8).ToString();
                        UpdateState();
                        break;
                    case InteractionType.Dice: {
                            var numbers = new[] { "1", "2", "3", "4", "5", "6" };

                            if (LegacyDataString == "-1") {
                                LegacyDataString = RandomizeStrings(numbers)[0];
                            }

                            UpdateState();
                        }
                        break;
                    case InteractionType.HabboWheel:
                        // AIR WIN63-202610151745-318700621 FurnitureHabboWheelVisualization.as:37 stops on results 1 to 10.
                        LegacyDataString = Random.Shared.Next(1, 11).ToString();
                        UpdateState();
                        break;
                    case InteractionType.LoveShuffler:
                        if (LegacyDataString == "0") {
                            LegacyDataString = Random.Shared.Next(1, 5).ToString();
                            RequestUpdate(20, false);
                        }
                        else if (LegacyDataString != "-1") {
                            LegacyDataString = "-1";
                        }

                        UpdateState(false, true);
                        break;
                    case InteractionType.Alert:
                        if (LegacyDataString == "1") {
                            LegacyDataString = "0";
                            UpdateState(false, true);
                        }

                        break;
                    case InteractionType.VendingMachine:
                        if (LegacyDataString == "1") {
                            user = room.GetRoomUserManager().GetRoomUserByHabbo(InteractingUser);

                            if (user == null) {
                                break;
                            }

                            user.UnlockWalking();

                            if (Definition.VendingIds.Count > 0) {
                                var randomDrink = Definition.VendingIds[Random.Shared.Next(0, Definition.VendingIds.Count)];
                                user.CarryItem(randomDrink);
                            }

                            InteractingUser = 0;
                            LegacyDataString = "0";
                            UpdateState(false, true);
                        }

                        break;
                    case InteractionType.Scoreboard: {
                            if (string.IsNullOrEmpty(LegacyDataString)) {
                                break;
                            }

                            var seconds = 0;

                            try {
                                seconds = int.Parse(LegacyDataString);
                            }
                            catch { }

                            if (seconds > 0) {
                                if (InteractionCountHelper == 1) {
                                    seconds--;
                                    InteractionCountHelper = 0;
                                    LegacyDataString = seconds.ToString();
                                    UpdateState();
                                }
                                else {
                                    InteractionCountHelper++;
                                }

                                UpdateCounter = 1;
                            }
                            else {
                                UpdateCounter = 0;
                            }

                            break;
                        }
                    case InteractionType.Banzaicounter: {
                            if (WiredCounterController.Recognizes(this)) {
                                break;
                            }

                            if (string.IsNullOrEmpty(LegacyDataString)) {
                                break;
                            }

                            var seconds = 0;

                            try {
                                seconds = int.Parse(LegacyDataString);
                            }
                            catch { }

                            if (seconds > 0) {
                                if (InteractionCountHelper == 1) {
                                    seconds--;
                                    InteractionCountHelper = 0;

                                    if (room.GetBanzai().IsBanzaiActive) {
                                        LegacyDataString = seconds.ToString();
                                        UpdateState();
                                    }
                                    else {
                                        break;
                                    }
                                }
                                else {
                                    InteractionCountHelper++;
                                }

                                UpdateCounter = 1;
                            }
                            else {
                                UpdateCounter = 0;
                                room.GetBanzai().BanzaiEnd();
                            }

                            break;
                        }
                    case InteractionType.Banzaitele: {
                            LegacyDataString = string.Empty;
                            UpdateState();
                            break;
                        }
                    case InteractionType.Banzaifloor: {
                            if (Value == 3) {
                                if (InteractionCountHelper == 1) {
                                    InteractionCountHelper = 0;

                                    switch (Team) {
                                        case Team.Blue: {
                                                LegacyDataString = "11";
                                                break;
                                            }
                                        case Team.Green: {
                                                LegacyDataString = "8";
                                                break;
                                            }
                                        case Team.Red: {
                                                LegacyDataString = "5";
                                                break;
                                            }
                                        case Team.Yellow: {
                                                LegacyDataString = "14";
                                                break;
                                            }
                                    }
                                }
                                else {
                                    LegacyDataString = "";
                                    InteractionCountHelper++;
                                }

                                UpdateState();
                                InteractionCount++;

                                if (InteractionCount < 16) {
                                    UpdateCounter = 1;
                                }
                                else {
                                    UpdateCounter = 0;
                                }
                            }

                            break;
                        }
                    case InteractionType.Banzaipuck: {
                            if (InteractionCount > 4) {
                                InteractionCount++;
                                UpdateCounter = 1;
                            }
                            else {
                                InteractionCount = 0;
                                UpdateCounter = 0;
                            }

                            break;
                        }
                    case InteractionType.FreezeTile: {
                            if (InteractingUser > 0) {
                                LegacyDataString = "11000";
                                UpdateState(false, true);
                                room.GetFreeze().OnFreezeTiles(this, FreezePowerUp);
                                InteractingUser = 0;
                                InteractionCountHelper = 0;
                            }

                            break;
                        }
                    case InteractionType.Counter: {
                            if (WiredCounterController.Recognizes(this)) {
                                break;
                            }

                            if (string.IsNullOrEmpty(LegacyDataString)) {
                                break;
                            }

                            var seconds = 0;

                            try {
                                seconds = int.Parse(LegacyDataString);
                            }
                            catch { }

                            if (seconds > 0) {
                                if (InteractionCountHelper == 1) {
                                    seconds--;
                                    InteractionCountHelper = 0;

                                    if (room.GetSoccer().GameIsStarted) {
                                        LegacyDataString = seconds.ToString();
                                        UpdateState();
                                    }
                                    else {
                                        break;
                                    }
                                }
                                else {
                                    InteractionCountHelper++;
                                }

                                UpdateCounter = 1;
                            }
                            else {
                                UpdateNeeded = false;
                                room.GetSoccer().StopGame();
                            }

                            break;
                        }
                    case InteractionType.Freezetimer: {
                            if (WiredCounterController.Recognizes(this)) {
                                break;
                            }

                            if (string.IsNullOrEmpty(LegacyDataString)) {
                                break;
                            }

                            var seconds = 0;

                            try {
                                seconds = int.Parse(LegacyDataString);
                            }
                            catch { }

                            if (seconds > 0) {
                                if (InteractionCountHelper == 1) {
                                    seconds--;
                                    InteractionCountHelper = 0;

                                    if (room.GetFreeze().GameIsStarted) {
                                        LegacyDataString = seconds.ToString();
                                        UpdateState();
                                    }
                                    else {
                                        break;
                                    }
                                }
                                else {
                                    InteractionCountHelper++;
                                }

                                UpdateCounter = 1;
                            }
                            else {
                                UpdateNeeded = false;
                                room.GetFreeze().StopGame();
                            }

                            break;
                        }
                    case InteractionType.PressurePad: {
                            LegacyDataString = "1";
                            UpdateState();
                            break;
                        }
                    case InteractionType.WiredSelector:
                    case InteractionType.WiredAddon:
                    case InteractionType.WiredVariable:
                    case InteractionType.WiredEffect:
                    case InteractionType.WiredTrigger:
                    case InteractionType.WiredCondition: {
                            if (LegacyDataString == "1") {
                                LegacyDataString = "0";
                                UpdateState(false, true);
                            }
                        }
                        break;
                    case InteractionType.Cannon: {
                            if (LegacyDataString != "1") {
                                break;
                            }

                            var targetStart = Coordinate;
                            var targetSquares = new List<Point>();

                            switch (Rotation) {
                                case 0: {
                                        targetStart = new(GetX - 1, GetY);

                                        if (!targetSquares.Contains(targetStart)) {
                                            targetSquares.Add(targetStart);
                                        }

                                        for (var I = 1; I <= 3; I++) {
                                            var targetSquare = new Point(targetStart.X - I, targetStart.Y);

                                            if (!targetSquares.Contains(targetSquare)) {
                                                targetSquares.Add(targetSquare);
                                            }
                                        }

                                        break;
                                    }
                                case 2: {
                                        targetStart = new(GetX, GetY - 1);

                                        if (!targetSquares.Contains(targetStart)) {
                                            targetSquares.Add(targetStart);
                                        }

                                        for (var I = 1; I <= 3; I++) {
                                            var targetSquare = new Point(targetStart.X, targetStart.Y - I);

                                            if (!targetSquares.Contains(targetSquare)) {
                                                targetSquares.Add(targetSquare);
                                            }
                                        }

                                        break;
                                    }
                                case 4: {
                                        targetStart = new(GetX + 2, GetY);

                                        if (!targetSquares.Contains(targetStart)) {
                                            targetSquares.Add(targetStart);
                                        }

                                        for (var I = 1; I <= 3; I++) {
                                            var targetSquare = new Point(targetStart.X + I, targetStart.Y);

                                            if (!targetSquares.Contains(targetSquare)) {
                                                targetSquares.Add(targetSquare);
                                            }
                                        }

                                        break;
                                    }
                                case 6: {
                                        targetStart = new(GetX, GetY + 2);

                                        if (!targetSquares.Contains(targetStart)) {
                                            targetSquares.Add(targetStart);
                                        }

                                        for (var I = 1; I <= 3; I++) {
                                            var targetSquare = new Point(targetStart.X, targetStart.Y + I);

                                            if (!targetSquares.Contains(targetSquare)) {
                                                targetSquares.Add(targetSquare);
                                            }
                                        }

                                        break;
                                    }
                            }

                            if (targetSquares.Count > 0) {
                                foreach (var square in targetSquares.ToList()) {
                                    var affectedUsers = room.GetGameMap().GetRoomUsers(square).ToList();

                                    if (affectedUsers == null || affectedUsers.Count == 0) {
                                        continue;
                                    }

                                    foreach (var target in affectedUsers) {
                                        if (target == null || target.IsBot || target.IsPet) {
                                            continue;
                                        }

                                        var targetClient = target.GetClient();

                                        if (targetClient == null || targetClient.GetHabbo() == null) {
                                            continue;
                                        }

                                        if (room.CheckRights(targetClient, true)) {
                                            continue;
                                        }

                                        target.ApplyEffect(4);
                                        targetClient.Send(new RoomNotificationComposer("Kicked from room", "You were hit by a cannonball!", "room_kick_cannonball", ""));
                                        target.ApplyEffect(0);
                                        room.GetRoomUserManager().RemoveUserFromRoom(targetClient, true);
                                    }
                                }
                            }

                            LegacyDataString = "2";
                            UpdateState(false, true);
                        }
                        break;
                }
            }
        }
        catch (Exception e) {
            ExceptionLogger.LogException(e);
        }
    }

    public static string[] RandomizeStrings(string[] arr)
    {
        var list = new List<KeyValuePair<int, string>>();

        // Add all strings from array
        // Add new random int each time
        foreach (var s in arr) {
            list.Add(new(Random.Shared.Next(), s));
        }

        // Sort the list by the random number
        var sorted = from item in list
                     orderby item.Key
                     select item;
        // Allocate new string array
        var result = new string[arr.Length];
        // Copy values to array
        var index = 0;

        foreach (var pair in sorted) {
            result[index] = pair.Value;
            index++;
        }

        // Return copied array
        return result;
    }

    public void RequestUpdate(int cycles, bool setUpdate)
    {
        UpdateCounter = cycles;

        if (setUpdate) {
            UpdateNeeded = true;
        }
    }

    /// <summary>When a user last switched this room flag furni (see InteractorConfigurationSwitch).</summary>
    internal DateTimeOffset? LastSwitchUse { get; set; }

    public void UpdateState()
    {
        UpdateState(true, true);
    }

    /// <summary>
    /// A state a user switched by using the furni. Observed on hhus (AIR WIN63-202610131318-860371703): using a wf_colortile sends one
    /// ObjectDataUpdate (374) with the new state, not a full ObjectUpdate. Furni whose height depends on the state keep the full update.
    /// </summary>
    public void UpdateUsedState()
    {
        var room = GetRoom();

        if (room == null || !IsFloorItem || Definition.AdjustableHeights.Count > 1) {
            UpdateState();

            return;
        }

        UpdateState(true, false);
        room.SendObjectData(this, LegacyDataString, false);
        room.SendPacket(new FurnitureMetadataComposer([RoomItemSnapshot.Capture(this)]));
    }

    public void UpdateState(bool inDb, bool inRoom)
    {
        var room = GetRoom();

        if (room == null) {
            return;
        }

        MagicTileHeight.Sync(this);
        PublishIfAttached(true);
        MarkInteractionStateChanged();
        room.GetGameMap()?.Navigation?.ItemStateChanged(Id);

        if (inDb) {
            room.GetRoomItemHandler().UpdateItem(this);
        }

        if (IsFloorItem) {
            room.GetGameMap()?.NotifyPlacementState(this);
        }

        if (inRoom) {
            if (IsFloorItem) {
                var furnitureSnapshot = RoomItemSnapshot.Capture(this);
                room.SendPacket(new ObjectUpdateComposer(furnitureSnapshot));
                room.SendPacket(new FurnitureMetadataComposer([furnitureSnapshot]));
            }
            else {
                var furnitureSnapshot = RoomItemSnapshot.Capture(this);
                room.SendPacket(new ItemUpdateComposer(furnitureSnapshot));
                room.SendPacket(new FurnitureMetadataComposer([furnitureSnapshot]));
            }
        }
    }

    internal void Attach(Room room, IItemInteractorFactory interactors, IItemTravelStore travelStore, IRewardTrackManager rewards)
    {
        if (RoomId != room.RoomId) {
            throw new InvalidOperationException("Item room id does not match the admitting room.");
        }

        if (_room != null && !ReferenceEquals(_room, room)) {
            throw new InvalidOperationException("Item is already attached to another room.");
        }

        _room = room;
        Interlocked.Increment(ref _placement);
        _interactionClock = room.InteractionClock;
        _interactors = interactors;
        _travelStore = travelStore;
        _rewards = rewards;

        // Teleports and hoppers run their door state machine every cycle while placed.
        if (Definition.InteractionType is InteractionType.Teleport or InteractionType.Hopper && !UpdateNeeded && UpdateCounter <= 0) {
            RequestUpdate(1, true);
        }
    }

    internal void Detach(Room room)
    {
        if (!ReferenceEquals(_room, room)) {
            throw new InvalidOperationException("Item cannot be detached from a room that does not own it.");
        }

        _room = null;
        Interlocked.Increment(ref _placement);
        _interactionClock = null;
        _interactors = null;
        _travelStore = null;
        _rewards = null;
    }

    public Room? GetRoom() => _room;

    private IItemTravelStore TravelStore() => _travelStore
        ?? throw new InvalidOperationException("Item must be attached to a room before using travel behavior.");

    public void UserFurniCollision(RoomUser? user)
    {
        var room = GetRoom();
        var client = user?.GetClient();

        if (room == null || user == null || client == null || client.GetHabbo() == null) {
            return;
        }

        room.GetWired().TriggerEvent(WiredBoxType.TriggerUserFurniCollision, client.GetHabbo(), this);
    }

    public void UserWalksOnFurni(RoomUser? user) => UserWalksOnFurni(user, true);

    /// <summary>
    /// The user arrives on this furni. A room cycle step passes wired: false: it raised the Wired walk-on event when it announced
    /// the step (<see cref="WiredWalksOnFurni"/>).
    /// </summary>
    internal void UserWalksOnFurni(RoomUser? user, bool wired)
    {
        var room = GetRoom();

        if (room == null || user == null) {
            return;
        }

        var habbo = user.GetClient()?.GetHabbo();

        // Bots and pets can trigger modern walk stacks; legacy boxes and tents still need a Habbo.
        if (habbo == null) {
            if (wired) {
                WiredWalksOnFurni(user);
            }

            return;
        }

        if (Definition.InteractionType == InteractionType.Tent || Definition.InteractionType == InteractionType.TentSmall) {
            room.AddUserToTent(Id, user);
        }

        if (wired) {
            WiredWalksOnFurni(user);
        }

        user.LastItem = this;
    }

    public void UserWalksOffFurni(RoomUser? user) => UserWalksOffFurni(user, true);

    /// <summary>The user leaves this furni; see <see cref="UserWalksOnFurni(RoomUser?, bool)"/>.</summary>
    internal void UserWalksOffFurni(RoomUser? user, bool wired)
    {
        var room = GetRoom();

        if (room == null || user == null) {
            return;
        }

        var habbo = user.GetClient()?.GetHabbo();

        if (habbo == null) {
            if (wired) {
                WiredWalksOffFurni(user);
            }

            return;
        }

        if (Definition.InteractionType == InteractionType.Tent || Definition.InteractionType == InteractionType.TentSmall) {
            room.RemoveUserFromTent(Id, user);
        }

        if (wired) {
            WiredWalksOffFurni(user);
        }
    }

    /// <summary>
    /// Raises only the Wired walk-on event. Observed on hhus (AIR WIN63-202610131318-860371703): a walk-on stack's flash and message go
    /// out in the same tick as the UserUpdate that announces the step onto the furni (mv towards it), one walk tick before the arrival.
    /// </summary>
    internal void WiredWalksOnFurni(RoomUser user) => WiredWalk(user, WiredEventKind.WalkOn, WiredBoxType.TriggerWalkOnFurni);

    /// <summary>Raises only the Wired walk-off event, in the tick that announces the step off the furni.</summary>
    internal void WiredWalksOffFurni(RoomUser user) => WiredWalk(user, WiredEventKind.WalkOff, WiredBoxType.TriggerWalkOffFurni);

    private void WiredWalk(RoomUser user, WiredEventKind kind, WiredBoxType type)
    {
        if (GetRoom() is not { } room) {
            return;
        }

        if (user.GetClient()?.GetHabbo() is { } habbo) {
            room.GetWired().TriggerEvent(type, habbo, this);
        }
        else if (user.IsBot) {
            room.GetWired().Dispatch(new(kind) { Actor = user, EventItem = this });
        }
    }

    public void Destroy()
    {
        NavigationInputs?.Remove(this);

        if (_room is { } room) {
            Detach(room);
        }

        Definition = null;
    }
}
