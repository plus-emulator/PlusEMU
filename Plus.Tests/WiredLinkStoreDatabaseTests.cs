using Dapper;
using MySqlConnector;
using Plus.HabboHotel.Items.Wired.Links;
using Xunit;

namespace Plus.Tests;

public sealed class WiredLinkStoreDatabaseTests
{
    [Fact]
    public void MigrationKeepsEndpointsWhenTheirNetworkIsDeleted()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/119_WiredLinkKeepsSelfReceiving.sql"));

        Assert.Contains("MODIFY `network_id` INT(10) UNSIGNED NULL", sql);
        Assert.Contains("ON DELETE SET NULL", sql);
    }

    [RoomComponentDatabaseFact]
    public void SelfReceivingStaysWhenTheLinkLeavesItsNetworkOrTheNetworkIsDeleted()
    {
        var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")) { Database = "information_schema", Pooling = false };
        var schema = "wired_link_store_" + Guid.NewGuid().ToString("N")[..12];
        using var admin = new MySqlConnection(options.ConnectionString);
        admin.Open();
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            options.Database = schema;
            var database = new HabbiconDatabaseTests.TestDatabase(options.ConnectionString);
            using var connection = database.Connection();
            connection.Execute("""
                CREATE TABLE users (id INT(11) PRIMARY KEY) ENGINE=InnoDB;
                CREATE TABLE items (id INT(10) UNSIGNED PRIMARY KEY, room_id INT(10) UNSIGNED NOT NULL DEFAULT 0) ENGINE=InnoDB;
                CREATE TABLE rooms (id INT(10) UNSIGNED PRIMARY KEY, caption VARCHAR(100)) ENGINE=InnoDB;
                INSERT INTO users VALUES (7);
                INSERT INTO items VALUES (50, 42), (51, 42);
                """);
            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/81_WiredOctoberBoxes.sql")).Split("START TRANSACTION;")[0]);
            var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/119_WiredLinkKeepsSelfReceiving.sql"));
            connection.Execute(migration);
            connection.Execute(migration);
            var store = new DatabaseWiredLinkStore(database);

            Assert.Equal(WiredLinkResult.Ok, store.Create(7, "doors", out var network));
            Assert.Equal(WiredLinkResult.Ok, store.Select(7, 50, 42, network, true));
            Assert.Equal(new WiredLinkEndpoint(50, network, true), store.Endpoint(50));

            Assert.Equal(WiredLinkResult.Ok, store.Select(7, 50, 42, 0, true));
            Assert.Equal(new WiredLinkEndpoint(50, 0, true), store.Endpoint(50));

            Assert.Equal(WiredLinkResult.Ok, store.Select(7, 51, 42, network, true));
            Assert.Equal(WiredLinkResult.Ok, store.Delete(7, network, out var detached));
            Assert.Equal([51u], detached.Select(member => member.ItemId));
            Assert.Equal(new WiredLinkEndpoint(51, 0, true), store.Endpoint(51));
            Assert.Equal(new WiredLinkEndpoint(50, 0, true), store.Endpoint(50));
        }
        finally {
            admin.Execute($"DROP DATABASE `{schema}`");
        }
    }
}
