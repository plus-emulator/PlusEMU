using Plus.Core.Settings;
using Plus.Communication.Packets;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing.Rooms.Permissions;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.Core;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.AI.Speech;
using Plus.HabboHotel.Rooms.Chat.Emotions;
using Plus.Core.FigureData;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Rooms.Trading;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms;

public class RoomUserManager
{
    public const int MaxStressBots = 2000;
    private readonly object _stressSync = new();
    private readonly Queue<(int Amount, int OwnerId, Action<string> Reply, Func<string, string>? Outfit)> _stressRequests = new();
    private readonly ConcurrentQueue<(int X, int Y, RoomUser Speaker)> _headTurnRequests = new();
    // Owned by the room tick: each turned head goes back to the body at ResetTick, if it is still Head.
    private readonly List<(int X, int Y, RoomUser Speaker)> _headTurnBatch = [];
    private readonly Dictionary<RoomUser, (long ResetTick, int Head)> _headTurns = new();
    private long _headTurnTick;
    // The head goes back 5000 ms after the status update that showed the turn.
    private static readonly int HeadTurnTicks = (int)(TimeSpan.FromSeconds(5) / RoomCycle.Interval);
    private bool _disposed;
    private int _nextStressBotId;

    private ConcurrentDictionary<int, RoomUser> _bots;
    private ConcurrentDictionary<int, RoomUser> _pets;

    private int _primaryPrivateUserId;
    private Room _room;
    private readonly IRoomUserStore _store;
    private readonly TimeProvider _clock;
    private readonly IRewardTrackManager _rewards;
    private readonly IChatEmotionsManager _chatEmotions;
    private readonly IBotAiFactory _botAiFactory;
    private readonly IGameClientManager _clients;
    private readonly IItemTravelStore _travelStore;
    private readonly ISettingsManager _settings;
    private ConcurrentDictionary<int, RoomUser> _users;

    public int UserCount;


    public RoomUserManager(Room room, IRoomUserStore store, TimeProvider clock, IRewardTrackManager rewards,
        IChatEmotionsManager chatEmotions, IBotAiFactory botAiFactory, IGameClientManager clients, IItemTravelStore travelStore,
        ISettingsManager settings)
    {
        _room = room;
        _store = store;
        _clock = clock;
        _rewards = rewards;
        _chatEmotions = chatEmotions;
        _botAiFactory = botAiFactory;
        _clients = clients;
        _travelStore = travelStore;
        _settings = settings;
        _users = new();
        _pets = new();
        _bots = new();
        _primaryPrivateUserId = 0;
        PetCount = 0;
        UserCount = 0;
    }

    /// <summary>Every user, bot and pet that comes into the room takes the next VirtualId, so this counts the visits started so far.</summary>
    internal int Visits => Volatile.Read(ref _primaryPrivateUserId);

    public int PetCount { get; private set; }

    // Requests are bounded and applied on the cycle thread, before its user snapshot.
    // outfit maps a gender to a look; without it every bot wears the default figure.
    public bool QueueStressBots(int amount, int ownerId, Action<string> reply, Func<string, string>? outfit = null)
    {
        if (amount < 0 || amount > MaxStressBots) {
            return false;
        }

        lock (_stressSync) {
            if (_disposed || _stressRequests.Count >= 8) {
                return false;
            }

            _stressRequests.Enqueue((amount, ownerId, reply, outfit));

            return true;
        }
    }

    internal void ProcessStressBots()
    {
        lock (_stressSync) {
            if (_disposed || !_stressRequests.TryDequeue(out var request)) {
                return;
            }

            var temporary = _bots.Values.Where(user => user.BotData?.IsTemporary == true).ToList();

            if (request.Amount == 0) {
                foreach (var user in temporary) {
                    _room.GetGameMap().RemoveUserFromMap(user, user.Coordinate);
                    RemoveBot(user.VirtualId, false);
                }

                request.Reply($"Cleared {temporary.Count} temporary stress bots.");

                return;
            }

            if (temporary.Count + request.Amount > MaxStressBots) {
                request.Reply($"This room has {temporary.Count} stress bots; the limit is {MaxStressBots}. Use :stress bots clear first.");

                return;
            }

            var model = _room.GetGameMap().Model;

            for (var i = 0; i < request.Amount; i++) {
                // Negative IDs cannot collide with database/inventory bots.
                var id = --_nextStressBotId;
                var speeches = new List<RandomSpeech> { new($"Stress bot {-id} checking room traffic.", id) };
                var gender = request.Outfit == null || Random.Shared.Next(2) == 0 ? "M" : "F";
                var look = request.Outfit?.Invoke(gender) ?? IFigureDataManager.DefaultFigure;
                var bot = new RoomBot(id, _room.RoomId, "generic", "freeroam", $"Stress {-id}", "Temporary stress bot",
                    look, model.DoorX, model.DoorY, model.DoorZ, model.DoorOrientation,
                    0, 0, 0, 0, ref speeches, gender, 0, request.OwnerId, true, 60, false, 0)
                { IsTemporary = true };
                var user = DeployBot(bot, null);
                user.AllowOverride = true;
                // DeployBot always assigns the AI it just created.
                user.BotAi?.OnSelfEnterRoom();
            }

            request.Reply($"Created {request.Amount} temporary stress bots ({temporary.Count + request.Amount}/{MaxStressBots}). Use :stress bots clear to remove them.");
        }
    }

    private void PublishSpawn(RoomUser actor)
    {

        _users.TryAdd(actor.InternalRoomId, actor);
        AdmitSpawn(actor);
    }

    private void AdmitSpawn(RoomUser actor)
    {
        _room.GetGameMap().Navigation!.Admit(actor);
    }

    public RoomUser DeployBot(RoomBot bot, Pet pet)
    {
        var user = new RoomUser(0, _room.RoomId, Interlocked.Increment(ref _primaryPrivateUserId) - 1, _room, null, _chatEmotions, _rewards);
        bot.VirtualId = user.VirtualId;
        var personalId = user.VirtualId;
        user.InternalRoomId = personalId;

        var model = _room.GetGameMap().Model;

        if (bot.X > 0 && bot.Y > 0 && bot.X < model.MapSizeX && bot.Y < model.MapSizeY) {
            user.InitializePosition(bot.X, bot.Y, bot.Z);

            user.SetRot(bot.Rot, false);
        }
        else {
            bot.X = model.DoorX;
            bot.Y = model.DoorY;
            user.InitializePosition(model.DoorX, model.DoorY, model.DoorZ);

            user.SetRot(model.DoorOrientation, false);
        }

        user.BotData = bot;
        user.BotAi = _botAiFactory.Create(bot.AiType, user.VirtualId);

        if (user.IsPet) {
            user.BotAi.Init(bot.BotId, user.VirtualId, _room.RoomId, user, _room);
            user.PetData = pet;
            user.PetData.VirtualId = user.VirtualId;
            user.PetData.Attach(_room, _clients, _rewards);
        }
        else {
            user.BotAi.Init(bot.BotId, user.VirtualId, _room.RoomId, user, _room);
        }

        user.UpdateNeeded = true;
        PublishSpawn(user);

        _room.SendUser(user);

        if (user.IsPet && user.PetData is { } petData) {
            if (_pets.ContainsKey(petData.PetId)) {
                _pets[petData.PetId] = user;
            }
            else {
                _pets.TryAdd(petData.PetId, user);
            }

            PetCount++;
        }
        else if (user.IsBot) {
            if (_bots.ContainsKey(user.BotData.BotId)) {
                _bots[user.BotData.BotId] = user;
            }
            else {
                _bots.TryAdd(user.BotData.Id, user);
            }

            _room.SendPacket(new DanceComposer(user.VirtualId, user.BotData.DanceId));
        }

        return user;
    }

    public void RemoveBot(int virtualId, bool kicked)
    {
        var user = GetRoomUserByVirtualId(virtualId);

        if (user == null || !user.IsBot) {
            return;
        }

        if (_users == null || !_users.TryRemove(new KeyValuePair<int, RoomUser>(user.InternalRoomId, user))) {
            return;
        }

        try {
            _room.GetGameMap().Navigation.Remove(user);

            if (user.IsPet) {
                if (user.PetData is { } petData && _pets.TryRemove(new KeyValuePair<int, RoomUser>(petData.PetId, user))) {
                    PetCount--;
                }
            }
            else {
                _bots.TryRemove(new KeyValuePair<int, RoomUser>(user.BotData.Id, user));
            }

            _room.IceTagLeave(user);
            _room.GetWired()?.BeforeActorLeaves(user);
            user.BotAi?.OnSelfLeaveRoom(kicked);
            _room.SendPacket(new UserRemoveComposer(user.VirtualId));
            _room.GetWired()?.Dispatch(new WiredRuntimeEvent(WiredEventKind.Leave) { Actor = user });
            OnRemove(user);
        }
        finally {
            user.BotAi?.Detach(_room, user);
            user.PetData?.Detach(_room);
            user.Dispose();
        }
    }

    public RoomUser? GetUserForSquare(int x, int y) => _room.GetGameMap().GetRoomUsers(new(x, y)).FirstOrDefault();

    public bool AddAvatarToRoom(GameClient session)
    {
        if (_room == null) {
            return false;
        }

        if (session == null) {
            return false;
        }

        if (session.GetHabbo().CurrentRoom == null) {
            return false;
        }

        if (_users.Any(u => u.Value.UserId == session.GetHabbo().Id)) {
            return false;
        }

        var visit = Interlocked.Increment(ref _primaryPrivateUserId);
        var user = new RoomUser(session.GetHabbo().Id, _room.RoomId, visit - 1, _room, session, _chatEmotions, _rewards) { Visit = visit };
        user.UserId = session.GetHabbo().Id;
        session.GetHabbo().TentId = 0;
        var personalId = user.VirtualId;
        user.InternalRoomId = personalId;
        session.GetHabbo().CurrentRoom = _room;

        user.WiredRoomEntry = WiredRoomEntrySnapshot.Capture(_room, session.GetHabbo());
        var model = _room.GetGameMap().Model;

        if (model == null) {
            return false;
        }

        if (!_room.PetMorphsAllowed && session.GetHabbo().PetId != 0) {
            session.GetHabbo().PetId = 0;
        }

        if (!session.GetHabbo().IsTeleporting && !session.GetHabbo().IsHopping) {
            if (!model.DoorIsValid()) {
                var square = _room.GetGameMap().GetRandomWalkableSquare();
                model.DoorX = square.X;
                model.DoorY = square.Y;
                model.DoorZ = (int)_room.GetGameMap().GetHeightForSquareFromData(square);
            }

            user.InitializePosition(model.DoorX, model.DoorY, model.DoorZ);

            user.SetRot(model.DoorOrientation, false);
        }
        else if (!user.IsBot && (session.GetHabbo().IsTeleporting || session.GetHabbo().IsHopping)) {
            Item? item = null;

            if (session.GetHabbo().IsTeleporting) {
                item = _room.GetRoomItemHandler().GetItem(session.GetHabbo().TeleporterId);
            }
            else if (session.GetHabbo().IsHopping) {
                item = _room.GetRoomItemHandler().GetItem(session.GetHabbo().HopperId);
            }

            if (item != null) {
                if (session.GetHabbo().IsTeleporting && item.Definition.InteractionType == InteractionType.WiredRoomLinker) {
                    user.InitializePosition(item.GetX, item.GetY, item.GetZ);

                    user.SetRot(item.Rotation, false);
                    // Nothing walks the user out of a linker, so the entry ends here (the wired entry snapshot was taken above).
                    session.GetHabbo().IsTeleporting = false;
                    session.GetHabbo().TeleporterId = 0;
                }
                else if (session.GetHabbo().IsTeleporting) {
                    item.LegacyDataString = "2";
                    item.UpdateState(false, true);
                    user.InitializePosition(item.GetX, item.GetY, item.GetZ);

                    user.SetRot(item.Rotation, false);

                    if (session.GetHabbo().TeleporterId != 0) {
                        _rewards.Progress(session, RewardTrackActions.Teleport);
                    }

                    item.InteractingUser2 = session.GetHabbo().Id;
                    item.LegacyDataString = "0";
                    item.UpdateState(false, true);
                }
                else if (session.GetHabbo().IsHopping) {
                    item.LegacyDataString = "1";
                    item.UpdateState(false, true);
                    user.InitializePosition(item.GetX, item.GetY, item.GetZ);

                    user.SetRot(item.Rotation, false);
                    user.AllowOverride = false;
                    item.InteractingUser2 = session.GetHabbo().Id;
                    item.LegacyDataString = "2";
                    item.UpdateState(false, true);
                }
            }
            else {
                user.InitializePosition(model.DoorX, model.DoorY, model.DoorZ - 1);

                user.SetRot(model.DoorOrientation, false);
            }
        }

        if (!_users.TryAdd(personalId, user)) {
            return false;
        }

        AdmitSpawn(user);

        _room.SendUserToOthers(user);

        // A user forwarded by Teleport to Room arrives still glowing and loses the glow as an in-room teleport does after landing.
        if (session.GetHabbo().ForwardArrivalRestoreEffect is { } restore) {
            session.GetHabbo().ForwardArrivalRestoreEffect = null;
            user.ForwardArrivalEffect = (WiredRoomForwarding.ArrivalEffectTicks, restore);
            ApplyArrivalEffect(session, user, WiredRoomForwarding.TeleportEffectId);
        }

        if (ControllerLevel(session) is > 0 and var level) {
            user.SetStatus("flatctrl", level.ToString());
        }

        user.UpdateNeeded = true;

        if (ForcedEntryEffect.For(session.GetHabbo(), _settings) is > 0 and var forcedEffect) {
            ApplyArrivalEffect(session, user, forcedEffect);
        }

        foreach (var bot in _bots.Values.ToList()) {
            if (bot == null || bot.BotAi == null) {
                continue;
            }

            bot.BotAi.OnUserEnterRoom(user);
        }

        if (session.GetHabbo().Id != _room.OwnerId) {
            _rewards.Progress(session, RewardTrackActions.EnterOtherUsersRoom);
        }

        var pendingFollow = session.GetHabbo().PendingFollowRoomId;

        if (pendingFollow != 0) {
            session.GetHabbo().PendingFollowRoomId = 0;

            if (pendingFollow == _room.RoomId) {
                _rewards.Progress(session, RewardTrackActions.FollowFriend);
            }
        }

        return true;
    }

    // The room sees the effect now; the entrant gets its own effect with the room entry, after the room rating (AIR WIN63-202610151745-318700621).
    private void ApplyArrivalEffect(GameClient session, RoomUser user, int effectId)
    {
        if (session.GetHabbo().Effects is not { } effects) {
            return;
        }

        effects.CurrentEffect = effectId;
        _room.SendPacketToOthers(new AvatarEffectComposer(user.VirtualId, effectId), session);
    }

    private int ControllerLevel(GameClient session)
    {
        if (_room.CheckRights(session, true)) {
            return 4;
        }

        if (_room.Group == null) {
            return _room.CheckRights(session, false) ? 1 : 0;
        }

        var habboId = session.GetHabbo().Id;

        if (!_room.Group.IsAdmin(habboId) && _room.UsersWithRights.Contains(habboId)) {
            return 1;
        }

        return _room.CheckRights(session, false, true) ? 3 : 0;
    }

    // Observed on AIR WIN63-202610151745-318700621: the entrant's controller level and Wired permissions follow the room's labels,
    // then YouAreOwner for the owner.
    public void SendEntryRights(GameClient session)
    {
        var level = ControllerLevel(session);

        if (level > 0) {
            session.Send(new YouAreControllerComposer(_room.Id, level));
        }
        else {
            session.Send(new YouAreNotControllerComposer(_room.Id));
        }

        _room.GetWired()?.SendPermissions(session);

        if (level == 4) {
            session.Send(new YouAreOwnerComposer(_room.Id));
        }
    }

    public void RemoveUserFromRoom(GameClient? session, bool nofityUser, bool notifyKick = false, short? closeReason = null)
    {
        RoomUser? removedUser = null;

        try {
            if (_room == null) {
                return;
            }

            if (session == null || session.GetHabbo() == null) {
                return;
            }

            var user = GetUserList().FirstOrDefault(candidate => !candidate.IsBot && ReferenceEquals(candidate.GetClient(), session));

            if (user == null) {
                if (GetRoomUserByHabbo(session.GetHabbo().Id) != null || !ReferenceEquals(session.GetHabbo().CurrentRoom, _room)) {
                    return;
                }
            }

            if (notifyKick) {
                session.Send(new GenericErrorComposer(GenericError.KickedFromRoom));
            }

            // Observed on AIR WIN63-202610151745-318700621: leaving a room, for the hotel view or another room, starts with YouArePlayingGame(false).
            if (user != null) {
                session.Send(new Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired.YouArePlayingGameComposer(false));
            }

            if (nofityUser) {
                session.Send(new CloseConnectionComposer(closeReason));
            }

            if (session.GetHabbo().TentId > 0) {
                session.GetHabbo().TentId = 0;
            }

            session.EndCameraContext();

            if (user != null) {
                _room.GetGameMap().Navigation!.Remove(user);
            }

            if (user != null) {
                _room.IceTagLeave(user);
                _room.GetWired()?.BeforeActorLeaves(user);
            }

            session.GetHabbo().CurrentRoom = null;

            if (user != null) {
                if (user.RidingHorse) {
                    user.RidingHorse = false;
                    var userRiding = GetRoomUserByVirtualId(user.HorseId);

                    if (userRiding != null) {
                        userRiding.RidingHorse = false;
                        userRiding.HorseId = 0;
                    }
                }

                if (user.Team != Team.None) {
                    var team = _room.GetTeamManagerForFreeze();

                    if (team != null) {
                        // The user may be in the Banzai team list instead.
                        var banzai = _room.GetTeamManagerForBanzai();
                        (banzai.Has(user) ? banzai : team).OnUserLeave(user);
                        user.Team = Team.None;

                        if (session.GetHabbo().Effects is { CurrentEffect: not 0 } teamEffects) {
                            teamEffects.ApplyEffect(0);
                            // ApplyEffect is skipped when the session already moved on (re-entering the room); the team effect must
                            // still not come back with the next room entry. Observed on AIR WIN63-202610151745-318700621
                            // (wf_act_kick_user.cov_selector): a teamed owner re-entering gets no avatar effect.
                            teamEffects.CurrentEffect = 0;
                        }
                    }
                }

                // A football gate outfit belongs to its room: the user's own figure is given back on leaving.
                if (user.FootballGateOutfit is { } outfit && outfit.BaseLook == session.GetHabbo().Look) {
                    user.FootballGateOutfit = null;
                    session.Send(new UserChangeComposer(AvatarChangeSnapshot.Capture(user, true)));
                }

                removedUser = user;
                RemoveRoomUser(user, actorLeavePrepared: true);

                if (user.CurrentItemEffect != ItemEffectType.None) {
                    if (session.GetHabbo().Effects is { } itemEffects) {
                        itemEffects.CurrentEffect = -1;
                    }
                }

                if (user.IsTrading) {
                    Trade? trade = null;

                    if (_room.GetTrading().TryGetTrade(user.TradeId, out trade)) {
                        trade.EndTrade(user.TradeId);
                    }
                }

                //Session.GetHabbo().CurrentRoomId = 0;
                session.GetHabbo().Messenger?.NotifyChangesToFriends();
                _store.RecordExit(_room.RoomId, session.GetHabbo().Id, _clock.GetUtcNow(), _room.UsersNow);
            }
        }
        catch (Exception e) {
            ExceptionLogger.LogException(e);
        }
        finally {
            removedUser?.Dispose();
        }
    }

    private void OnRemove(RoomUser user)
    {
        try {
            var session = user.GetClient();

            if (session == null) {
                return;
            }

            var bots = new List<RoomUser>();

            try {
                foreach (var roomUser in GetUserList().ToList()) {
                    if (roomUser == null) {
                        continue;
                    }

                    if (roomUser.IsBot && !roomUser.IsPet) {
                        if (!bots.Contains(roomUser)) {
                            bots.Add(roomUser);
                        }
                    }
                }
            }
            catch { }

            var petsToRemove = new List<RoomUser>();

            foreach (var bot in bots.ToList()) {
                if (bot == null || bot.BotAi == null) {
                    continue;
                }

                bot.BotAi.OnUserLeaveRoom(session);

                if (bot.IsPet && bot.PetData?.OwnerId == user.UserId && !_room.CheckRights(session, true)) {
                    if (!petsToRemove.Contains(bot)) {
                        petsToRemove.Add(bot);
                    }
                }
            }

            foreach (var toRemove in petsToRemove.ToList()) {
                if (toRemove == null) {
                    continue;
                }

                if (session?.GetHabbo()?.Inventory is not { } inventory) {
                    continue;
                }

                if (toRemove.PetData is { } pet && inventory.Pets.AddPet(pet)) {
                    pet.RoomId = 0;
                    pet.PlacedInRoom = false;
                    RemoveBot(toRemove.VirtualId, false);
                }
            }

            _room.GetGameMap().RemoveUserFromMap(user, new(user.X, user.Y));
        }
        catch (Exception e) {
            ExceptionLogger.LogCriticalException(e);
        }
    }

    private bool RemoveRoomUser(RoomUser user, bool detachAfterRemoval = false, bool actorLeavePrepared = false)
    {
        if (!_users.TryGetValue(user.InternalRoomId, out var registered) || !ReferenceEquals(registered, user)) {
            return false;
        }

        // The leaver itself gets no UserRemove (AIR WIN63-202610151745-318700621); its client drops the whole room.
        var leaver = user.IsBot ? null : user.GetClient();
        var recipients = GetRoomUsers().Select(roomUser => roomUser.GetClient()).Where(client => client != null && !ReferenceEquals(client, leaver))
            .Cast<GameClient>().ToArray();

        if (!_users.TryRemove(new KeyValuePair<int, RoomUser>(user.InternalRoomId, user))) {
            return false;
        }

        try {
            if (!actorLeavePrepared) {
                _room.GetGameMap().Navigation.Remove(user);
            }

            if (!actorLeavePrepared) {
                _room.IceTagLeave(user);
                _room.GetWired()?.BeforeActorLeaves(user);
            }

            _room.GetGameMap().RemoveUserFromMap(user, new(user.X, user.Y));

            try {
                GameClient.SendBroadcast(new UserRemoveComposer(user.VirtualId), recipients);
            }
            catch (Exception error) {
                ExceptionLogger.LogException(error);
            }

            _room.GetWired()?.Dispatch(new WiredRuntimeEvent(WiredEventKind.Leave) { Actor = user });
            user.InternalRoomId = -1;
            OnRemove(user);

            return true;
        }
        finally {
            if (detachAfterRemoval) {
                user.Dispose();
            }
        }
    }

    private void RemoveAbandonedRoomUser(RoomUser user)
    {
        if (RemoveRoomUser(user, true)) {
            return;
        }

        if (!_users.Values.Any(candidate => ReferenceEquals(candidate, user))) {
            user.Dispose();
        }
    }

    public bool TryGetPet(int petId, [NotNullWhen(true)] out RoomUser? pet) => _pets.TryGetValue(petId, out pet);

    public bool TryGetBot(int botId, [NotNullWhen(true)] out RoomUser? bot) => _bots.TryGetValue(botId, out bot);

    public RoomUser? GetBotByName(string name)
    {
        var match = _bots.Values.FirstOrDefault(x => x.BotData != null && x.BotData.Name.ToLower() == name.ToLower());

        return match?.BotData is { } data ? _bots[data.Id] : null;
    }

    public void UpdateUserCount(int count)
    {
        UserCount = count;
        _room.UsersNow = count;
        _store.UpdateUserCount(_room.RoomId, count);
    }

    public RoomUser? GetRoomUserByVirtualId(int virtualId)
    {
        RoomUser? user = null;

        if (!_users.TryGetValue(virtualId, out user)) {
            return null;
        }

        return user;
    }

    public RoomUser? GetRoomUserByHabbo(int id)
    {
        var user = GetUserList().FirstOrDefault(x => !x.IsBot && x.HabboId == id);

        if (user != null) {
            return user;
        }

        return null;
    }

    public List<RoomUser> GetRoomUsers()
    {
        var users = new List<RoomUser>();

        foreach (var entry in _users) {
            if (!entry.Value.IsBot) {
                users.Add(entry.Value);
            }
        }

        return users;
    }

    public List<RoomUser> GetRoomUsersWithPermission(string permission)
    {
        var returnList = new List<RoomUser>();

        foreach (var user in GetUserList().ToList()) {
            if (user == null) {
                continue;
            }

            if (!user.IsBot && user.GetClient()?.GetHabbo()?.Access.Can(permission) == true) {
                returnList.Add(user);
            }
        }

        return returnList;
    }

    public RoomUser? GetRoomUserByHabbo(string pName)
    {
        var user = GetUserList().FirstOrDefault(x =>
            x.GetClient()?.GetHabbo()?.Username.Equals(pName, StringComparison.OrdinalIgnoreCase) == true);

        if (user != null) {
            return user;
        }

        return null;
    }

    public void UpdatePets()
    {
        foreach (var pet in GetPets().ToList()) {
            if (pet == null || pet.PetId <= 0) {
                continue;
            }

            if (pet.DbState == PetDatabaseUpdateState.NeedsInsert) {
                _store.SavePet(new(pet.PetId, pet.OwnerId, pet.RoomId, pet.Name, pet.Type, pet.Race, pet.Color,
                    pet.CreatedAt, 0, 0, 0, 0, 100, 0, 0, true));
            }
            else if (pet.DbState == PetDatabaseUpdateState.NeedsUpdate) {
                //Surely this can be *99 better? // TODO
                var user = GetRoomUserByVirtualId(pet.VirtualId);
                _store.SavePet(new(pet.PetId, pet.OwnerId, pet.RoomId, pet.Name, pet.Type, pet.Race, pet.Color,
                    pet.CreatedAt, user?.X ?? 0, user?.Y ?? 0, user?.Z ?? 0, pet.Experience, pet.Energy, pet.Nutrition, pet.Respect, false));
            }

            pet.DbState = PetDatabaseUpdateState.Updated;
        }
    }

    private void UpdateBots()
    {
        foreach (var user in GetRoomUsers().ToList()) {
            if (user == null || !user.IsBot || user.BotData.IsTemporary) {
                continue;
            }

            if (user.IsBot) {
                _store.SaveBot(new(user.BotData.BotId, user.X, user.Y, user.Z, user.BotData.Name, user.BotData.Look, user.BotData.Rot));
            }
        }
    }


    public List<Pet> GetPets()
    {
        var pets = new List<Pet>();

        foreach (var user in _pets.Values.ToList()) {
            if (user?.PetData is not { } pet || !user.IsPet) {
                continue;
            }

            pets.Add(pet);
        }

        return pets;
    }

    public void SerializeStatusUpdates()
    {
        var users = new List<RoomUser>();
        var roomUsers = GetUserList();

        if (roomUsers == null) {
            return;
        }

        var hasViewer = false;

        foreach (var user in roomUsers) {
            if (user == null) {
                continue;
            }

            hasViewer |= !user.IsBot && user.GetClient() != null;

            // A wired move's "wf" status and a sign's "sign" status last one status update.
            if (user.WiredMoveStatusSent) {
                user.WiredMoveStatusSent = false;

                if (user.HasStatus("wf")) {
                    user.RemoveStatus("wf");
                    user.UpdateNeeded = true;
                }
            }

            if (user.SignStatusSent) {
                user.SignStatusSent = false;

                if (user.HasStatus("sign")) {
                    user.RemoveStatus("sign");
                    user.UpdateNeeded = true;
                }
            }

            if (!user.UpdateNeeded) {
                continue;
            }

            user.UpdateNeeded = false;
            user.WiredMoveStatusSent = user.HasStatus("wf");
            user.SignStatusSent = user.HasStatus("sign");
            users.Add(user);
        }

        if (users.Count > 0 && hasViewer) {
            _room.SendPacket(new UserUpdateComposer(RoomUserStatusSnapshot.Capture(users)));
        }
    }

    public void UpdateUserStatusses()
    {
        _room.GetGameMap().Navigation!.RefreshPostures();

        return;
    }

    private bool IsValid(RoomUser? user)
    {
        if (user == null || _disposed || !user.IsAttachedTo(_room)) {
            return false;
        }

        if (user.IsBot) {
            return true;
        }

        return user.GetClient()?.GetHabbo()?.CurrentRoom == _room;
    }

    internal bool ValidateMovementActor(RoomUser actor)
    {
        if (_disposed) {
            return false;
        }

        if (IsValid(actor) && !actor.NeedsAutokick) {
            return true;
        }

        var client = actor.GetClient();

        if (client?.GetHabbo()?.CurrentRoom == _room) {
            RemoveUserFromRoom(client, true);
        }
        else {
            RemoveAbandonedRoomUser(actor);
        }

        return false;
    }

    public void OnCycle()
    {
        lock (_stressSync) {
            if (_disposed) {
                return;
            }

            ProcessStressBots();
            ExpireForwardArrivalEffects();
            CycleHeadTurns();
            _room.GetGameMap().Navigation!.Executor.Tick();
            _room.GetGameMap().Navigation!.TickMounts();
        }
    }

    // A unit can speak from a packet thread or from inside the actor tick, so its line is queued and the room tick turns the heads
    // before the actors move. A line said during the tick then shows on the following status update, as on Habbo.
    internal void TurnHeadsToward(RoomUser speaker) => _headTurnRequests.Enqueue((speaker.X, speaker.Y, speaker));

    internal void CycleHeadTurns()
    {
        if (_headTurns.Count == 0 && _headTurnRequests.IsEmpty) {
            return;
        }

        _headTurnTick++;

        foreach (var (user, turn) in _headTurns) {
            if (turn.ResetTick > _headTurnTick && user.IsAttachedTo(_room)) {
                continue;
            }

            _headTurns.Remove(user);

            if (user.RotHead == turn.Head && user.RotHead != user.RotBody) {
                user.RotHead = user.RotBody;
                user.UpdateNeeded = true;
            }
        }

        while (_headTurnRequests.TryDequeue(out var request)) {
            _headTurnBatch.Add(request);
        }

        if (_headTurnBatch.Count == 0) {
            return;
        }

        // One pass over the room for every line said since the last tick; a later line wins.
        foreach (var (_, user) in _users) {
            if (user.IsBot) {
                continue;
            }

            foreach (var (x, y, speaker) in _headTurnBatch) {
                if (!ReferenceEquals(user, speaker) && user.LookToward(x, y) is { } head) {
                    _headTurns[user] = (_headTurnTick + HeadTurnTicks, head);
                }
            }
        }

        _headTurnBatch.Clear();
    }

    private void ExpireForwardArrivalEffects()
    {
        foreach (var user in GetUserList()) {
            if (user?.ForwardArrivalEffect is not { } arrival) {
                continue;
            }

            if (arrival.TicksLeft > 1) {
                user.ForwardArrivalEffect = (arrival.TicksLeft - 1, arrival.Restore);

                continue;
            }

            user.ForwardArrivalEffect = null;

            if (user.CurrentEffect == WiredRoomForwarding.TeleportEffectId) {
                user.ApplyEffect(arrival.Restore);
            }
        }
    }

    public void UpdateUserStatus(RoomUser user, bool cyclegameitems)
    {
        if (user == null) {
            return;
        }

        var wasLaying = user.Statusses.ContainsKey("lay");

        try {
            var isBot = user.IsBot;

            if (isBot) {
                cyclegameitems = false;
            }

            UpdateSignStatus(user);
            var itemsOnSquare = _room.GetGameMap().GetAllRoomItemForSquare(user.X, user.Y);
            var model = _room.GetGameMap().Model;
            var walkMagic = _room.GetGameMap().WalkMagicAt(user.X, user.Y);

            if (walkMagic != null) {
                // Height rebinding is callback-free; movement still dispatches the legacy furni hooks.
                user.Statusses.Remove("sit");
                user.Statusses.Remove("lay");
                user.IsSitting = false;
                user.IsLying = false;
                user.Z = walkMagic.GetZ;
                user.UpdateNeeded = true;

                if (!cyclegameitems) {
                    return;
                }
            }

            var hasSeat = walkMagic == null && (model.SqState[user.X, user.Y] == SquareState.Seat || itemsOnSquare.Any(squareItem => squareItem?.Definition?.IsSeat == true));
            var hasBed = walkMagic == null && itemsOnSquare.Any(squareItem => squareItem?.Definition?.InteractionType is InteractionType.Bed or InteractionType.TentSmall);

            if (RoomPosture.ReleaseSit(user.IsSitting, user.Statusses.ContainsKey("sit"), hasSeat)) {
                user.Statusses.Remove("sit");
                user.UpdateNeeded = true;
            }

            if (RoomPosture.ReleaseLay(user.IsLying, user.Statusses.ContainsKey("lay"), hasBed)) {
                user.Statusses.Remove("lay");
                user.UpdateNeeded = true;
            }

            if (user.IsLying || user.IsSitting) {
                return;
            }

            double newZ;

            if (itemsOnSquare != null || itemsOnSquare.Count != 0) {
                if (walkMagic == null && user.RidingHorse && user.IsPet == false) {
                    newZ = _room.GetGameMap().SqAbsoluteHeight(user.X, user.Y, itemsOnSquare.ToList()) + 1;
                }
                else {
                    newZ = _room.GetGameMap().SqAbsoluteHeight(user.X, user.Y, itemsOnSquare.ToList());
                }
            }
            else {
                newZ = 1;
            }

            if (newZ != user.Z) {
                user.Z = newZ;
                user.UpdateNeeded = true;
            }

            if (walkMagic == null && model.SqState[user.X, user.Y] == SquareState.Seat) {
                if (!user.Statusses.ContainsKey("sit")) {
                    user.Statusses.Add("sit", "1.0");
                }

                user.Z = model.SqFloorHeight[user.X, user.Y];
                user.RotHead = model.SqSeatRot[user.X, user.Y];
                user.RotBody = model.SqSeatRot[user.X, user.Y];
                user.UpdateNeeded = true;
            }

            if (itemsOnSquare.Count == 0) {
                user.LastItem = null;
            }

            foreach (var item in itemsOnSquare.ToList()) {
                if (item == null) {
                    continue;
                }

                if (walkMagic == null && item.Definition.IsSeat) {
                    if (!user.Statusses.ContainsKey("sit")) {
                        if (!user.Statusses.ContainsKey("sit")) {
                            user.Statusses.Add("sit", TextHandling.GetString(item.Definition.Height) + " 0");
                        }
                    }

                    user.Z = item.GetZ;
                    user.RotHead = item.Rotation;
                    user.RotBody = item.Rotation;
                    user.UpdateNeeded = true;
                }

                switch (item.Definition.InteractionType) {
                    case InteractionType.Bed:
                    case InteractionType.TentSmall: {
                            if (walkMagic != null) {
                                break;
                            }

                            if (!user.Statusses.ContainsKey("lay")) {
                                user.Statusses.Add("lay", $"{TextHandling.GetString(item.Definition.Height)} null");
                            }

                            user.Z = item.GetZ;
                            user.RotHead = item.Rotation;
                            user.RotBody = item.Rotation;
                            user.UpdateNeeded = true;
                            break;
                        }
                    case InteractionType.Banzaigategreen:
                    case InteractionType.Banzaigateblue:
                    case InteractionType.Banzaigatered:
                    case InteractionType.Banzaigateyellow: {
                            if (cyclegameitems) {
                                var effectId = Convert.ToInt32(item.Team + 32);
                                var t = user.GetClient().GetHabbo().CurrentRoom.GetTeamManagerForBanzai();

                                if (user.Team == Team.None) {
                                    if (t.CanEnterOnTeam(item.Team)) {
                                        if (user.Team != Team.None) {
                                            t.OnUserLeave(user);
                                        }

                                        user.Team = item.Team;
                                        t.AddUser(user);

                                        if (user.GetClient().GetHabbo().Effects.CurrentEffect != effectId) {
                                            user.GetClient().GetHabbo().Effects.ApplyEffect(effectId);
                                        }
                                    }
                                }
                                else if (user.Team != Team.None && user.Team != item.Team) {
                                    t.OnUserLeave(user);
                                    user.Team = Team.None;
                                    user.GetClient().GetHabbo().Effects.ApplyEffect(0);
                                }
                                else {
                                    //usersOnTeam--;
                                    t.OnUserLeave(user);

                                    if (user.GetClient().GetHabbo().Effects.CurrentEffect == effectId) {
                                        user.GetClient().GetHabbo().Effects.ApplyEffect(0);
                                    }

                                    user.Team = Team.None;
                                }
                                //Item.ExtraData = usersOnTeam.ToString();
                                //Item.UpdateState(false, true);
                            }

                            break;
                        }
                    case InteractionType.FreezeYellowGate:
                    case InteractionType.FreezeRedGate:
                    case InteractionType.FreezeGreenGate:
                    case InteractionType.FreezeBlueGate: {
                            if (cyclegameitems) {
                                var effectId = Convert.ToInt32(item.Team + 39);
                                var t = user.GetClient().GetHabbo().CurrentRoom.GetTeamManagerForFreeze();

                                if (user.Team == Team.None) {
                                    if (t.CanEnterOnTeam(item.Team)) {
                                        if (user.Team != Team.None) {
                                            t.OnUserLeave(user);
                                        }

                                        user.Team = item.Team;
                                        t.AddUser(user);

                                        if (user.GetClient().GetHabbo().Effects.CurrentEffect != effectId) {
                                            user.GetClient().GetHabbo().Effects.ApplyEffect(effectId);
                                        }
                                    }
                                }
                                else if (user.Team != Team.None && user.Team != item.Team) {
                                    t.OnUserLeave(user);
                                    user.Team = Team.None;
                                    user.GetClient().GetHabbo().Effects.ApplyEffect(0);
                                }
                                else {
                                    //usersOnTeam--;
                                    t.OnUserLeave(user);

                                    if (user.GetClient().GetHabbo().Effects.CurrentEffect == effectId) {
                                        user.GetClient().GetHabbo().Effects.ApplyEffect(0);
                                    }

                                    user.Team = Team.None;
                                }
                                //Item.ExtraData = usersOnTeam.ToString();
                                //Item.UpdateState(false, true);
                            }

                            break;
                        }
                    case InteractionType.Banzaitele: {
                            if (user.Statusses.ContainsKey("mv")) {
                                _room.GetGameItemHandler().OnTeleportRoomUserEnter(user, item);
                            }

                            break;
                        }
                    case InteractionType.Effect: {
                            if (user == null) {
                                return;
                            }

                            if (!user.IsBot) {
                                if (item == null || item.Definition == null || user.GetClient() == null || user.GetClient().GetHabbo() == null || user.GetClient().GetHabbo().Effects == null) {
                                    return;
                                }

                                if (item.Definition.EffectId == 0 && user.GetClient().GetHabbo().Effects.CurrentEffect == 0) {
                                    return;
                                }

                                user.GetClient().GetHabbo().Effects.ApplyEffect(item.Definition.EffectId);
                                item.LegacyDataString = "1";
                                item.UpdateState(false, true);
                                item.RequestUpdate(2, true);
                            }

                            break;
                        }
                    case InteractionType.Arrow: {
                            if (user.GoalX == item.GetX && user.GoalY == item.GetY) {
                                if (user == null || user.GetClient() == null || user.GetClient().GetHabbo() == null) {
                                    continue;
                                }

                                var room = user.GetClient().GetHabbo().CurrentRoom;

                                if (room == null) {
                                    return;
                                }

                                if (!ItemTeleporterFinder.IsTeleLinked(item.Id, room, _travelStore)) {
                                    user.UnlockWalking();
                                }
                                else {
                                    var linkedTele = _travelStore.FindLinkedTeleporter(item.Id);
                                    var teleRoomId = ItemTeleporterFinder.GetTeleRoomId(linkedTele, room, _travelStore);

                                    if (teleRoomId == room.RoomId) {
                                        var targetItem = room.GetRoomItemHandler().GetItem(linkedTele);

                                        if (targetItem == null) {
                                            if (user.GetClient() != null) {
                                                user.GetClient().SendWhisper("Hey, that arrow is poorly!");
                                            }

                                            return;
                                        }

                                        room.GetGameMap().TeleportToItem(user, targetItem);
                                    }
                                    else if (teleRoomId != room.RoomId) {
                                        if (user != null && !user.IsBot && user.GetClient() != null && user.GetClient().GetHabbo() != null) {
                                            user.GetClient().GetHabbo().IsTeleporting = true;
                                            user.GetClient().GetHabbo().TeleportingRoomId = teleRoomId;
                                            user.GetClient().GetHabbo().TeleporterId = linkedTele;
                                            user.GetClient().GetHabbo().PrepareRoom(teleRoomId, "");
                                        }
                                    }
                                    else if (_room.GetRoomItemHandler().GetItem(linkedTele) != null) {
                                        user.SetPos(item.GetX, item.GetY, item.GetZ);
                                        user.SetRot(item.Rotation, false);
                                    }
                                    else {
                                        user.UnlockWalking();
                                    }
                                }
                            }

                            break;
                        }
                }
            }

            if (cyclegameitems) {
                if (_room.GotSoccer()) {
                    _room.GetSoccer().OnUserWalk(user);
                }

                if (_room.GotBanzai()) {
                    _room.GetBanzai().OnUserWalk(user);
                }

                if (_room.GotFreeze()) {
                    _room.GetFreeze().OnUserWalk(user);
                }
            }
        }
        catch (Exception e) {
            ExceptionLogger.LogException(e);
        }
        finally {
            if (!wasLaying && user.Statusses.ContainsKey("lay")) {
                _room.GetWired().Dispatch(new(WiredEventKind.AvatarAction) { Actor = user, Action = (int)WiredAvatarAction.Lay });
            }
        }
    }

    internal void UpdateSignStatus(RoomUser user)
    {
        if (user.RecentExpression is { } expression && _clock.GetUtcNow() >= expression.ExpiresAt) {
            user.RecentExpression = null;
        }

        if (user.SignExpiresAt is not { } deadline) {
            return;
        }

        var now = _clock.GetUtcNow();

        if (now < deadline) {
            return;
        }

        user.SignExpiresAt = null;
        user.HeldSign = null;

        if (user.HasStatus("sign")) {
            user.RemoveStatus("sign");
            user.UpdateNeeded = true;
        }
    }

    public ICollection<RoomUser> GetUserList() => _users.Values;

    public void Dispose()
    {
        var room = _room;

        if (room == null) {
            return;
        }

        // Match the tick's room-owner -> stress-request lock order.
        lock (room.NavigationSync)
            lock (_stressSync) {
                if (_disposed) {
                    return;
                }

                _disposed = true;
                _stressRequests.Clear();
                DisposeUsers();
            }
    }

    private void DisposeUsers()
    {
        var users = _users.Values.ToArray();

        try {
            _room.GetGameMap()?.Navigation?.Shutdown();

            foreach (var user in users) {
                _room.IceTagLeave(user);
                _room.GetWired()?.BeforeActorLeaves(user);
            }

            // Keep each save independent so a failed write cannot skip the remaining teardown.
            try {
                UpdatePets();
            }
            catch (Exception e) {
                ExceptionLogger.LogCriticalException(e);
            }

            try {
                UpdateBots();
            }
            catch (Exception e) {
                ExceptionLogger.LogCriticalException(e);
            }

            _room.UsersNow = 0;

            try {
                _store.UpdateUserCount(_room.Id, 0);
            }
            catch (Exception e) {
                ExceptionLogger.LogCriticalException(e);
            }
        }
        finally {
            _users.Clear();
            _pets.Clear();
            _bots.Clear();
            UserCount = 0;
            PetCount = 0;

            foreach (var user in users) {
                user.BotAi?.Detach(_room, user);
                user.PetData?.Detach(_room);
                user.Dispose();
            }

            _users = null;
            _pets = null;
            _bots = null;
            _room = null;
        }
    }
}
