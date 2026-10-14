using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Conditions;

/// <summary>
/// The level User Level (LevelMatches.as, levels 1 to 30) compares. The October client never shows a user level, so the source is
/// inferred: one level per 1000 achievement points, from 1 up to 30. Kept in one place so a live observation can replace the rule.
/// </summary>
public static class WiredUserLevel
{
    public const int Max = 30;
    public const int PointsPerLevel = 1000;

    public static int FromAchievementPoints(int points) => Math.Clamp(1 + Math.Max(0, points) / PointsPerLevel, 1, Max);

    public static int Of(RoomUser user) => FromAchievementPoints(user.GetClient()?.GetHabbo()?.HabboStats?.AchievementPoints ?? 0);
}
