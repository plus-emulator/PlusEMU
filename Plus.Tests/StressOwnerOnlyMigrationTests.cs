using System.Text.RegularExpressions;
using Dapper;
using MySqlConnector;
using Xunit;

namespace Plus.Tests;

public sealed class StressOwnerOnlyMigrationTests
{
    private const string Migration = "Database/Migrations/99_StressCommandOwnerOnly.sql";

    [RoomComponentDatabaseFact]
    public void OnlyTheOwnerRoleAndItsUsersKeepTheStressCommand()
    {
        InSchema(connection =>
        {
            var sql = File.ReadAllText(HabbiconPacketTests.Repo(Migration));
            connection.Execute(sql);
            connection.Execute(sql);

            Assert.Equal([9], connection.Query<int>("SELECT role_id FROM role_permissions WHERE permission_key = 'command.stress' ORDER BY role_id"));
            Assert.Equal([2, 9], connection.Query<int>("SELECT role_id FROM role_permissions WHERE permission_key = 'moderation.tool' ORDER BY role_id"));
            Assert.Equal([(100, "grant"), (101, "deny")],
                connection.Query<(int, string)>("SELECT user_id, effect FROM user_permissions WHERE permission_key = 'command.stress' ORDER BY user_id"));
            Assert.Equal([102], connection.Query<int>("SELECT user_id FROM user_permissions WHERE permission_key = 'moderation.tool'"));
        });
    }

    [RoomComponentDatabaseFact]
    public void TheOwnerRoleGetsTheStressCommandWhenItLackedIt()
    {
        InSchema(connection =>
        {
            connection.Execute("DELETE FROM role_permissions WHERE role_id = 9");

            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo(Migration)));

            Assert.Equal([9], connection.Query<int>("SELECT role_id FROM role_permissions WHERE permission_key = 'command.stress'"));
        });
    }

    [Fact]
    public void TheFreshInstallGrantsTheStressCommandToTheOwnerRoleOnly()
    {
        var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));

        Assert.Equal(["9"], Regex.Matches(fresh, @"\((\d+),'command\.stress'\)").Select(match => match.Groups[1].Value));
        Assert.Contains("(9,'owner','Owner',", fresh);
    }

    private static void InSchema(Action<MySqlConnection> run)
    {
        var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!);
        using var admin = new MySqlConnection(options.ConnectionString);
        admin.Open();
        var schema = "stress_owner_" + Guid.NewGuid().ToString("N");
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            options.Database = schema;
            using var connection = new MySqlConnection(options.ConnectionString);
            connection.Open();
            connection.Execute("""
                CREATE TABLE roles (id INT NOT NULL PRIMARY KEY, slug VARCHAR(100) NOT NULL, UNIQUE KEY slug (slug)) ENGINE=InnoDB;
                CREATE TABLE role_permissions (role_id INT NOT NULL, permission_key VARCHAR(191) NOT NULL, PRIMARY KEY (role_id, permission_key)) ENGINE=InnoDB;
                CREATE TABLE user_roles (user_id INT NOT NULL, role_id INT NOT NULL, PRIMARY KEY (user_id, role_id)) ENGINE=InnoDB;
                CREATE TABLE user_permissions (user_id INT NOT NULL, permission_key VARCHAR(191) NOT NULL, effect ENUM('grant','deny') NOT NULL,
                    PRIMARY KEY (user_id, permission_key)) ENGINE=InnoDB;
                INSERT INTO roles VALUES (1, 'default'), (2, 'trial_moderator'), (5, 'administrator'), (8, 'developer'), (9, 'owner');
                INSERT INTO role_permissions VALUES (1, 'command.stress'), (2, 'command.stress'), (2, 'moderation.tool'), (5, 'command.stress'),
                    (8, 'command.stress'), (9, 'command.stress'), (9, 'moderation.tool');
                INSERT INTO user_roles VALUES (100, 9), (101, 2), (102, 5), (103, 8), (103, 1);
                INSERT INTO user_permissions VALUES (100, 'command.stress', 'grant'), (101, 'command.stress', 'deny'), (102, 'command.stress', 'grant'),
                    (102, 'moderation.tool', 'grant'), (103, 'command.stress', 'grant');
                """);
            run(connection);
        }
        finally {
            admin.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }
}
