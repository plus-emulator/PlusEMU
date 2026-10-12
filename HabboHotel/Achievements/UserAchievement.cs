namespace Plus.HabboHotel.Achievements;

public class UserAchievement
{
    public UserAchievement(string achievementGroup, int level, int progress)
    {
        AchievementGroup = achievementGroup;
        Level = level;
        Progress = progress;
    }

    public string AchievementGroup { get; }
    public int Level { get; set; }
    // The total score, compared with the cumulative level thresholds.
    public int Progress { get; set; }
    // Progress without a level-up stays in memory and is written with the rest of the user when the session is saved.
    internal bool ProgressUnsaved { get; set; }
}
