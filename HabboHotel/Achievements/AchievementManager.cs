using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Achievements;
using Plus.Communication.Packets.Outgoing.Notifications;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.Database;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.Friends;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Inventory.Badges;
using Plus.HabboHotel.Users.Messenger;
using Plus.HabboHotel.Users;
using Plus.Core;
using Plus.HabboHotel.Talents;

namespace Plus.HabboHotel.Achievements;

public class AchievementManager : IAchievementManager, IStartable
{
    public Dictionary<string, Achievement> Achievements { get; private set; }
    private readonly IAchievementLevelFactory _achievementLevelFactory;
    private readonly IDatabase _database;
    private readonly IBadgeManager _badgeManager;
    private readonly ITalentTrackProgressionService _talents;
    private readonly IMessengerDataLoader _messenger;

    public AchievementManager(IAchievementLevelFactory achievementLevelFactory, IDatabase database, IBadgeManager badgeManager, ITalentTrackProgressionService talents,
        IMessengerDataLoader messenger)
    {
        _achievementLevelFactory = achievementLevelFactory;
        _database = database;
        _badgeManager = badgeManager;
        _talents = talents;
        _messenger = messenger;
        Achievements = new();
    }

    public async Task Start() => await Init();

    public async Task Init() => Achievements = await _achievementLevelFactory.GetAchievementLevels();

    // The stored progress is the user's total score and the level thresholds are cumulative, as on habbo.com: fromBeginning
    // replaces the total, otherwise the progress is added to it. Every threshold the total reaches unlocks its level in order.
    public bool ProgressAchievement(GameClient session, string group, int progress, bool fromBeginning = false)
    {
        if (session == null || !Achievements.TryGetValue(group, out var data) || data == null) {
            return false;
        }

        var habbo = session.GetHabbo();

        if (habbo == null || habbo.AccessClosed || data.NextLevel(habbo.GetAchievementData(group)?.Level ?? 0) == null) {
            return false; // no levels, or done: no more.
        }

        var userData = habbo.Achievements.GetOrAdd(group, name => new(name, 0, 0));
        List<AchievementLevel> reached;
        AchievementProgressSnapshot? progressed = null;

        lock (habbo.WalletSync) {
            if (habbo.AccessClosed) {
                return false;
            }

            var total = fromBeginning ? Math.Max(0, progress) : (int)Math.Clamp((long)userData.Progress + progress, 0, int.MaxValue);
            // Levels unlock in order; the first threshold out of reach stops the run.
            reached = data.Levels.Values.Where(level => level.Level > userData.Level).OrderBy(level => level.Level)
                .TakeWhile(level => level.Requirement <= total).ToList();

            // The reward points land in the loaded statistics and the badge in the loaded inventory, so a user without them is not advanced.
            if (reached.Count > 0 && (habbo.HabboStats == null || habbo.Inventory == null)) {
                return false;
            }

            // Plain progress is saved with the session instead of a write per call; a level-up commits it at once.
            userData.Progress = total;
            userData.ProgressUnsaved = true;

            if (reached.Count == 0 && data.Visible) {
                progressed = AchievementProgressSnapshot.Capture(data, userData);
            }
        }

        if (reached.Count == 0) {
            if (progressed != null) {
                session.Send(new AchievementProgressedComposer(progressed));
            }

            _talents.Progress(habbo, Achievements);

            return false;
        }

        var unlocked = false;

        foreach (var level in reached) {
            if (!Unlock(session, habbo, data, userData, level)) {
                break;
            }

            unlocked = true;
        }

        // AIR builds the achievement list once per session; the new state of a listed achievement arrives as AchievementProgressed.
        if (unlocked && data.Visible && AchievementProgressSnapshot.Capture(data, userData) is { } next) {
            session.Send(new AchievementProgressedComposer(next));
        }

        _talents.Progress(habbo, Achievements);

        return unlocked;
    }

    // One level: its badge replaces the previous level's, then the level and its rewards are committed together.
    private bool Unlock(GameClient session, Habbo habbo, Achievement data, UserAchievement userData, AchievementLevel level)
    {
        if (habbo.HabboStats is not { } stats || habbo.Inventory is not { } inventory) {
            return false;
        }

        var unlock = AchievementUnlockSnapshot.Capture(data, level);
        var wornChanged = _badgeManager.ReplaceBadge(habbo, unlock.PreviousBadge, unlock.Badge).Result;

        lock (habbo.WalletSync) {
            if (habbo.AccessClosed) {
                return false;
            }

            // A concurrent call already committed this level.
            if (userData.Level >= level.Level) {
                return true;
            }

            using (var connection = _database.Connection()) {
                connection.Execute("REPLACE INTO `user_achievements` (`userid`,`group`,`level`,`progress`) VALUES (@habboId, @group, @level, @progress)",
                    new { habboId = habbo.Id, group = userData.AchievementGroup, level = level.Level, progress = userData.Progress });
            }

            userData.Level = level.Level;
            userData.ProgressUnsaved = false;
            // A synchronous disconnect during Send must save both rewards.
            habbo.Duckets += level.RewardPixels;
            stats.AchievementPoints += level.RewardPoints;
            session.Send(new AchievementUnlockedComposer(unlock));

            if (level.RewardPixels != 0) {
                session.Send(new HabboActivityPointNotificationComposer(habbo.Duckets, level.RewardPixels));
            }

            session.Send(new AchievementScoreComposer(stats.AchievementPoints));
        }

        // The unlock packet stops the client wearing the new badge, so the worn badges follow it.
        if (wornChanged) {
            var worn = new HabboUserBadgesComposer(habbo.Id, BadgeInventorySnapshot.Capture(inventory.Badges.EquippedBadges).Equipped);

            if (habbo.CurrentRoom is { } room) {
                room.SendPacket(worn);
            }
            else {
                session.Send(worn);
            }
        }

        // Only a committed level reaches friends; the friend bar shows the badge code as "earned an achievement".
        _messenger.BroadcastStatusUpdate(habbo, MessengerEventTypes.AchievementUnlocked, unlock.Badge);

        return true;
    }

    public ICollection<Achievement> GetGameAchievements(int gameId)
    {
        var achievements = new List<Achievement>();

        foreach (var achievement in Achievements.Values.ToList()) {
            if (achievement.Category == "games" && achievement.GameId == gameId) {
                achievements.Add(achievement);
            }
        }

        return achievements;
    }
}
