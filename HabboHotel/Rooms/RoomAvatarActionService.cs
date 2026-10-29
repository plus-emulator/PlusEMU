using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Quests.DailyTasks;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Subscriptions;

namespace Plus.HabboHotel.Rooms;

public interface IRoomAvatarActionService
{
    void Move(GameClient session, int x, int y);
    void GiveHandItem(Room room, GameClient session, int userId);
    void PerformAction(Room room, GameClient session, int action);
    void Dance(Room room, GameClient session, int danceId);
    void SetPosture(GameClient session, int posture);
    void LookTo(Room room, GameClient session, int x, int y);
    void SetTyping(GameClient session, bool typing);
    void ApplySign(Room room, GameClient session, int signId);
}

public sealed class RoomAvatarActionService(TimeProvider clock, IQuestManager questManager, IRewardTrackManager rewards) : IRoomAvatarActionService
{
    private static readonly TimeSpan SignDuration = TimeSpan.FromSeconds(5);

    public void Move(GameClient session, int x, int y)
    {
        var habbo = session.GetHabbo();
        var room = habbo.CurrentRoom;

        if (!habbo.InRoom || room == null) {
            return;
        }

        var user = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);

        if (user == null || !room.GetGameMap().ValidTile(x, y)) {
            return;
        }

        if (!user.IsBot) {
            room.GetWired().Dispatch(new(WiredEventKind.ClickTile) { Actor = user, X = x, Y = y });
        }

        if (!user.CanWalk) {
            return;
        }

        // A walk to another tile wakes a sleeping avatar at once; a walk to the tile it stands on wakes it on the next room tick.
        if (!user.SetStep && x == user.X && y == user.Y) {
            user.UnIdle(atRoomTick: true);

            return;
        }

        user.UnIdle();
        user.MoveTo(x, y);

        return;
    }

    public void GiveHandItem(Room room, GameClient session, int userId)
    {
        var habbo = session.GetHabbo();

        if (!ReferenceEquals(habbo.CurrentRoom, room)) {
            return;
        }

        var users = room.GetRoomUserManager();
        var actor = users.GetRoomUserByHabbo(habbo.Id);
        var target = users.GetRoomUserByHabbo(userId);

        if (actor == null || target == null) {
            return;
        }

        if ((Math.Abs(actor.X - target.X) >= 3 || Math.Abs(actor.Y - target.Y) >= 3)
            && !habbo.Access.Can(PermissionKeys.ModerationTool)) {
            return;
        }

        if (actor.CarryItemId <= 0 || actor.CarryTimer <= 0) {
            return;
        }

        if (actor.CarryItemId == 8) {
            questManager.ProgressUserQuest(session, QuestType.GiveCoffee);
        }

        target.CarryItem(actor.CarryItemId);
        actor.CarryItem(0);
        target.DanceId = 0;
    }

    private sealed record ExpressionRule(WiredAvatarAction? Wired = null, TimeSpan? CountsFor = null, bool ClubOnly = false);

    // Without Habbo Club, expressions 2, 3, 4, 6 and 67 reach nobody. User Performs Action sees a wave for about 5 s and respect for 2 s.
    private static readonly Dictionary<int, ExpressionRule> Expressions = new()
    {
        [1] = new(WiredAvatarAction.Wave, TimeSpan.FromSeconds(5)),
        [2] = new(WiredAvatarAction.Kiss, ClubOnly: true),
        [3] = new(WiredAvatarAction.Laugh, ClubOnly: true),
        [4] = new(ClubOnly: true),
        [5] = new(WiredAvatarAction.Relax),
        [6] = new(ClubOnly: true),
        // The respect expression fires User Performs Action - Respect. Expression 67 fires nothing.
        [7] = new(WiredAvatarAction.Respect, TimeSpan.FromSeconds(2)),
        [67] = new(ClubOnly: true)
    };

    public void PerformAction(Room room, GameClient session, int action)
    {
        var rule = Expressions.GetValueOrDefault(action) ?? new();

        if (rule.ClubOnly && ClubAccess.LevelFor(session.GetHabbo().Access) == 0) {
            return;
        }

        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (user == null) {
            return;
        }

        if (user.DanceId > 0) {
            user.DanceId = 0;
            room.SendPacket(new DanceComposer(user.VirtualId, 0));
        }

        user.UnIdle(atRoomTick: true);
        room.SendPacket(new ActionComposer(user.VirtualId, action));

        if (action == 5) {
            user.WakePending = false;

            if (!user.IsAsleep) {
                user.IsAsleep = true;
                room.SendPacket(new SleepComposer(user.VirtualId, true));
            }
        }

        user.RecentExpression = rule is { Wired: { } remembered, CountsFor: { } window } ? ((int)remembered, clock.GetUtcNow() + window) : null;

        if (rule.Wired is { } wiredAction) {
            room.GetWired().Dispatch(new(WiredEventKind.AvatarAction)
            { Actor = user, Action = (int)wiredAction, Code = -1 });
        }

        if (action == 1) {
            rewards.Progress(session, RewardTrackActions.Wave);
        }

        questManager.ProgressUserQuest(session, QuestType.SocialWave);
    }

    public void Dance(Room room, GameClient session, int danceId)
    {
        // Without Habbo Club, dance 2 to 4 sends nothing and the current dance stays.
        if (danceId is > 1 and <= 4 && ClubAccess.LevelFor(session.GetHabbo().Access) == 0) {
            return;
        }

        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (user == null) {
            return;
        }

        user.UnIdle(atRoomTick: true);

        if (danceId < 0 || danceId > 4) {
            danceId = 0;
        }

        if (danceId > 0 && user.CarryItemId > 0) {
            user.CarryItem(0);
        }

        var previousDance = user.DanceId;
        user.DanceId = danceId;
        room.SendPacket(new DanceComposer(user.VirtualId, danceId));

        if (danceId > 0) {
            room.GetWired().Dispatch(new(WiredEventKind.AvatarAction)
            { Actor = user, Action = (int)WiredAvatarAction.Dance, Code = danceId });
        }

        if (danceId >= 1 && danceId <= 4 && danceId != previousDance) {
            rewards.Progress(session, RewardTrackActions.Dance);
        }

        questManager.ProgressUserQuest(session, QuestType.SocialDance);

        if (room.GetRoomUserManager().GetRoomUsers().Count > 19) {
            questManager.ProgressUserQuest(session, QuestType.MassDance);
        }
    }

    public void SetPosture(GameClient session, int posture)
    {
        if (!session.GetHabbo().InRoom || posture is not (0 or 1)) {
            return;
        }

        var room = session.GetHabbo().CurrentRoom;
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (user == null || user.Statusses.ContainsKey("lie") || user.IsLying || user.RidingHorse || user.IsSwimming) {
            return;
        }

        // The last request of a room tick decides; the posture changes on the tick.
        user.PostureRequest = posture;
    }

    public void LookTo(Room room, GameClient session, int x, int y)
    {
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (user == null || user.IsAsleep) {
            return;
        }

        user.UnIdle();

        if (user.TurnToward(x, y)) {
            room.IceTagLookTo(user, x, y);
        }
    }

    public void SetTyping(GameClient session, bool typing)
    {
        if (!session.GetHabbo().InRoom) {
            return;
        }

        var room = session.GetHabbo().CurrentRoom;

        if (room == null) {
            return;
        }

        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Username);

        if (user == null) {
            return;
        }

        room.SendPacket(new UserTypingComposer(user.VirtualId, typing));
    }

    public void ApplySign(Room room, GameClient session, int signId)
    {
        if (signId is < 0 or > 17) {
            return;
        }

        var habbo = session.GetHabbo();

        if (habbo.CurrentRoom != room) {
            return;
        }

        var user = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);

        if (user == null) {
            return;
        }

        var now = clock.GetUtcNow();
        user.UnIdle(atRoomTick: true);
        user.ShowSign(signId, now + SignDuration);
        room.GetWired().Dispatch(new(WiredEventKind.AvatarAction)
        { Actor = user, Action = (int)WiredAvatarAction.Sign, Code = signId });
        rewards.Progress(session, DailyTaskActions.ShowSign, 1, signId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
