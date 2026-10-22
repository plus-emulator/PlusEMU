using System.Data;
using Dapper;
using MySqlConnector;
using Plus.Core.Settings;
using Plus.Database;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Subscriptions;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public sealed class CatalogFurniPurchaseDatabaseTests
{
    [RoomComponentDatabaseFact]
    public void LimitedFurniCommitsAndRollsBackWithTheWalletTransaction()
    {
        var root = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!)
        {
            Database = "",
            Pooling = false,
            AllowZeroDateTime = true,
            ConvertZeroDateTime = true
        };
        using var admin = new MySqlConnection(root.ConnectionString);
        admin.Open();
        var schema = "task_catalog_furni_" + Guid.NewGuid().ToString("N");
        admin.Execute($"CREATE DATABASE `{schema}`");

        try {
            var options = new MySqlConnectionStringBuilder(root.ConnectionString) { Database = schema };
            using var connection = new MySqlConnection(options.ConnectionString);
            connection.Open();
            connection.Execute("""
                CREATE TABLE users (id INT PRIMARY KEY,credits INT NOT NULL);
                CREATE TABLE items (id INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,base_item INT UNSIGNED NOT NULL,user_id INT UNSIGNED NOT NULL,room_id INT UNSIGNED NOT NULL DEFAULT 0,x INT NOT NULL DEFAULT 0,y INT NOT NULL DEFAULT 0,z DOUBLE NOT NULL DEFAULT 0,wall_pos VARCHAR(100) NOT NULL DEFAULT '',rot INT NOT NULL DEFAULT 0,extra_data TEXT NOT NULL,limited_number INT NOT NULL DEFAULT 0,limited_stack INT NOT NULL DEFAULT 0);
                CREATE TABLE club_credit_spending (id INT AUTO_INCREMENT PRIMARY KEY,user_id INT NOT NULL,credits INT NOT NULL,spent_at DATETIME(6) NOT NULL);
                CREATE TABLE catalog_offer_limited (offer_id INT PRIMARY KEY,stack INT UNSIGNED NOT NULL,sold INT UNSIGNED NOT NULL DEFAULT 0);
                INSERT INTO catalog_offer_limited VALUES (50,2,0);
                INSERT INTO users VALUES (42,100);
                """);
            var database = new ProbeDatabase(options.ConnectionString);
            var now = new DateTimeOffset(2040, 2, 3, 4, 5, 6, TimeSpan.Zero);
            var rewards = new ClubRewards(database, null!, null!, new Settings(), null!, new FixedClock(now), null!);
            var factory = new ItemFactory(database);
            var definition = new ItemDefinition { Id = 900, ItemName = "rare", Type = ItemType.Floor };
            var habbo = Habbo(now);

            Assert.True(rewards.Charge(habbo, 10, deliver: (db, transaction) =>
            {
                var serial = (uint)CatalogLimitedStock.Reserve(db, transaction, 50)!.Value;
                factory.CreateSingleItemNullable(db, transaction, definition, habbo, "", 0, serial, 2);

                return true;
            }));

            Assert.Equal(90, connection.ExecuteScalar<int>("SELECT credits FROM users WHERE id=42"));
            Assert.Equal((42, 1, 2), connection.QuerySingle<(int, int, int)>("SELECT user_id,limited_number,limited_stack FROM items"));
            Assert.Equal(1, connection.ExecuteScalar<int>("SELECT sold FROM catalog_offer_limited WHERE offer_id=50"));

            connection.Execute("DELETE FROM items; DELETE FROM club_credit_spending; UPDATE users SET credits=100 WHERE id=42; UPDATE catalog_offer_limited SET sold=0 WHERE offer_id=50; CREATE TRIGGER reject_item AFTER INSERT ON items FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced';");
            habbo.Credits = 100;

            Assert.Throws<MySqlException>(() => rewards.Charge(habbo, 10, deliver: (db, transaction) =>
            {
                var serial = (uint)CatalogLimitedStock.Reserve(db, transaction, 50)!.Value;
                factory.CreateSingleItemNullable(db, transaction, definition, habbo, "", 0, serial, 2);

                return true;
            }));

            Assert.Equal(100, habbo.Credits);
            Assert.Equal(100, connection.ExecuteScalar<int>("SELECT credits FROM users WHERE id=42"));
            Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items"));
            Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM club_credit_spending"));
            Assert.Equal(0, connection.ExecuteScalar<int>("SELECT sold FROM catalog_offer_limited WHERE offer_id=50"));
        }
        finally {
            admin.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static Habbo Habbo(DateTimeOffset now) => new()
    {
        Id = 42,
        Credits = 100,
        Access = UserAccess.Create([], membership: new ClubMembership(now.AddDays(1), now.AddDays(-1), now.AddDays(-1)))
    };

    private sealed class ProbeDatabase(string connectionString) : IDatabase
    {
        public bool IsConnected() => true;
        public IDbConnection Connection() => new MySqlConnection(connectionString);
    }

    private sealed class Settings : ISettingsManager
    {
        public string TryGetValue(string value) => "10";
        public string? GetOptionalValue(string key) => "10";
        public Task Reload() => Task.CompletedTask;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
