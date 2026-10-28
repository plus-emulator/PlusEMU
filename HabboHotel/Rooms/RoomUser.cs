using System.Diagnostics.CodeAnalysis;
using Plus.HabboHotel.Permissions;
using System.Drawing;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.Chat.Emotions;
using Plus.HabboHotel.Rooms.Games.Freeze;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms;

public sealed record FootballGateOutfit(string BaseLook, string Look);

public class RoomUser
{
    private ActorMovementState? _movement;
    public ActorMovementState Movement => LazyInitializer.EnsureInitialized(ref _movement)!;

    public WiredRoomEntrySnapshot WiredRoomEntry { get; internal set; }

    /// <summary>A user forwarded by Teleport to Room glows for TicksLeft more room ticks, then wears Restore again.</summary>
    internal (int TicksLeft, int Restore)? ForwardArrivalEffect { get; set; }

    /// <summary>The look a football gate put on, kept only while the user's own look is still the one it was made from.</summary>
    public FootballGateOutfit? FootballGateOutfit { get; set; }

    /// <summary>The look shown in this room: a football gate's outfit, or the user's own look.</summary>
    public string LookFor(Users.Habbo habbo) => FootballGateOutfit is { } outfit && outfit.BaseLook == habbo.Look ? outfit.Look : habbo.Look;
    private GameClient? _mClient;
    private Room? _mRoom;
    private readonly IChatEmotionsManager _chatEmotions;
    private readonly IRewardTrackManager _rewards;

    public bool AllowOverride;

    public FreezePowerUp BanzaiPowerUp;
    public BotAi? BotAi;
    public RoomBot? BotData;
    public bool CanWalk;
    public int CarryItemId; //byte
    public int CarryTimer; //byte
    public int ChatSpamCount;
    public int ChatSpamTicks = 16;
    public ItemEffectType CurrentItemEffect;
    public int DanceId;
    public bool FastWalking = false;
    public int FreezeCounter;
    public bool Freezed;
    public bool FreezeInteracting;
    public int FreezeLives;
    public bool Frozen;
    public uint GateId;

    public int GoalX; //byte
    public int GoalY; //byte
    public int HabboId;
    public int HorseId = 0;
    public int IdleTime; //byte
    public bool InteractingGate;
    public int InternalRoomId;
    public bool IsAsleep;
    public bool IsJumping;
    public bool IsLying = false;

    public bool IsRolling = false;
    public bool IsSitting = false;
    public bool IsWalking;
    public int LastBubble = 0;
    public DateTimeOffset? LastInteractionAt;
    public Item? LastItem = null;

    public int LlPartner = 0;
    public int LockedTilesCount;
    public bool MoonwalkEnabled = false;

    public bool PathRecalcNeeded;
    public Pet? PetData;

    public int PrevTime;
    public bool RidingHorse = false;

    // The swim effects. A swimming avatar cannot sit or lay.
    private static readonly int[] SwimEffects = [29, 30, 185];
    public bool IsSwimming => !IsBot && GetClient()?.GetHabbo()?.Effects is { } effects && SwimEffects.Contains(effects.CurrentEffect);
    public int RollerDelay = 0;
    internal bool RollerSettling;
    internal PendingRollerLanding? PendingLanding;
    public uint RoomId;
    public int RotBody; //byte
    public int RotHead; //byte

    public bool SetStep;
    public int SetX; //byte
    public int SetY; //byte
    public double SetZ;
    // Absolute Z set by :bh; furniture this user places or moves lands at it, ignoring the stack below.
    public double? BuildHeight;
    public bool ShieldActive;
    public int ShieldCounter;
    // The sign Wired still sees until SignExpiresAt; its "sign" status goes out in one status update only.
    public int? HeldSign;

    /// <summary>
    /// The last Wave or Respect expression and when Wired stops seeing it. The condition "user performs action" passes for these two shortly after the expression, though no status of them is left.
    /// </summary>
    internal (int Action, DateTimeOffset ExpiresAt)? RecentExpression { get; set; }
    public DateTimeOffset? SignExpiresAt;
    public bool SuperFastWalking = false;
    private sealed record TeamMembership(Team Value, long Revision);
    private TeamMembership? _teamMembership;
    public Team Team
    {
        get => Volatile.Read(ref _teamMembership)?.Value ?? Team.None;
        set
        {
            TeamMembership? previous;
            TeamMembership next;

            do {
                previous = Volatile.Read(ref _teamMembership);

                if ((previous?.Value ?? Team.None) == value) {
                    return;
                }

                next = new(value, checked((previous?.Revision ?? 0) + 1));
            } while (!ReferenceEquals(Interlocked.CompareExchange(ref _teamMembership, next, previous), previous));
        }
    }
    internal long TeamRevision => Volatile.Read(ref _teamMembership)?.Revision ?? 0;
    public int TeleDelay; //byte
    public bool TeleportEnabled;
    public double TimeInRoom;
    public bool UpdateNeeded;
    public int UserId;
    public int VirtualId;
    /// <summary>The room's <see cref="RoomUserManager.Visits"/> right after this user came in; 0 when unknown.</summary>
    internal int Visit { get; init; }

    public int X; //byte
    public int Y; //byte
    public double Z;

    public RoomUser(int habboId, uint roomId, int virtualId, Room room, GameClient? client,
        IChatEmotionsManager chatEmotions, IRewardTrackManager rewards)
    {
        Freezed = false;
        HabboId = habboId;
        RoomId = roomId;
        VirtualId = virtualId;
        IdleTime = 0;
        X = 0;
        Y = 0;
        Z = 0;
        PrevTime = 0;
        RotHead = 0;
        RotBody = 0;
        UpdateNeeded = true;
        Statusses = new();
        TeleDelay = -1;
        _mRoom = room;
        _mClient = client;
        _chatEmotions = chatEmotions;
        _rewards = rewards;
        AllowOverride = false;
        CanWalk = true;
        InternalRoomId = 0;
        CurrentItemEffect = ItemEffectType.None;
        FreezeLives = 0;
        InteractingGate = false;
        GateId = 0;
        LastInteractionAt = null;
        LockedTilesCount = 0;
        IsJumping = false;
        TimeInRoom = 0;
        TradeId = 0;
        TradePartner = 0;
        IsTrading = false;
    }


    public Point Coordinate => new(X, Y);

    public bool IsPet => IsBot && BotData.IsPet;

    // A bot has no effects component; its effect is the last one sent for it.
    private int _botEffect;
    public int CurrentEffect => IsBot ? _botEffect : GetClient()?.GetHabbo()?.Effects?.CurrentEffect ?? 0;


    public bool IsDancing
    {
        get
        {
            if (DanceId >= 1) {
                return true;
            }

            return false;
        }
    }

    public bool IsTrading { get; set; }

    public int TradePartner { get; set; }

    public int TradeId { get; set; }


    public Dictionary<string, string> Statusses { get; }
    //set { this._statusses = value; }

    public bool NeedsAutokick
    {
        get
        {
            if (IsBot) {
                return false;
            }

            if (GetClient()?.GetHabbo() is not { } habbo) {
                return true;
            }

            if (habbo.Access.Can(PermissionKeys.ModerationTool) || GetRoom().OwnerId == HabboId) {
                return false;
            }

            if (GetRoom().Id == 1649919) {
                return false;
            }

            if (IdleTime >= 7200) {
                return true;
            }

            return false;
        }
    }

    [MemberNotNullWhen(true, nameof(BotData))]
    public bool IsBot
    {
        get
        {
            if (BotData != null) {
                return true;
            }

            return false;
        }
    }

    public Point SquareInFront
    {
        get
        {
            var sq = new Point(X, Y);

            if (RotBody == 0) {
                sq.Y--;
            }
            else if (RotBody == 2) {
                sq.X++;
            }
            else if (RotBody == 4) {
                sq.Y++;
            }
            else if (RotBody == 6) {
                sq.X--;
            }

            return sq;
        }
    }

    public Point SquareBehind
    {
        get
        {
            var sq = new Point(X, Y);

            if (RotBody == 0) {
                sq.Y++;
            }
            else if (RotBody == 2) {
                sq.X--;
            }
            else if (RotBody == 4) {
                sq.Y--;
            }
            else if (RotBody == 6) {
                sq.X++;
            }

            return sq;
        }
    }

    public Point SquareLeft
    {
        get
        {
            var sq = new Point(X, Y);

            if (RotBody == 0) {
                sq.X++;
            }
            else if (RotBody == 2) {
                sq.Y--;
            }
            else if (RotBody == 4) {
                sq.X--;
            }
            else if (RotBody == 6) {
                sq.Y++;
            }

            return sq;
        }
    }

    public Point SquareRight
    {
        get
        {
            var sq = new Point(X, Y);

            if (RotBody == 0) {
                sq.X--;
            }
            else if (RotBody == 2) {
                sq.Y++;
            }
            else if (RotBody == 4) {
                sq.X++;
            }
            else if (RotBody == 6) {
                sq.Y--;
            }

            return sq;
        }
    }

    public string GetUsername()
    {
        if (IsBot) {
            return string.Empty;
        }

        return GetClient()?.GetHabbo()?.Username ?? "Unknown User";
    }

    /// <summary>
    /// A sleeping avatar that talks, waves, signs or dances, or is spoken to by a Show Message, wakes on the next room tick (<paramref name="atRoomTick"/>);
    /// a walk to another tile wakes it at once.
    /// </summary>
    public void UnIdle(bool atRoomTick = false)
    {
        var room = GetRoom();

        if (room == null) {
            return;
        }

        if (!IsBot) {
            var habbo = GetClient()?.GetHabbo();

            if (habbo != null) {
                habbo.TimeAfk = 0;
            }
        }

        IdleTime = 0;

        if (!IsAsleep) {
            return;
        }

        if (atRoomTick) {
            WakePending = true;

            return;
        }

        WakeUp(room);
    }

    /// <summary>The avatar wakes on the next room tick, after an action (see <see cref="UnIdle"/>).</summary>
    public bool WakePending;

    public void CompleteWake()
    {
        if (WakePending && GetRoom() is { } room) {
            WakeUp(room);
        }
    }

    private void WakeUp(Room room)
    {
        WakePending = false;

        if (!IsAsleep) {
            return;
        }

        IsAsleep = false;
        room.SendPacket(new SleepComposer(VirtualId, false));
        room.GetWired().Dispatch(new(WiredEventKind.AvatarAction) { Actor = this, Action = (int)WiredAvatarAction.Awake });
    }

    public void Dispose()
    {
        Statusses.Clear();
        Interlocked.Exchange(ref _mRoom, null);
        Interlocked.Exchange(ref _mClient, null);
    }

    public void Chat(string message, int colour = 0)
    {
        var room = GetRoom();

        if (room == null || !IsBot) {
            return;
        }

        var style = IsPet ? 0 : colour == 0 ? 2 : colour;

        room.SendChat(receiver => new ChatComposer(VirtualId, message, 0, style, receiver),
            user => user.GetClient()?.GetHabbo() is { } habbo && !(IsPet ? habbo.AllowPetSpeech : habbo.AllowBotSpeech));

        if (!IsPet) {
            room.GetRoomUserManager().TurnHeadsToward(this);
        }
    }

    /// <summary>
    /// A standing avatar turns its head one step toward a speaking bot or user that is at most two steps from where its body faces, or straight to it when it is one step away.
    /// </summary>
    internal int? LookToward(int x, int y)
    {
        if (IsWalking || Statusses.ContainsKey("sit") || Statusses.ContainsKey("lay")) {
            return null;
        }

        var steps = (Rotation.Calculate(X, Y, x, y) - RotBody + 12) % 8 - 4;

        if (Math.Abs(steps) > 2) {
            return null;
        }

        var head = (RotBody + Math.Sign(steps) + 8) % 8;

        if (RotHead != head) {
            RotHead = head;
            UpdateNeeded = true;
        }

        return head;
    }

    public void HandleSpamTicks()
    {
        if (ChatSpamTicks >= 0) {
            ChatSpamTicks--;

            if (ChatSpamTicks == -1) {
                ChatSpamCount = 0;
            }
        }
    }

    public const int ChatBurst = 2;
    public const long ChatRefillMilliseconds = 600;
    private int _chatTokens = ChatBurst;
    private long? _chatSlot;

    /// <summary>
    /// The chat budget: a bucket of <see cref="ChatBurst"/> lines that gains one line on every <see cref="ChatRefillMilliseconds"/> step of
    /// the clock. A line over the budget is dropped without a mute. Says 600 ms apart all go out; 500 ms apart drop every sixth after the first eight; 400 ms apart drop every third after the first six;
    /// 150 ms apart keep the first three and then one per 600 ms. No flood-control message comes in any of them.
    /// </summary>
    public bool IncrementAndCheckFlood(DateTimeOffset now, out int muteTime)
    {
        muteTime = 0;
        var habbo = GetClient()?.GetHabbo();

        if (habbo == null || !ReferenceEquals(habbo.CurrentRoom, GetRoom())) {
            return false;
        }

        var slot = now.ToUnixTimeMilliseconds() / ChatRefillMilliseconds;

        if (_chatSlot is { } last && slot > last) {
            _chatTokens = (int)Math.Min(ChatBurst, _chatTokens + (slot - last));
        }

        _chatSlot = slot;

        if (_chatTokens < 1) {
            return true;
        }

        _chatTokens--;

        return false;
    }

    public void OnChat(int colour, string message, bool shout)
    {
        var room = GetRoom();
        var client = GetClient();
        var habbo = client?.GetHabbo();

        if (room == null || client == null || habbo == null || !ReferenceEquals(habbo.CurrentRoom, room)) {
            return;
        }

        if (room.GetWired().TriggerEvent(WiredBoxType.TriggerUserSays, habbo, message, colour, shout)) {
            room.GetWired().FinishSpeech();

            return;
        }

        // "k 5 " does not fire "k #(a)" and is echoed as "k 5".
        message = message.TrimEnd(' ');
        habbo.HasSpoken = true;

        if (room.WordFilterList.Count > 0 && !habbo.Access.Can(PermissionKeys.ChatFilterBypass)) {
            message = room.GetFilter().CheckMessage(message);
        }

        var gesture = _chatEmotions.GetEmotionsForText(message);
        IServerPacket Packet(int receiver) => shout
            ? new ShoutComposer(VirtualId, message, gesture, colour, receiver)
            : new ChatComposer(VirtualId, message, gesture, colour, receiver);

        if (habbo.TentId > 0) {
            room.SendToTent(habbo.Id, habbo.TentId, Packet);
            var toNotify = room.GetRoomUserManager().GetRoomUsersWithPermission(PermissionKeys.StaffReceiveAlerts);

            if (toNotify.Count > 0) {
                foreach (var user in toNotify) {
                    if (user == null || user.GetClient() == null || user.GetClient().GetHabbo() == null ||
                        user.GetClient().GetHabbo().TentId == habbo.TentId) {
                        continue;
                    }

                    user.GetClient().Send(new WhisperComposer(VirtualId, $"[Tent Chat] {message}", 0, colour, user.VirtualId));
                }
            }
        }
        else {
            foreach (var user in room.GetRoomUserManager().GetRoomUsers().ToList()) {
                if (user == null || user.GetClient() == null || user.GetClient().GetHabbo() == null || user.GetClient().GetHabbo().IgnoresComponent.IsIgnored(habbo.Id)) {
                    continue;
                }

                if (room.ChatDistance > 0 && Gamemap.TileDistance(X, Y, user.X, user.Y) > room.ChatDistance) {
                    continue;
                }

                user.GetClient().Send(Packet(user.VirtualId));
            }

            room.GetRoomUserManager().TurnHeadsToward(this);
        }

        if (shout) {
            foreach (var user in room.GetRoomUserManager().GetUserList().ToList()) {
                if (!user.IsBot) {
                    continue;
                }

                // A placed bot always has its AI; the reaction is skipped only if it does not.
                user.BotAi?.OnUserShout(this, message);
            }
        }
        else {
            foreach (var user in room.GetRoomUserManager().GetUserList().ToList()) {
                if (!user.IsBot) {
                    continue;
                }

                user.BotAi?.OnUserSay(this, message);
            }
        }

        room.GetWired().FinishSpeech();
    }

    public void ClearMovement(bool update)
    {
        GetRoom()?.GetGameMap().Navigation.Cancel(this);
    }

    public void MoveTo(Point c)
    {
        MoveTo(c.X, c.Y);
    }

    public void MoveTo(int pX, int pY, bool pOverride)
    {
        GetRoom()?.GetGameMap().Navigation.Move(this, pX, pY, IsBot ? MoveOrigin.Bot : MoveOrigin.User,
                        TeleportEnabled ? MoveFlags.Teleport : MoveFlags.None);
    }

    public void MoveTo(int pX, int pY)
    {
        MoveTo(pX, pY, false);
    }

    // Walks to the item's approach tile; under v2 with approach_auto_interact the arrival starts the interaction.
    public void ApproachItem(Item item, int actionKind)
    {
        var room = GetRoom();

        if (room == null) {
            return;
        }

        var front = item.SquareInFront;

        if (!IsBot && !TeleportEnabled && room.GetGameMap()?.Navigation is { Settings.ApproachAutoInteract: true } navigation
            && navigation.DescribeApproach(item, actionKind) is { } approach) {
            navigation.Move(this, front.X, front.Y, MoveOrigin.User, MoveFlags.None, approach);
        }
        else {
            MoveTo(front);
        }
    }

    public void MoveTo(int x, int y, MoveOrigin origin, MoveFlags flags = MoveFlags.None)
    {
        GetRoom()?.GetGameMap().Navigation.Move(this, x, y, origin, flags);
    }

    internal void InitializePosition(int x, int y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public void UnlockWalking()
    {
        AllowOverride = false;
        CanWalk = true;
    }


    public void SetPos(int pX, int pY, double pZ)
    {
        GetRoom()?.GetGameMap().Navigation.ForcePlace(this, pX, pY, pZ, ForceResolution.ExactZ);
    }

    public void CarryItem(int item)
    {
        var room = GetRoom();

        if (room == null) {
            return;
        }

        var previous = CarryItemId;
        CarryItemId = item;

        if (item > 0) {
            CarryTimer = 240;
        }
        else {
            CarryTimer = 0;
        }

        room.SendPacket(new CarryObjectComposer(VirtualId, item));
        var client = GetClient();

        if (item > 0 && item != previous && !IsBot && client != null) {
            _rewards.Progress(client, RewardTrackActions.FindHandItem, 1, item.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }


    /// <summary>Turns a standing avatar to face a tile, the way a look-at does. Returns false when it cannot turn.</summary>
    internal bool TurnToward(int x, int y)
    {
        if (x == X && y == Y || IsWalking || RidingHorse) {
            return false;
        }

        SetRot(Rotation.Calculate(X, Y, x, y), false);
        UpdateNeeded = true;

        return true;
    }

    public void SetRot(int rotation, bool headOnly)
    {
        if (Statusses.ContainsKey("lay") || IsWalking) {
            return;
        }

        var diff = RotBody - rotation;
        RotHead = RotBody;

        if (Statusses.ContainsKey("sit") || headOnly) {
            if (RotBody == 2 || RotBody == 4) {
                if (diff > 0) {
                    RotHead = RotBody - 1;
                }
                else if (diff < 0) {
                    RotHead = RotBody + 1;
                }
            }
            else if (RotBody == 0 || RotBody == 6) {
                if (diff > 0) {
                    RotHead = RotBody - 1;
                }
                else if (diff < 0) {
                    RotHead = RotBody + 1;
                }
            }
        }
        else if (diff <= -2 || diff >= 2) {
            RotHead = rotation;
            RotBody = rotation;
        }
        else {
            RotHead = rotation;
        }

        UpdateNeeded = true;
    }


    /// <summary>
    /// The "wf" status of a wired move lasts as long as the animation of the last move. The first status update after that clears it.
    /// "/flatctrl 4/wf//" goes on the status updates inside the animation. A move without animation sets nothing.
    /// </summary>
    internal DateTimeOffset? WiredStatusUntil { get; set; }

    internal void MarkWiredMove(DateTimeOffset until)
    {
        SetStatus("wf");
        WiredStatusUntil = until;
        UpdateNeeded = true;
    }

    /// <summary>
    /// Set when the "sign" status has gone out once; the next status update clears it. The sign is in one status update and gone from the next one, about 500 ms later.
    /// </summary>
    internal bool SignStatusSent { get; set; }

    internal void ShowSign(int signId, DateTimeOffset expiresAt)
    {
        SetStatus("sign", signId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SignStatusSent = false;
        HeldSign = signId;
        SignExpiresAt = expiresAt;
        UpdateNeeded = true;
    }

    public bool HasStatus(string key) => Statusses.ContainsKey(key);

    public void RemoveStatus(string key)
    {
        if (HasStatus(key)) {
            Statusses.Remove(key);
        }
    }

    public void SetStatus(string key, string value = "")
    {
        if (Statusses.ContainsKey(key)) {
            Statusses[key] = value;
        }
        else {
            Statusses.Add(key, value);
        }
    }


    public void ApplyEffect(int effectId)
    {
        var room = GetRoom();

        if (room == null) {
            return;
        }

        if (IsBot) {
            _botEffect = effectId;
            room.SendPacket(new AvatarEffectComposer(VirtualId, effectId));

            return;
        }

        var effects = GetClient()?.GetHabbo()?.Effects;

        if (effects == null) {
            return;
        }

        effects.ApplyEffect(effectId);
    }


    public GameClient? GetClient() => IsBot ? null : _mClient;

    /// <summary>The reward tracks this user progresses, for wired boxes that progress them.</summary>
    internal IRewardTrackManager RewardTracks => _rewards;

    internal bool IsAttachedTo(Room room) => ReferenceEquals(_mRoom, room);

    private Room? GetRoom() => _mRoom;

    // The room stays set until Dispose, also after the session left it.
    internal Room? AttachedRoom => _mRoom;
}
