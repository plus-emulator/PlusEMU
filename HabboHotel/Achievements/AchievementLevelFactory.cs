using Dapper;
using Plus.Database;

namespace Plus.HabboHotel.Achievements;

public class AchievementLevelFactory : IAchievementLevelFactory
{
    // The client keys achievements by id. A hotel achievement's row id can equal an official id (ACH_Motto is row 3,
    // ACH_EmailVerification is official id 3), so hotel achievements are numbered above every official id.
    internal const int HotelAchievementIdOffset = 1_000_000;

    private readonly IDatabase _database;

    public AchievementLevelFactory(IDatabase database) => _database = database;

    public async Task<Dictionary<string, Achievement>> GetAchievementLevels()
    {
        var achievements = new Dictionary<string, Achievement>();

        using var connection = _database.Connection();
        // habbo.com order (by official id, the hotel's own achievements last), so the client sees its categories in the official order.
        var table = await connection.QueryAsync<AchievementRow>(
            "SELECT `id` AS Id, `category` AS Category, `group_name` AS GroupName, `level` AS Level, `reward_pixels` AS RewardPixels, " +
            "`reward_points` AS RewardPoints, `progress_needed` AS ProgressNeeded, `game_id` AS GameId, `official_id` AS OfficialId, " +
            "`state` AS State, `visible` AS Visible, `badge_code` AS BadgeCode " +
            "FROM `achievements` ORDER BY `official_id` IS NULL, `official_id`, `group_name`, `level`");

        foreach (var row in table) {
            var level = new AchievementLevel(row.Level, row.RewardPixels, row.RewardPoints, row.ProgressNeeded, row.BadgeCode);

            // The lowest level of a group describes the whole group.
            if (!achievements.TryGetValue(row.GroupName, out var achievement)) {
                achievement = new Achievement
                {
                    Id = row.OfficialId ?? HotelAchievementIdOffset + row.Id,
                    Category = row.Category,
                    GroupName = row.GroupName,
                    GameId = row.GameId,
                    OfficialId = row.OfficialId,
                    State = (AchievementState)row.State,
                    Visible = row.Visible
                };
                achievements.Add(row.GroupName, achievement);
            }

            achievement.AddLevel(level);
        }

        return achievements;
    }

    private sealed class AchievementRow
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public int Level { get; set; }
        public int RewardPixels { get; set; }
        public int RewardPoints { get; set; }
        public int ProgressNeeded { get; set; }
        public int GameId { get; set; }
        public int? OfficialId { get; set; }
        public short State { get; set; }
        public bool Visible { get; set; }
        public string? BadgeCode { get; set; }
    }
}
