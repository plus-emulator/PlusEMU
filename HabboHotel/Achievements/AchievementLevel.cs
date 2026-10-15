namespace Plus.HabboHotel.Achievements;

public struct AchievementLevel
{
    public int Level { get; }
    // The total score that completes this level; thresholds are cumulative, as on habbo.com.
    public int Requirement { get; }
    public int RewardPixels { get; }
    public int RewardPoints { get; }
    // Null when the badge is the group name followed by the level.
    public string? BadgeCode { get; }

    public AchievementLevel(int level, int rewardPixels, int rewardPoints, int requirement, string? badgeCode = null)
    {
        Level = level;
        RewardPixels = rewardPixels;
        RewardPoints = rewardPoints;
        Requirement = requirement;
        BadgeCode = badgeCode;
    }
}
