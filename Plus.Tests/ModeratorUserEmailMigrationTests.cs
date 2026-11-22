using Dapper;
using MySqlConnector;
using Xunit;

namespace Plus.Tests;

public sealed class ModeratorUserEmailMigrationTests
{
    private const string Migration = "Database/Migrations/175_ModeratorUserEmail.sql";

    [Fact]
    public void TheFreshInstallGrantsTheEmailRightOnlyToTheTopSecurityLevel()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));

        foreach (var role in new[] { 7, 8, 9 }) {
            Assert.Contains($"\n({role},'moderation.user_email'),", sql);
        }

        foreach (var role in new[] { 1, 2, 3, 4, 5, 6, 11, 12, 13 }) {
            Assert.DoesNotContain($"\n({role},'moderation.user_email')", sql);
        }

        Assert.Contains("\n('moderation.user_email','moderation',", sql);
    }

    [RoomComponentDatabaseFact]
    public void TheMigrationGrantsTheEmailRightToTopLevelRolesAndARerunChangesNothing()
    {
        var server = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        var schema = "moderator_user_email_" + Guid.NewGuid().ToString("N");

        using (var setup = new MySqlConnection(server)) {
            setup.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        }

        try {
            using var connection = new MySqlConnection(new MySqlConnectionStringBuilder(server) { Database = schema }.ConnectionString);
            connection.Open();
            var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));

            foreach (var table in new[] { "acl_permissions", "roles", "role_permissions" }) {
                connection.Execute(WiredMigrationSchema.Table(fresh, table));
            }

            connection.Execute("""
                INSERT INTO roles (id, slug, name, security_level) VALUES
                    (1, 'default', 'User', 1), (2, 'trial_moderator', 'Trial Moderator', 2), (6, 'manager', 'Manager', 6),
                    (7, 'community_leader', 'Community Leader', 7), (9, 'owner', 'Owner', 7);
                INSERT INTO role_permissions (role_id, permission_key) VALUES (2, 'moderation.tool'), (6, 'moderation.tool');
                """);

            var migration = File.ReadAllText(HabbiconPacketTests.Repo(Migration));
            connection.Execute(migration);
            connection.Execute(migration);

            Assert.Equal(new[] { 7, 9 }, connection.Query<int>(
                "SELECT role_id FROM role_permissions WHERE permission_key = 'moderation.user_email' ORDER BY role_id").ToArray());
            Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM acl_permissions WHERE `key` = 'moderation.user_email' AND is_orphan = 0"));
        }
        finally {
            using var cleanup = new MySqlConnection(server);
            cleanup.Execute($"DROP DATABASE `{schema}`");
        }
    }
}
