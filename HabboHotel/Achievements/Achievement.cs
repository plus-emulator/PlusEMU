using System.Globalization;

namespace Plus.HabboHotel.Achievements;

// The AIR achievement states (WIN63 quest/AchievementCategories): archived ones are listed under "archive",
// wired ones only in the wired_games category.
public enum AchievementState : short
{
    Disabled = 0,
    Enabled = 1,
    Archived = 2,
    OffSeason = 3,
    Wired = 4
}

public class Achievement
{

    public int Id { get; set; }

    public string? Category { get; set; }

    public string? GroupName { get; set; }

    public int GameId { get; set; }

    // The habbo.com achievement id, or null for an achievement only this hotel has.
    public int? OfficialId { get; set; }

    public AchievementState State { get; set; } = AchievementState.Enabled;

    // A hidden achievement keeps progressing and its badges stay, but it is never listed to the client.
    public bool Visible { get; set; } = true;

    public Dictionary<int, AchievementLevel> Levels { get; set; } = new();

    public void AddLevel(AchievementLevel level) => Levels.Add(level.Level, level);

    // Levels can be sparse, so the highest defined level completes the achievement, not the count.
    public int HighestLevel => Levels.Count == 0 ? 0 : Levels.Keys.Max();

    // The first defined level above the given one, or null once the highest level is held.
    public AchievementLevel? NextLevel(int level)
    {
        AchievementLevel? next = null;

        foreach (var candidate in Levels.Values) {
            if (candidate.Level > level && (next == null || candidate.Level < next.Value.Level)) {
                next = candidate;
            }
        }

        return next;
    }

    // The highest defined level below the given one, or null for the first level.
    public AchievementLevel? PreviousLevel(int level)
    {
        AchievementLevel? previous = null;

        foreach (var candidate in Levels.Values) {
            if (candidate.Level < level && (previous == null || candidate.Level > previous.Value.Level)) {
                previous = candidate;
            }
        }

        return previous;
    }

    // Thresholds are cumulative totals, so a level starts where the level before it ends.
    public int ScoreAtStartOf(int level) => PreviousLevel(level)?.Requirement ?? 0;

    // ACH_<code><level>, unless the level names its own badge (the one-level ACH_Profile1 and ACH_Profile2).
    public string BadgeCode(int level) =>
        Levels.TryGetValue(level, out var data) && !string.IsNullOrEmpty(data.BadgeCode)
            ? data.BadgeCode
            : (GroupName ?? string.Empty) + level.ToString(CultureInfo.InvariantCulture);
}
