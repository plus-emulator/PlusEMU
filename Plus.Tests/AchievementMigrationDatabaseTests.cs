using System.Text.RegularExpressions;
using Dapper;
using MySqlConnector;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

// Runs Database/Migrations/76_OfficialAchievements.sql on achievement tables in their pre-migration shape.
[Collection("SharedDatabase")]
public sealed class AchievementMigrationDatabaseTests
{
    [RoomComponentDatabaseFact]
    public async Task OfficialSetReplacesThresholdsKeepsRewardsHidesTheRestAndConvertsProgressToTotals()
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        var schema = "task_achievement_migration_" + Guid.NewGuid().ToString("N");
        using var admin = new MySqlConnection(root);
        admin.Execute($"CREATE DATABASE `{schema}`");

        try {
            var database = new HabbiconDatabaseTests.TestDatabase(new MySqlConnectionStringBuilder(root) { Database = schema }.ConnectionString);
            using var connection = database.Connection();
            var pristine = File.ReadAllText(HabbiconPacketTests.Repo("Resources/SQLs/Original Database.sql"));

            foreach (var table in new[] { "achievements", "badge_definitions", "user_achievements" }) {
                connection.Execute(Regex.Match(pristine, $@"CREATE TABLE `{table}` \([\s\S]*?\) ENGINE=[^;]+;").Value);
            }

            // Old thresholds counted from 0 after every level, with this hotel's rewards.
            connection.Execute("INSERT INTO achievements(group_name,category,level,reward_pixels,reward_points,progress_needed,game_id) VALUES " +
                "('ACH_RespectEarned','social',1,5,10,1,0),('ACH_RespectEarned','social',2,7,11,10,0),('ACH_RespectEarned','social',3,5,10,20,0)," +
                "('ACH_RespectEarned','social',4,5,10,50,0),('ACH_RoomEntry','explore',1,5,10,5,0),('ACH_RoomEntry','explore',2,5,10,10,0)," +
                "('ACH_AvatarLooks','identity',1,5,100,1,0),('ACH_AvatarLooks','identity',2,5,100,1,0),('ACH_SelfModDoorModeSeen','tools',1,0,5,1,0)," +
                "('ACH_Motto','identity',1,5,100,1,0),('ACH_BaseJumpWins','games',1,0,5,3,1),('ACH_BaseJumpWins','games',2,0,5,6,1)");
            connection.Execute("INSERT INTO user_achievements(userid,`group`,level,progress) VALUES " +
                "(7,'ACH_RespectEarned',4,7),(8,'ACH_RespectEarned',3,0),(7,'ACH_RoomEntry',0,3),(8,'ACH_RoomEntry',2,0)," +
                "(7,'ACH_AvatarLooks',2,0),(7,'ACH_Motto',1,0),(7,'ACH_BaseJumpWins',2,4),(7,'ACH_Gone',3,9)");
            var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/76_OfficialAchievements.sql"));

            connection.Execute(migration);
            // A second run converts nothing again and keeps operator edits to rewards.
            connection.Execute("UPDATE achievements SET reward_points=99 WHERE group_name='ACH_RespectEarned' AND level=5");
            connection.Execute(migration);

            // Total = the thresholds every held level took plus the progress since, at least the official threshold of the level held.
            Assert.Equal(new[] {
                (7, "ACH_AvatarLooks", 2, 2), (7, "ACH_BaseJumpWins", 2, 13), (7, "ACH_Gone", 3, 9), (7, "ACH_Motto", 1, 1),
                (7, "ACH_RespectEarned", 4, 88), (7, "ACH_RoomEntry", 0, 3), (8, "ACH_RespectEarned", 3, 31), (8, "ACH_RoomEntry", 2, 20)
            }, connection.Query<(int, string, int, int)>("SELECT userid,`group`,level,progress FROM user_achievements ORDER BY userid,`group`"));
            Assert.Equal(new[] { (1, 5, 10, 1), (2, 7, 11, 6), (3, 5, 10, 16), (4, 5, 10, 66), (5, 0, 99, 166), (6, 0, 10, 366) },
                connection.Query<(int, int, int, int)>("SELECT level,reward_pixels,reward_points,progress_needed FROM achievements " +
                    "WHERE group_name='ACH_RespectEarned' AND level<=6 ORDER BY level"));
            Assert.Equal(("explore", 1), connection.QuerySingle<(string, int)>(
                "SELECT category,COUNT(*) FROM achievements WHERE group_name='ACH_SelfModDoorModeSeen' GROUP BY category"));
            // An official achievement keeps exactly its official levels.
            Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM achievements WHERE group_name='ACH_AvatarLooks'"));
            Assert.Equal(new[] { ("ACH_BaseJumpWins", 2), ("ACH_Motto", 1) },
                connection.Query<(string, int)>("SELECT group_name,COUNT(*) FROM achievements WHERE visible=0 GROUP BY group_name ORDER BY group_name"));
            Assert.Equal((1876, 166), connection.QuerySingle<(int, int)>("SELECT COUNT(*),COUNT(DISTINCT group_name) FROM achievements WHERE visible=1"));
            Assert.Equal(new[] { ("ACH_Profile1", "ACH_Profile1"), ("ACH_Profile2", "ACH_Profile2") },
                connection.Query<(string, string)>("SELECT group_name,badge_code FROM achievements WHERE badge_code IS NOT NULL ORDER BY group_name"));
            Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM achievements a LEFT JOIN badge_definitions b " +
                "ON b.code = COALESCE(a.badge_code, CONCAT(a.group_name, a.level)) WHERE b.code IS NULL"));

            // The engine loads the official order with official ids; only the official set is listed.
            var loaded = await new AchievementLevelFactory(database).GetAchievementLevels();
            Assert.Equal(168, loaded.Count);
            Assert.Equal("ACH_EmailVerification", loaded.Values.First().GroupName);
            Assert.Equal((17, AchievementState.Enabled, true), (loaded["ACH_RespectEarned"].Id, loaded["ACH_RespectEarned"].State, loaded["ACH_RespectEarned"].Visible));
            Assert.Equal((null, false), (loaded["ACH_Motto"].OfficialId, loaded["ACH_Motto"].Visible));
            // A hotel achievement never shares its client id with an official one.
            Assert.True(loaded["ACH_Motto"].Id > AchievementLevelFactory.HotelAchievementIdOffset);
            Assert.Equal("ACH_Profile2", loaded["ACH_Profile2"].BadgeCode(1));
            var listed = new AchievementSnapshotService().Capture(new Habbo { Id = 7, Username = "u" }, loaded.Values);
            Assert.Equal(166, listed.Length);
            Assert.Equal(1876, listed.Sum(snapshot => snapshot.TotalLevels));
            Assert.Contains(listed, snapshot => snapshot.State == AchievementState.Archived);
            var limits = new AchievementSnapshotService().CaptureDefinitions(loaded.Values);
            Assert.Equal(1874, limits.Sum(definition => definition.Levels.Length));
            var badges = new BadgeManager(database, TestGameClientManager.Empty, TestLogging.For<BadgeManager>());
            await badges.Init();
            Assert.All(loaded.Values.SelectMany(achievement => achievement.Levels.Keys.Select(achievement.BadgeCode)),
                badge => Assert.True(badges.Badges.ContainsKey(badge.ToUpper()), badge));
        }
        finally {
            admin.Execute($"DROP DATABASE `{schema}`");
        }
    }

    // Runs Database/Migrations/77_AchievementBadgeCleanup.sql after 76 on badges the old engine left behind.
    [RoomComponentDatabaseFact]
    public void BadgeCleanupKeepsOnlyTheHighestLevelBadgeAndCarriesAWornSlot()
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        var schema = "task_achievement_badges_" + Guid.NewGuid().ToString("N");
        using var admin = new MySqlConnection(root);
        admin.Execute($"CREATE DATABASE `{schema}`");

        try {
            var database = new HabbiconDatabaseTests.TestDatabase(new MySqlConnectionStringBuilder(root) { Database = schema }.ConnectionString);
            using var connection = database.Connection();
            var pristine = File.ReadAllText(HabbiconPacketTests.Repo("Resources/SQLs/Original Database.sql"));

            foreach (var table in new[] { "achievements", "badge_definitions", "user_achievements", "user_badges" }) {
                connection.Execute(Regex.Match(pristine, $@"CREATE TABLE `{table}` \([\s\S]*?\) ENGINE=[^;]+;").Value);
            }

            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/76_OfficialAchievements.sql")));
            connection.Execute("INSERT INTO user_badges(user_id,badge_id,badge_slot) VALUES " +
                // Earlier levels worn in slots 2 and 3, the highest level not worn: it takes slot 2.
                "(7,'ACH_RoomEntry1',2),(7,'ACH_RoomEntry2',3),(7,'ACH_RoomEntry3',0)," +
                // One-level achievements with their own badge codes are different achievements.
                "(7,'ACH_Profile1',0),(7,'ACH_Profile2',1)," +
                // The highest level is already worn and keeps its slot.
                "(8,'ACH_RespectEarned2',1),(8,'ACH_RespectEarned4',4)," +
                // Not achievement badges of a known achievement, and a single level badge: untouched.
                "(8,'ADM',5),(8,'ACH_Gone1',0),(8,'ACH_Gone2',0),(9,'ACH_RoomEntry1',1)");
            var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/77_AchievementBadgeCleanup.sql"));
            var expected = new[] {
                (7, "ACH_Profile1", 0), (7, "ACH_Profile2", 1), (7, "ACH_RoomEntry3", 2),
                (8, "ACH_Gone1", 0), (8, "ACH_Gone2", 0), (8, "ACH_RespectEarned4", 4), (8, "ADM", 5),
                (9, "ACH_RoomEntry1", 1)
            };

            connection.Execute(migration);
            Assert.Equal(expected, connection.Query<(int, string, int)>("SELECT user_id,badge_id,badge_slot FROM user_badges ORDER BY user_id,badge_id"));

            // A second run changes nothing.
            connection.Execute(migration);
            Assert.Equal(expected, connection.Query<(int, string, int)>("SELECT user_id,badge_id,badge_slot FROM user_badges ORDER BY user_id,badge_id"));
        }
        finally {
            admin.Execute($"DROP DATABASE `{schema}`");
        }
    }
}
