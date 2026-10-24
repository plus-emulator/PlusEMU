using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Achievements;
using Microsoft.Extensions.Logging;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.Core;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Data.Moodlight;
using Plus.HabboHotel.Items.Data.Toner;
using Plus.HabboHotel.Rooms.Games;
using Plus.HabboHotel.Rooms.Games.Banzai;
using Plus.HabboHotel.Rooms.Games.Football;
using Plus.HabboHotel.Rooms.Games.Freeze;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms;

public class Room
{
    private readonly IReadOnlyList<IRoomComponent> _components;
    private int _initiated;
    private RoomData? _data;
    private BansComponent _bansComponent;
    private FilterComponent _filterComponent;
    private Dictionary<uint, List<RoomUser>> _tents;
    private TradingComponent _tradingComponent;
    private WiredComponent _wiredComponent;
    private BattleBanzai _banzai;
    private Freeze _freeze;
    private GameItemHandler _gameItemHandler;
    private GameManager _gameManager;

    private Gamemap _gamemap;
    private RoomItemHandling _roomItemHandling;

    private RoomUserManager _roomUserManager;
    private TimeProvider? _interactionClock;
    private IRoomUserSnapshotService _userSnapshots;
    private Soccer _soccer;
    private IceTagGame? _iceTag;

    public bool IsCrashed;
    public DateTimeOffset? LastRegenerationAt;
    // The room's Wired timer, read by the elapsed-time conditions and @wired_timer. It starts when the room loads.
    public DateTimeOffset LastTimerResetAt;
    private DateTimeOffset? _timerRestartAt;
    public bool MDisposed;
    public MoodlightData MoodlightData;

    public Dictionary<int, DateTimeOffset> MutedUsers;

    public Task ProcessTask;

    // RoomCycle's task slot: 0 idle, else the kind of pass holding it.
    internal int CycleSlot;
    private object? _navigationSync;
    internal object NavigationSync => LazyInitializer.EnsureInitialized(ref _navigationSync);
    public bool RoomMuted;

    public TeamManager Teambanzai;
    public TeamManager Teamfreeze;

    public TonerData TonerData;

    public List<int> UsersWithRights;

    private readonly ILogger<RoomNavigation> _navigationLogger;
    private readonly ILogger _wiredLogger;
    private readonly IAchievementManager _achievements;
    private readonly IRoomManager _rooms;
    private readonly IGroupManager? _groups;

    public Room(RoomData data, IEnumerable<IRoomComponent> components, ILogger<RoomNavigation> navigationLogger, ILogger wiredLogger, IAchievementManager achievements, IRoomManager rooms,
        IGroupManager? groups = null)
    {
        _data = data;
        _components = components.OrderBy(component => component.Order).ToArray();
        _navigationLogger = navigationLogger;
        _wiredLogger = wiredLogger;
        _achievements = achievements;
        _rooms = rooms;
        _groups = groups;
    }


    // Guild furniture shows its group's badge and colours; null without the group (or a group manager, in tests).
    internal GuildFurnitureAppearance? FindGuildAppearance(int groupId) =>
        groupId > 0 && _groups != null && _groups.TryGetGroup(groupId, out var group)
            ? new(group.Id, group.Badge, _groups.GetColourCode(group.Colour1, true), _groups.GetColourCode(group.Colour2, false))
            : null;

    /// <summary>The hotel's achievements, for wired boxes that progress them.</summary>
    internal IAchievementManager Achievements => _achievements;

    /// <summary>The loaded rooms, for wired link deliveries to other rooms.</summary>
    internal IRoomManager Rooms => _rooms;

    public RoomData Data => _data ??= new RoomData();
    public static implicit operator RoomData(Room room) => room.Data;
    internal IReadOnlyList<IRoomComponent> Components => _components;

    public void Initiate()
    {
        if (Interlocked.Exchange(ref _initiated, 1) != 0) {
            throw new InvalidOperationException($"Room {Data.Id} has already been initiated.");
        }

        foreach (var component in _components) {
            component.Initiate(this);
        }

        foreach (var component in _components) {
            component.Initiated();
        }

        // Room Initialized (wf_trg_room_initializes) fires once the furniture, bots and pets are all loaded. A stack that fails must
        // not stop the room from loading.
        try {
            GetWired().RoomInitialized();
        }
        catch (Exception error) {
            _wiredLogger?.LogError(error, "Room Initialized failed while loading room {RoomId}.", Data.Id);
        }
    }

    internal void SetRuntime(Gamemap gamemap, RoomItemHandling items, RoomUserManager users,
        WiredComponent wired, IRoomUserSnapshotService userSnapshots, TimeProvider interactionClock)
    {
        IsLagging = 0;
        Unloaded = false;
        IdleTime = 0;
        RoomMuted = false;
        MutedUsers = new();
        _tents = new();
        _gamemap = gamemap;
        _roomItemHandling = items;
        _roomUserManager = users;
        _wiredComponent = wired;
        _userSnapshots = userSnapshots;
        _interactionClock = interactionClock;
        var loadedAt = interactionClock.GetUtcNow();
        LastRegenerationAt = loadedAt;
        LastTimerResetAt = loadedAt;
    }

    /// <summary>
    /// Reset Timers at <paramref name="now"/> restarts the room timer at <paramref name="tickAt"/>, the next 500 ms room tick; until then the timer keeps counting.
    /// A Less Than check 150 or 300 ms after the reset still sees the old timer, and one 450 ms or more after it sees the new one.
    /// </summary>
    internal void RestartTimerAt(DateTimeOffset now, DateTimeOffset tickAt)
    {
        TimerElapsedMilliseconds(now);
        // A restart still pending is for the same tick, so a second reset before that tick changes nothing.
        _timerRestartAt ??= tickAt;
    }

    internal long TimerElapsedMilliseconds(DateTimeOffset now)
    {
        if (_timerRestartAt is { } restartAt && now >= restartAt) {
            RestartTimer(restartAt);
            _timerRestartAt = null;
        }

        return Math.Max(0, (now - LastTimerResetAt).Ticks / TimeSpan.TicksPerMillisecond);
    }

    // A game stop restarts the timer at once, so a pending restart only counts when it comes later.
    private void RestartTimer(DateTimeOffset at) => LastTimerResetAt = at > LastTimerResetAt ? at : LastTimerResetAt;

    private Music.RoomMusicComponent? _music;
    public Music.RoomMusicComponent Music => _music ?? throw new InvalidOperationException("The room music component has not been initialized.");
    internal void SetMusic(Music.RoomMusicComponent music) => _music = music;

    internal void SetBans(BansComponent bans) => _bansComponent = bans;
    internal void SetFilter(FilterComponent filter) => _filterComponent = filter;
    internal void SetTrading(TradingComponent trading) => _tradingComponent = trading;

    internal ILogger<RoomNavigation> NavigationLogger => _navigationLogger;
    internal ILogger WiredLogger => _wiredLogger;
    internal TimeProvider InteractionClock => _interactionClock
        ?? throw new InvalidOperationException("The room interaction clock has not been initialized.");
    internal TimeProvider RuntimeClock => InteractionClock;

    public uint Id { get => Data.Id; set => Data.Id = value; }
    public string Name { get => Data.Name; set => Data.Name = value; }
    public string ModelName { get => Data.ModelName; set => Data.ModelName = value; }
    public string OwnerName { get => Data.OwnerName; set => Data.OwnerName = value; }
    public int OwnerId { get => Data.OwnerId; set => Data.OwnerId = value; }
    public string Password { get => Data.Password; set => Data.Password = value; }
    public int Score { get => Data.Score; set => Data.Score = value; }
    public RoomAccess Access { get => Data.Access; set => Data.Access = value; }
    public string Type { get => Data.Type; set => Data.Type = value; }
    public int UsersMax { get => Data.UsersMax; set => Data.UsersMax = value; }
    public int UsersNow { get => Data.UsersNow; set => Data.UsersNow = value; }
    public int Category { get => Data.Category; set => Data.Category = value; }
    public string Description { get => Data.Description; set => Data.Description = value; }
    public List<string> Tags => Data.Tags;
    public string Floor { get => Data.Floor; set => Data.Floor = value; }
    public string Landscape { get => Data.Landscape; set => Data.Landscape = value; }
    public bool AllowPets { get => Data.AllowPets; set => Data.AllowPets = value; }
    public bool AllowPetsEating { get => Data.AllowPetsEating; set => Data.AllowPetsEating = value; }
    public bool RoomBlockingEnabled { get => Data.RoomBlockingEnabled; set => Data.RoomBlockingEnabled = value; }
    public bool Hidewall { get => Data.Hidewall; set => Data.Hidewall = value; }
    public int WallThickness { get => Data.WallThickness; set => Data.WallThickness = value; }
    public int FloorThickness { get => Data.FloorThickness; set => Data.FloorThickness = value; }
    public string Wallpaper { get => Data.Wallpaper; set => Data.Wallpaper = value; }
    public int WhoCanMute { get => Data.WhoCanMute; set => Data.WhoCanMute = value; }
    public int WhoCanBan { get => Data.WhoCanBan; set => Data.WhoCanBan = value; }
    public int WhoCanKick { get => Data.WhoCanKick; set => Data.WhoCanKick = value; }
    public int ChatMode { get => Data.ChatMode; set => Data.ChatMode = value; }
    public int ChatSize { get => Data.ChatSize; set => Data.ChatSize = value; }
    public int ChatSpeed { get => Data.ChatSpeed; set => Data.ChatSpeed = value; }
    public int ExtraFlood { get => Data.ExtraFlood; set => Data.ExtraFlood = value; }
    public int ChatDistance { get => Data.ChatDistance; set => Data.ChatDistance = value; }
    public int TradeSettings { get => Data.TradeSettings; set => Data.TradeSettings = value; }
    public bool PushEnabled { get => Data.PushEnabled; set => Data.PushEnabled = value; }
    public bool PullEnabled { get => Data.PullEnabled; set => Data.PullEnabled = value; }
    public bool SuperPushEnabled { get => Data.SuperPushEnabled; set => Data.SuperPushEnabled = value; }
    public bool SuperPullEnabled { get => Data.SuperPullEnabled; set => Data.SuperPullEnabled = value; }
    public bool EnablesEnabled { get => Data.EnablesEnabled; set => Data.EnablesEnabled = value; }
    public bool RespectNotificationsEnabled { get => Data.RespectNotificationsEnabled; set => Data.RespectNotificationsEnabled = value; }
    public bool PetMorphsAllowed { get => Data.PetMorphsAllowed; set => Data.PetMorphsAllowed = value; }
    public int SalePrice { get => Data.SalePrice; set => Data.SalePrice = value; }
    public bool ReverseRollers { get => Data.ReverseRollers; set => Data.ReverseRollers = value; }
    public bool LayEnabled { get => Data.LayEnabled; set => Data.LayEnabled = value; }
    public bool HideWired { get => Data.HideWired; set => Data.HideWired = value; }
    public bool DiagonalSqueeze { get => Data.DiagonalSqueeze; set => Data.DiagonalSqueeze = value; }
    public RoomModel Model { get => Data.Model; set => Data.Model = value; }
    public RoomPromotion Promotion { get => Data.Promotion; set => Data.Promotion = value; }
    public Plus.HabboHotel.Groups.Group? Group { get => Data.Group; set => Data.Group = value; }
    public bool HasActivePromotion => Data.HasActivePromotion;
    public void EndPromotion() => Data.EndPromotion();

    public int IsLagging { get; set; }
    public bool Unloaded { get; set; }
    public int IdleTime { get; set; }

    public List<string> WordFilterList { get; set; }

    public int UserCount => _roomUserManager.GetRoomUsers().Count;

    public uint RoomId => Id;

    public bool CanTradeInRoom => true;

    public Gamemap GetGameMap() => _gamemap;
    internal IceTagGame GetIceTag() => LazyInitializer.EnsureInitialized(ref _iceTag, () => new(this, _achievements, InteractionClock));
    internal void IceTagLeave(RoomUser user) => _iceTag?.Leave(user);
    internal void IceTagAdmitted(Item item)
    {
        if (item.Definition.InteractionType == InteractionType.IceTagPole) {
            GetIceTag().RegisterPole(item);
        }
    }
    internal int UpdateIceTag(RoomUser user, Item? field) => field != null ? GetIceTag().Update(user, field) : _iceTag?.Update(user, null) ?? -1;
    internal void IceTagLookTo(RoomUser user, int x, int y) => _iceTag?.LookTo(user, x, y);

    public RoomItemHandling GetRoomItemHandler()
    {
        return _roomItemHandling;
    }

    public RoomUserManager GetRoomUserManager() => _roomUserManager;

    public Soccer GetSoccer()
    {
        if (_soccer == null) {
            _soccer = new(this);
        }

        return _soccer;
    }

    public TeamManager GetTeamManagerForBanzai()
    {
        if (Teambanzai == null) {
            Teambanzai = TeamManager.CreateTeam("banzai");
        }

        return Teambanzai;
    }

    public TeamManager GetTeamManagerForFreeze()
    {
        if (Teamfreeze == null) {
            Teamfreeze = TeamManager.CreateTeam("freeze");
        }

        return Teamfreeze;
    }

    public BattleBanzai GetBanzai()
    {
        if (_banzai == null) {
            _banzai = new(this, RuntimeClock, _achievements);
        }

        return _banzai;
    }

    public Freeze GetFreeze()
    {
        if (_freeze == null) {
            _freeze = new(this);
        }

        return _freeze;
    }

    public GameManager GetGameManager()
    {
        if (_gameManager == null) {
            _gameManager = new(this, RuntimeClock);
        }

        return _gameManager;
    }

    public GameItemHandler GetGameItemHandler()
    {
        if (_gameItemHandler == null) {
            _gameItemHandler = new(this);
        }

        return _gameItemHandler;
    }

    public bool GotSoccer() => _soccer != null;

    public bool GotBanzai() => _banzai != null;

    public bool GotFreeze() => _freeze != null;

    public void ClearTags()
    {
        Tags.Clear();
    }

    public void AddTagRange(List<string> tags)
    {
        Tags.AddRange(tags);
    }

    public FilterComponent GetFilter() => _filterComponent;

    public WiredComponent GetWired() => _wiredComponent;

    public BansComponent GetBans() => _bansComponent;

    public TradingComponent GetTrading() => _tradingComponent;

    public bool CheckRights(GameClient? session) => CheckRights(session, false);

    public bool CheckRights(GameClient? session, bool requireOwnership, bool checkForGroups = false)
    {
        try {
            if (session == null || session.GetHabbo() == null) {
                return false;
            }

            if (session.GetHabbo().Username == OwnerName && Type == "private") {
                return true;
            }

            if (session.GetHabbo().Access.Can(PermissionKeys.RoomOwnerAny)) {
                return true;
            }

            if (!requireOwnership && Type == "private") {
                if (session.GetHabbo().Access.Can(PermissionKeys.RoomRightsAny)) {
                    return true;
                }

                if (UsersWithRights.Contains(session.GetHabbo().Id)) {
                    return true;
                }
            }

            if (checkForGroups && Type == "private") {
                if (Group == null) {
                    return false;
                }

                if (Group.IsAdmin(session.GetHabbo().Id)) {
                    return true;
                }

                if (Group.AdminOnlyDeco == 0) {
                    if (Group.IsAdmin(session.GetHabbo().Id)) {
                        return true;
                    }
                }
            }
        }
        catch (Exception e) {
            ExceptionLogger.LogException(e);
        }

        return false;
    }

    public void OnUserShoot(RoomUser user, Item ball)
    {
        Func<Item, bool>? predicate = null;
        string? key = null;

        foreach (var item in GetRoomItemHandler().GetFurniObjects(ball.GetX, ball.GetY).ToList()) {
            if (item.Definition.ItemName.StartsWith("fball_goal_")) {
                key = item.Definition.ItemName.Split(new[] { '_' })[2];
                user.UnIdle();
                user.DanceId = 0;
                _achievements.ProgressAchievement(user.GetClient(), "ACH_FootballGoalScored", 1);
                SendPacket(new ActionComposer(user.VirtualId, 1));
            }
        }

        if (key != null) {
            if (predicate == null) {
                predicate = p => p.Definition.ItemName == $"fball_score_{key}";
            }

            foreach (var item2 in GetRoomItemHandler().GetFloor.Where(predicate).ToList()) {
                if (item2.Definition.ItemName == $"fball_score_{key}") {
                    if (!string.IsNullOrEmpty(item2.LegacyDataString)) {
                        item2.LegacyDataString = (Convert.ToInt32(item2.LegacyDataString) + 1).ToString();
                    }
                    else {
                        item2.LegacyDataString = "1";
                    }

                    item2.UpdateState();
                }
            }
        }
    }

    internal void ProcessWiredOnly()
    {
        try {
            lock (NavigationSync) {
                ProcessWiredOwned();
            }
        }
        finally {
            _music?.PublishPending();
        }

        return;
    }

    // Serialized with ProcessRoom (one process task at a time), so it owns the room just like a full tick.
    internal void RunFastPass(Action pass)
    {
        try {
            lock (NavigationSync) {
                RunOwnedPass(pass);
            }
        }
        finally {
            _music?.PublishPending();
        }

        return;
    }

    public void ProcessRoom()
    {
        try {
            lock (NavigationSync) {
                ProcessRoomOwned();
            }
        }
        finally {
            _music?.PublishPending();
        }

        return;
    }

    private void ProcessRoomOwned()
    {
        if (IsCrashed || MDisposed) {
            return;
        }

        using var owner = Plus.HabboHotel.Rooms.PathFinding.RoomOwnerScope.Enter(this);

        try {
            if (!KeepRoomActive()) {
                return;
            }

            RunRoomPhase(() => GetWired().OnRoomTick(splitEnd: true));
            RunRoomPhase(CycleComponents);
            RunRoomPhase(CycleFurniture);
            RunRoomPhase(CycleActors);
            RunRoomPhase(() => GetRoomUserManager().SerializeStatusUpdates());
            RunRoomPhase(() => GetWired().OnRoomTickEnd());
            RunRoomPhase(() => _gameItemHandler?.OnCycle());
            RunRoomPhase(CycleWired);
        }
        catch (Exception error) {
            ExceptionLogger.LogException(error);
            OnRoomCrash(error);
        }
    }

    private void CycleComponents()
    {
        foreach (var component in _components) {
            component.Cycle();
        }
    }

    // An empty room unloads after about 33 seconds: 66 cycles of 500 ms.
    private const int IdleCyclesBeforeUnload = 66;

    private bool KeepRoomActive()
    {
        if (GetRoomUserManager().GetRoomUsers().Count == 0) {
            IdleTime++;
        }
        else if (IdleTime > 0) {
            IdleTime = 0;
        }

        if (HasActivePromotion && Promotion.HasExpired) {
            EndPromotion();
        }

        if (IdleTime < IdleCyclesBeforeUnload || HasActivePromotion) {
            return true;
        }

        _rooms.UnloadRoom(Id);

        return false;
    }

    private void RunRoomPhase(Action phase)
    {
        if (MDisposed) {
            return;
        }

        try {
            phase();
        }
        catch (Exception error) {
            ExceptionLogger.LogException(error);
        }
    }

    private void CycleFurniture()
    {
        GetGameMap().Navigation?.ApplyDirty();
        GetGameMap().Navigation?.DrainCommands();
        GetGameMap().Gates.Drain();

        GetRoomItemHandler().OnCycle();
        _music?.Cycle();
    }

    private void CycleActors()
    {
        GetGameMap().Navigation?.ApplyDirty();
        GetGameMap().Navigation?.DrainCommands();
        GetRoomUserManager().OnCycle();
    }

    private void CycleWired()
    {
        GetWired().OnCycle();
        GetGameMap().FlushPlacementUpdates();
    }

    private void OnRoomCrash(Exception e)
    {
        try {
            // A crash while unloading comes after the users were dropped.
            foreach (var user in _roomUserManager?.GetRoomUsers().ToList() ?? []) {
                if (user == null || user.GetClient() == null) {
                    continue;
                }

                user.GetClient().SendNotification("Sorry, it appears that room has crashed!"); //Unhandled exception in room: " + e);

                try {
                    GetRoomUserManager().RemoveUserFromRoom(user.GetClient(), true);
                }
                catch (Exception e2) {
                    ExceptionLogger.LogException(e2);
                }
            }
        }
        catch (Exception e3) {
            ExceptionLogger.LogException(e3);
        }

        IsCrashed = true;
        _rooms.UnloadRoom(Id);
    }


    public bool CheckMute(GameClient session, DateTimeOffset now)
    {
        if (MutedUsers.TryGetValue(session.GetHabbo().Id, out var mutedUntil)) {
            if (now >= mutedUntil) {
                MutedUsers.Remove(session.GetHabbo().Id);
            }
            else {
                return true;
            }
        }

        if (session.GetHabbo().TimeMuted > 0 || RoomMuted && session.GetHabbo().Username != OwnerName) {
            return true;
        }

        return false;
    }

    public void SendObjects(GameClient session)
    {
        GetGameMap().SendPlacementHeightMap(session);
        var presentation = GetGameMap().StaticModel.Presentation;
        session.Send(new FloorHeightMapComposer(GetGameMap().Model.GetRelativeHeightmap(), GetGameMap().StaticModel.WallHeight,
            presentation.HalfScale, Plus.HabboHotel.Items.AreaHide.AreaHideState.EntryHides(this), cameraX: presentation.CameraX, cameraY: presentation.CameraY, cameraZ: presentation.CameraZ));
        session.Send(new RoomVisualizationSettingsComposer(WallThickness, FloorThickness, Convert.ToBoolean(Hidewall)));
        var snapshotUsers = _roomUserManager.GetUserList().Where(user => user != null).ToArray();
        // The entrant gets the users already in the room as one Users packet (empty when nobody is there) before the furni, and its own Users entry after the wall items.
        // It gets no status update for itself here; that follows in the next room cycle.
        var entrant = snapshotUsers.FirstOrDefault(user => !user.IsBot && ReferenceEquals(user.GetClient(), session));
        var present = snapshotUsers.Where(user => !ReferenceEquals(user, entrant)).ToArray();
        session.Send(new UsersComposer(_userSnapshots.Capture(present)));
        // The transparency and highlight state comes right after that Users packet, before the floor items.
        GetWired()?.SendVisualizationState(session);

        foreach (var user in present) {
            SendUserExtras(session, user);
        }

        // Users with a pending update get their status from the next room tick, which reaches this session too.
        var settledUsers = present.Where(user => !user.UpdateNeeded).ToArray();

        if (settledUsers.Length > 0) {
            session.Send(new UserUpdateComposer(RoomUserStatusSnapshot.Capture(settledUsers)));
        }

        FurniVisibility.Seed(this);
        var snapshotFurniture = VisibleFloorItems.ToArray();
        var furnitureSnapshot = RoomFurnitureSnapshot.Capture(snapshotFurniture, OwnerId, OwnerName);
        session.Send(new ObjectsComposer(furnitureSnapshot));
        session.Send(new FurnitureMetadataComposer(furnitureSnapshot.Items));
        var snapshotWalls = GetRoomItemHandler().GetWall.ToArray();
        var wallSnapshot = RoomFurnitureSnapshot.Capture(snapshotWalls, OwnerId, OwnerName);
        session.Send(new ItemsComposer(wallSnapshot));
        session.Send(new FurnitureMetadataComposer(wallSnapshot.Items));

        if (entrant != null && _userSnapshots.Capture(entrant) is { } entrantSnapshot) {
            session.Send(new UsersComposer(entrantSnapshot));
            SendUserExtras(session, entrant, includeEffect: false);
            // A cycle that ran during admission sent the status before the client knew the avatar.
            entrant.UpdateNeeded = true;
        }

        _wiredComponent?.SnapshotEnqueued(session, snapshotFurniture.Concat(snapshotWalls), snapshotUsers);
    }

    private static void SendUserExtras(GameClient session, RoomUser user, bool includeEffect = true)
    {
        if (user.IsBot && user.BotData.DanceId > 0) {
            session.Send(new DanceComposer(user.VirtualId, user.BotData.DanceId));
        }
        else if (!user.IsBot && !user.IsPet && user.IsDancing) {
            session.Send(new DanceComposer(user.VirtualId, user.DanceId));
        }

        if (user.IsAsleep) {
            session.Send(new SleepComposer(user.VirtualId, true));
        }

        if (user.CarryItemId > 0 && user.CarryTimer > 0) {
            session.Send(new CarryObjectComposer(user.VirtualId, user.CarryItemId));
        }

        if (includeEffect && !user.IsBot && !user.IsPet && user.CurrentEffect > 0) {
            session.Send(new AvatarEffectComposer(user.VirtualId, user.CurrentEffect));
        }
    }

    public void AddTent(uint tentId)
    {
        if (_tents.ContainsKey(tentId)) {
            _tents.Remove(tentId);
        }

        _tents.Add(tentId, new());
    }

    public void RemoveTent(uint tentId)
    {
        if (!_tents.ContainsKey(tentId)) {
            return;
        }

        var users = _tents[tentId];

        foreach (var user in users.ToList()) {
            if (user == null || user.GetClient() == null || user.GetClient().GetHabbo() == null) {
                continue;
            }

            user.GetClient().GetHabbo().TentId = 0;
        }

        if (_tents.ContainsKey(tentId)) {
            _tents.Remove(tentId);
        }
    }

    public void AddUserToTent(uint tentId, RoomUser user)
    {
        if (user != null && user.GetClient() != null && user.GetClient().GetHabbo() != null) {
            if (!_tents.ContainsKey(tentId)) {
                _tents.Add(tentId, new());
            }

            if (!_tents[tentId].Contains(user)) {
                _tents[tentId].Add(user);
            }

            user.GetClient().GetHabbo().TentId = tentId;
        }
    }

    public void RemoveUserFromTent(uint tentId, RoomUser user)
    {
        if (user != null && user.GetClient() != null && user.GetClient().GetHabbo() != null) {
            if (!_tents.ContainsKey(tentId)) {
                _tents.Add(tentId, new());
            }

            if (_tents[tentId].Contains(user)) {
                _tents[tentId].Remove(user);
            }

            user.GetClient().GetHabbo().TentId = 0;
        }
    }

    public void SendToTent(int id, uint tentId, Func<int, IServerPacket> chat)
    {
        if (!_tents.ContainsKey(tentId)) {
            return;
        }

        foreach (var user in _tents[tentId].ToList()) {
            if (user == null || user.GetClient() == null || user.GetClient().GetHabbo() == null || user.GetClient().GetHabbo().IgnoresComponent.IsIgnored(id) ||
                user.GetClient().GetHabbo().TentId != tentId) {
                continue;
            }

            user.GetClient().Send(chat(user.VirtualId));
        }
    }

    public void SendPacket(IServerPacket packet, bool withRightsOnly = false) => SendPacket(packet, withRightsOnly, null);

    /// <summary>
    /// Sends a chat packet to everyone in the room. Each copy carries the viewer's own room index as its receiver, so <paramref name="chat"/> composes it per viewer.
    /// </summary>
    public void SendChat(Func<int, IServerPacket> chat, Func<RoomUser, bool>? receives = null)
    {
        if (_roomUserManager == null) {
            return;
        }

        foreach (var user in _roomUserManager.GetRoomUsers()) {
            if (user?.GetClient() is not { Revision: not null } client || receives?.Invoke(user) == false) {
                continue;
            }

            client.Send(chat(user.VirtualId));
        }
    }

    public IEnumerable<Item> VisibleFloorItems
    {
        get
        {
            var hiding = FurniVisibility.Capture(this);

            return GetRoomItemHandler().GetFloor.Where(item => (!HideWired || !item.IsWired) && !FurniVisibility.Hides(hiding, item));
        }
    }

    public void SetWiredHidden(bool hidden)
    {
        HideWired = hidden;
        var wired = GetRoomItemHandler().GetFloor.Where(item => item.IsWired).ToArray();

        if (hidden) {
            foreach (var item in wired) {
                SendPacket(new ObjectRemoveComposer(item.Id, item.IsTemporary, item.UserId));
            }

            return;
        }

        // A box inside a hidden area is not on the clients and stays off them.
        wired = wired.Where(item => !FurniVisibility.IsConcealed(this, item)).ToArray();

        var wiredSnapshot = RoomFurnitureSnapshot.Capture(wired, OwnerId, OwnerName);

        SendPacket(new ObjectsComposer(wiredSnapshot), false, viewer =>
        {
            foreach (var item in wired) {
                _wiredComponent?.ObjectEnqueued(viewer, item, null);
            }
        });
        SendPacket(new FurnitureMetadataComposer(wiredSnapshot.Items));
    }

    public void SendObject(Item item)
    {
        if (FurniVisibility.ConcealOnArrival(this, item)) {
            return;
        }

        if (item is { IsTemporary: true, IsFloorItem: true }) {
            SendTemporaryFloorItems([item]);

            return;
        }

        var furniture = RoomItemSnapshot.Capture(item);

        SendPacket(item.IsWallItem ? new ItemAddComposer(furniture) : new ObjectAddComposer(furniture), false,
            viewer => _wiredComponent?.ObjectEnqueued(viewer, item, null));
        SendPacket(new FurnitureMetadataComposer([furniture]));
    }

    internal void SendTemporaryFloorItems(IReadOnlyList<Item> items)
    {
        if (items.Count == 0) {
            return;
        }

        // Two templates arrive together in one Objects message.
        var snapshot = RoomFurnitureSnapshot.Capture(items, OwnerId, OwnerName);
        SendPacket(new ObjectsComposer(snapshot), false, viewer =>
        {
            foreach (var item in items) {
                _wiredComponent?.ObjectEnqueued(viewer, item, null);
            }
        });
        SendPacket(new FurnitureMetadataComposer(snapshot.Items));
    }

    internal void SendTemporaryWallItems(IReadOnlyList<Item> items)
    {
        if (items.Count == 0) {
            return;
        }

        var snapshot = RoomFurnitureSnapshot.Capture(items, OwnerId, OwnerName);
        SendPacket(new ItemsComposer(snapshot), false, viewer =>
        {
            foreach (var item in items) {
                _wiredComponent?.ObjectEnqueued(viewer, item, null);
            }
        });
        SendPacket(new FurnitureMetadataComposer(snapshot.Items));
    }

    public void SendUser(RoomUser user)
    {
        var snapshot = _userSnapshots.Capture(user);

        if (snapshot != null) {
            SendPacket(new UsersComposer(snapshot), false, viewer => _wiredComponent?.ObjectEnqueued(viewer, null, user));
        }
    }

    internal void SendPacketToOthers(IServerPacket packet, GameClient except) => SendPacket(packet, false, null, except);

    // The admitted user's own client gets its Users entry with the room snapshot instead (SendObjects).
    public void SendUserToOthers(RoomUser user)
    {
        var snapshot = _userSnapshots.Capture(user);

        if (snapshot != null) {
            SendPacket(new UsersComposer(snapshot), false, viewer => _wiredComponent?.ObjectEnqueued(viewer, null, user), user.GetClient());
        }
    }

    private void SendPacket(IServerPacket packet, bool withRightsOnly, Action<RoomUser>? enqueued, GameClient? except = null)
    {
        if (packet == null) {
            return;
        }

        try {
            if (_roomUserManager == null) {
                return;
            }

            GameClient.SendBroadcast(packet, GetRecipients());

            IEnumerable<GameClient> GetRecipients()
            {
                foreach (var user in _roomUserManager.GetRoomUsers()) {
                    var client = user?.GetClient();

                    if (client == null || ReferenceEquals(client, except) || withRightsOnly && !CheckRights(client)) {
                        continue;
                    }

                    yield return client;
                    // SendBroadcast sends inside its loop, so this runs after the packet is queued for this viewer.
                    enqueued?.Invoke(user);
                }
            }
        }
        catch (Exception e) {
            ExceptionLogger.LogException(e);
        }
    }

    // Clients whose revision does not map the data composer get the full update instead; the mapping is checked before anything is sent to a client.
    internal void SendObjectData(Item item, string data, bool batch)
    {
        var wire = FurnitureWireData.ForState(item.ExtraData, data);
        IServerPacket packet = batch ? new ObjectsDataUpdateComposer([(item.Id, wire)]) : new ObjectDataUpdateComposer(item.Id, wire);

        try {
            if (_roomUserManager == null) {
                return;
            }

            var clients = _roomUserManager.GetRoomUsers().Select(user => user?.GetClient()).OfType<GameClient>().ToArray();
            bool Maps(GameClient client, uint id) => client.Revision?.InternalIdToOutgoingIdMapping.ContainsKey(id) == true;
            GameClient.SendBroadcast(packet, clients.Where(client => Maps(client, packet.MessageId)));
            var fallback = clients.Where(client => !Maps(client, packet.MessageId) && Maps(client, Plus.Communication.Packets.Outgoing.ServerPacketHeader.ObjectUpdateComposer)).ToArray();

            if (fallback.Length > 0) {
                var legacy = RoomItemSnapshot.Capture(item) with { Data = wire };
                GameClient.SendBroadcast(new ObjectUpdateComposer(legacy), fallback);
                GameClient.SendBroadcast(new FurnitureMetadataComposer([legacy]), fallback);
            }
        }
        catch (Exception e) {
            ExceptionLogger.LogException(e);
        }
    }

    /// <summary>
    /// Several floor items' data in one ObjectsDataUpdate; clients whose revision does not map it get each item's full
    /// update instead.
    /// </summary>
    /// <summary>
    /// The tick's box flashes go out before its Wired movement packet: the ObjectsDataUpdate with the fired boxes precedes the WiredMovements of the same firing.
    /// </summary>
    internal void FlushWiredFlashes() => _wiredComponent?.FlushFlashes();
    internal void FlushWiredFlashData() => _wiredComponent?.FlushFlashData();
    internal bool HoldsWiredMovements => _wiredComponent?.HoldsMovements == true;

    /// <summary>Sends a Wired movement packet after the pending box flashes and furni states (see WiredComponent.SendWiredMovement).</summary>
    internal void SendWiredMovement(IServerPacket packet)
    {
        if (_wiredComponent is { } wired) {
            wired.SendWiredMovement(packet);
        }
        else {
            SendPacket(packet);
        }
    }

    /// <param name="visits">Items a user sees only when its <see cref="RoomUser.Visit"/> is at most this (<see cref="RoomUserManager.Visits"/>).</param>
    internal void SendObjectsData(IReadOnlyList<Item> items, IReadOnlyDictionary<Item, int>? visits = null)
    {
        if (_roomUserManager == null) {
            return;
        }

        var users = _roomUserManager.GetRoomUsers().Where(user => user?.GetClient() != null);

        // Users who are past the same number of visit limits see the same items.
        foreach (var audience in users.GroupBy(user => visits?.Values.Count(limit => user.Visit > limit) ?? 0)) {
            var user = audience.First();
            SendObjectsData([.. items.Where(item => visits?.TryGetValue(item, out var limit) != true || user.Visit <= limit)],
                [.. audience.Select(member => member.GetClient()!)]);
        }
    }

    private static void SendObjectsData(IReadOnlyList<Item> items, GameClient[] clients)
    {
        if (items.Count == 0) {
            return;
        }

        IServerPacket packet = new ObjectsDataUpdateComposer([.. items.Select(item => (item.Id, FurnitureWireData.ForState(item.ExtraData, item.LegacyDataString)))]);

        try {
            bool Maps(GameClient client, uint id) => client.Revision?.InternalIdToOutgoingIdMapping.ContainsKey(id) == true;
            GameClient.SendBroadcast(packet, clients.Where(client => Maps(client, packet.MessageId)));
            var fallback = clients.Where(client => !Maps(client, packet.MessageId) && Maps(client, Plus.Communication.Packets.Outgoing.ServerPacketHeader.ObjectUpdateComposer)).ToArray();

            if (fallback.Length > 0) {
                foreach (var item in items) {
                    var legacyFurniture = RoomItemSnapshot.Capture(item);
                    GameClient.SendBroadcast(new ObjectUpdateComposer(legacyFurniture), fallback);
                    GameClient.SendBroadcast(new FurnitureMetadataComposer([legacyFurniture]), fallback);
                }
            }
        }
        catch (Exception e) {
            ExceptionLogger.LogException(e);
        }
    }

    public void SendPacket(List<IServerPacket> packets)
    {
        if (packets.Count <= 1) {
            foreach (var packet in packets) {
                SendPacket(packet);
            }

            return;
        }

        try {
            if (_roomUserManager == null) {
                return;
            }

            GameClient.SendBroadcast(packets, _roomUserManager.GetRoomUsers().Select(user => user?.GetClient()).OfType<GameClient>().ToList());
        }
        catch (Exception e) {
            ExceptionLogger.LogException(e);
        }
    }

    public void Dispose()
    {
        DisposeExecutorRoom();

        return;
    }

    private void ProcessWiredOwned() => RunOwnedPass(() =>
    {
        GetWired().OnFastCycle();
        _iceTag?.Cycle();
    });

    // The fast pass owns the room for the gate sequencer, so Wired closes run inline and in order.
    private void RunOwnedPass(Action pass)
    {
        if (IsCrashed || MDisposed) {
            return;
        }

        using var owner = Plus.HabboHotel.Rooms.PathFinding.RoomOwnerScope.Enter(this);

        try {
            pass();
        }
        catch (Exception e) {
            ExceptionLogger.LogException(e);
        }
    }

    private void DisposeExecutorRoom()
    {
        lock (NavigationSync) {

            if (MDisposed) {
                return;
            }

            IsCrashed = false;
            MDisposed = true;
            _gamemap?.Navigation?.Shutdown();
            _gamemap?.ClosePlacementUpdates();
            RemoveRemainingUsers();
            DisposeCompletedTask();
            ClearRoomCollections();
            DisposeGames();
            DisposeMapAndTeams();
            DisposeRoomManagers();
            ClearRoomComponents();
        }
    }

    private void RemoveRemainingUsers()
    {
        if (_roomUserManager == null) {
            return;
        }

        foreach (var user in _roomUserManager.GetRoomUsers().ToList()) {
            var client = user?.GetClient();

            if (client != null) {

                _roomUserManager.RemoveUserFromRoom(client, true);
            }
        }
    }

    private void DisposeCompletedTask()
    {
        try {
            if (ProcessTask is { IsCompleted: true }) {
                ProcessTask.Dispose();
            }
        }
        catch { }
    }

    private void ClearRoomCollections()
    {

        TonerData = null;
        MoodlightData = null;

        if (MutedUsers.Count > 0) {
            MutedUsers.Clear();
        }

        if (_tents.Count > 0) {
            _tents.Clear();
        }

        if (UsersWithRights.Count > 0) {
            UsersWithRights.Clear();
        }
    }

    private void DisposeGames()
    {

        if (_gameManager != null) {
            _gameManager.Dispose();
            _gameManager = null;
        }

        if (_freeze != null) {
            _freeze.Dispose();
            _freeze = null;
        }

        if (_soccer != null) {
            _soccer.Dispose();
            _soccer = null;
        }

        if (_banzai != null) {
            _banzai.Dispose();
            _banzai = null;
        }
    }

    private void DisposeMapAndTeams()
    {

        if (_gamemap != null) {
            _gamemap.Dispose();
            _gamemap = null;
        }

        if (_gameItemHandler != null) {
            _gameItemHandler.Dispose();
            _gameItemHandler = null;
        }

        if (Teambanzai != null) {
            Teambanzai.Dispose();
            Teambanzai = null;
        }

        if (Teamfreeze != null) {
            Teamfreeze.Dispose();
            Teamfreeze = null;
        }
    }

    private void DisposeRoomManagers()
    {

        if (_roomUserManager != null) {
            _roomUserManager.Dispose();
            _roomUserManager = null;
        }

        if (_roomItemHandling != null) {
            _roomItemHandling.Dispose();
            _roomItemHandling = null;
        }
    }

    private void ClearRoomComponents()
    {

        if (WordFilterList.Count > 0) {
            WordFilterList.Clear();
        }

        _filterComponent?.Cleanup();
        _wiredComponent?.Cleanup();
        _bansComponent?.Cleanup();
        _tradingComponent?.Cleanup();
    }
}
