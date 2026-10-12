using Plus.HabboHotel.Badges.Rarity;

namespace Plus.HabboHotel.Achievements;

public sealed record AchievementUnlockSnapshot(int Id, int Level, string Badge, string PreviousBadge,
    int PointReward, int PixelReward, string Category, BadgeRarity Rarity)
{
    public static AchievementUnlockSnapshot Capture(Achievement achievement, AchievementLevel level, BadgeRarityTable? rarity = null)
    {
        var badge = achievement.BadgeCode(level.Level);
        // The badge of the level before replaces nothing for the first level.
        var previous = achievement.PreviousLevel(level.Level) is { } before ? achievement.BadgeCode(before.Level) : string.Empty;

        return new(achievement.Id, level.Level, badge, previous, level.RewardPoints, level.RewardPixels, achievement.Category ?? string.Empty,
            (rarity ?? BadgeRarityTable.Current).Get(badge));
    }
}
