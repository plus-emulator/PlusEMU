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

    // Observed on AIR WIN63-202610201032-107964540: without Habbo Club, expressions 2, 3, 4, 6 and 67 reach nobody. User Performs
    // Action still sees a wave 4.90 s after it but not 5.27 s after it, and respect 1.69 s after it but not 1.87 s after it.
    private static readonly Dictionary<int, ExpressionRule> Expressions = new()
    {
        [1] = new(WiredAvatarAction.Wave, TimeSpan.FromSeconds(5)),
        [2] = new(WiredAvatarAction.Kiss, ClubOnly: true),
        [3] = new(WiredAvatarAction.Laugh, ClubOnly: true),
        [4] = new(ClubOnly: true),
        // Observed on AIR WIN63-202610201032-107964540: the sleep expression still counts 2.50 s after it, though the avatar stays asleep.
        // Observed on AIR WIN63-202610151745-318700621: it no longer counts 2.77 s after it.
        [5] = new(WiredAvatarAction.Relax, TimeSpan.FromMilliseconds(2600)),
        [6] = new(ClubOnly: true),
        // Observed on AIR WIN63-202610201032-107964540: the respect expression fires User Performs Action - Respect. Expression 67 fires nothing.
        [7] = new(WiredAvatarAction.Respect, TimeSpan.FromMilliseconds(1800)),
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
        }

        user.UnIdle(atRoomTick: true);
        room.SendPacket(new ActionComposer(user.VirtualId, action));

        if (action == 5) {
            user.WakePending = false;
            user.IsAsleep = true;
            room.SendPacket(new SleepComposer(user.VirtualId, true));
        }

        if (rule is { Wired: { } remembered, CountsFor: { } window }) {
            user.RecentExpression = ((int)remembered, clock.GetUtcNow() + window);
        }

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
        // Observed on AIR WIN63-202610201032-107964540: without Habbo Club, dance 2 to 4 sends nothing and the current dance stays.
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

        if (user == null || user.Statusses.ContainsKey("lie") || user.IsLying || user.RidingHorse || user.IsWalking || user.IsSwimming) {
            return;
        }

        var wasSitting = user.IsSitting;

        if (posture == 1 && !user.Statusses.ContainsKey("sit")) {
            // Observed on AIR WIN63-202610201032-107964540: sitting from a diagonal turns the body one step clockwise
            // and the head follows it (3 -> 4, 5 -> 6, 7 -> 0).
            if (user.RotBody % 2 != 0) {
                user.RotBody = (user.RotBody + 1) % 8;
                user.RotHead = user.RotBody;
            }

            // Observed on hhus (AIR WIN63-202610131318-860371703): sitting on the floor sends "sit 0.55 1" (sit height, can stand up)
            // and keeps the avatar's Z.
            user.Statusses.Add("sit", "0.55 1");
            user.IsSitting = true;
            user.UpdateNeeded = true;
        }
        else if (posture == 0 && user.IsSitting) {
            user.Statusses.Remove("sit");
            user.IsSitting = false;
            user.UpdateNeeded = true;
        }

        if (wasSitting != user.IsSitting) {
            room.GetWired().Dispatch(new(WiredEventKind.AvatarAction)
            {
                Actor = user,
                Action = (int)(user.IsSitting ? WiredAvatarAction.Sit : WiredAvatarAction.Stand),
                Code = -1
            });
        }
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
