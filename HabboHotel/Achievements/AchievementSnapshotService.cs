using System.Collections.Immutable;
using System.Globalization;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Achievements;

// One AIR AchievementData entry (WIN63 package_92.class_3624), already resolved against the achievement's own levels:
// the level a user works towards, or the last level once every level is held. Scores are the cumulative totals;
// the client subtracts ScoreAtStartOfLevel from Requirement and Progress for the bar of the level.
public sealed record AchievementProgressSnapshot(int Id, int TargetLevel, string Badge, int ScoreAtStartOfLevel, int Requirement, int RewardPixels,
    int Progress, bool Completed, string Category, int TotalLevels, AchievementState State)
{
    public static AchievementProgressSnapshot? Capture(Achievement achievement, UserAchievement? user)
    {
        // An achievement without levels has no level to show, so it is left out instead of being indexed.
        if (achievement.Levels.Count == 0) {
            return null;
        }

        var next = achievement.NextLevel(user?.Level ?? 0);
        var level = next ?? achievement.Levels[achievement.HighestLevel];
        // A threshold below the one before it (habbo.com's ACH_RecycledItems19) starts at itself instead of running backwards.
        var start = Math.Min(achievement.ScoreAtStartOf(level.Level), level.Requirement);

        return new(achievement.Id, level.Level, achievement.BadgeCode(level.Level), start, level.Requirement, level.RewardPixels,
            Math.Max(user?.Progress ?? 0, start), next == null, achievement.Category ?? string.Empty, achievement.Levels.Count, achievement.State);
    }
}

public sealed record AchievementDefinitionSnapshot(string Name, ImmutableArray<AchievementLevelDefinition> Levels);
public readonly record struct AchievementLevelDefinition(int Level, int Requirement);

public interface IAchievementSnapshotService
{
    ImmutableArray<AchievementDefinitionSnapshot> CaptureDefinitions(IEnumerable<Achievement> achievements);
    ImmutableArray<AchievementProgressSnapshot> Capture(Habbo habbo, IEnumerable<Achievement> achievements);
}

public sealed class AchievementSnapshotService : IAchievementSnapshotService
{
    // BadgePointLimits (1231) fills %limit% for the badge key "ACH_" + name + level, so only levels whose badge follows
    // that pattern are listed; ACH_Profile1 and ACH_Profile2 have no %limit% to fill.
    public ImmutableArray<AchievementDefinitionSnapshot> CaptureDefinitions(IEnumerable<Achievement> achievements)
    {
        var definitions = ImmutableArray.CreateBuilder<AchievementDefinitionSnapshot>();

        foreach (var achievement in Listed(achievements)) {
            var group = achievement.GroupName ?? string.Empty;
            var levels = achievement.Levels.Values.OrderBy(level => level.Level)
                .Where(level => achievement.BadgeCode(level.Level) == group + level.Level.ToString(CultureInfo.InvariantCulture))
                .Select(level => new AchievementLevelDefinition(level.Level, level.Requirement))
                .ToImmutableArray();

            if (levels.Length > 0) {
                definitions.Add(new(group.StartsWith("ACH_", StringComparison.Ordinal) ? group[4..] : group, levels));
            }
        }

        return definitions.ToImmutable();
    }

    public ImmutableArray<AchievementProgressSnapshot> Capture(Habbo habbo, IEnumerable<Achievement> achievements)
    {
        var snapshots = ImmutableArray.CreateBuilder<AchievementProgressSnapshot>();

        foreach (var achievement in Listed(achievements)) {
            if (AchievementProgressSnapshot.Capture(achievement, habbo.GetAchievementData(achievement.GroupName ?? string.Empty)) is { } snapshot) {
                snapshots.Add(snapshot);
            }
        }

        return snapshots.ToImmutable();
    }

    // Hidden achievements are never listed. AIR orders its categories by first appearance, so the list keeps the
    // habbo.com order (by official id); the hotel's own achievements follow in their loaded order.
    private static IEnumerable<Achievement> Listed(IEnumerable<Achievement> achievements) =>
        achievements.Where(achievement => achievement.Visible).OrderBy(achievement => achievement.OfficialId ?? int.MaxValue);
}
