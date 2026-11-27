using Dapper;
using MySqlConnector;
using Xunit;

namespace Plus.Tests;

/// <summary>Migration 179: every user can buy the crafting supplies and the mystery box.</summary>
public sealed class PublicCraftingCatalogMigrationTests
{
    private const string Migration = "Database/Migrations/179_PublicCraftingSupplies.sql";

    [RoomComponentDatabaseFact]
    public void TheSuppliesAndTheBoxFollowTheCraftingTablePageForEveryUser()
    {
        WithCatalogue(true, connection =>
        {
            Assert.Equal(new[]
            {
                (3, 2, 3, "nov26craft", (string?)null),
                (5, 2, 4, "nov26crackb", null),
                (6, 2, 5, "xmas12_mysterybox", null),
                (4, 2, 6, "hween26sc", null)
            }, HotNow(connection));
            Assert.Equal(("Mystery Box", 90, "default_3x3", 1, 1), connection.QuerySingle<(string, int, string, int, int)>(
                "SELECT caption, icon, layout, visible, enabled FROM catalog_pages WHERE id = 6"));
            Assert.Equal(new[] { 1800004797 }, connection.Query<int>("SELECT offer_id FROM catalog_page_offers WHERE page_id = 6"));
            Assert.Contains("key", connection.ExecuteScalar<string>("SELECT text FROM catalog_page_texts WHERE page_id = 6 AND slot = 0"));
        });
    }

    [RoomComponentDatabaseFact]
    public void ARerunChangesNothing()
    {
        WithCatalogue(true, connection =>
        {
            var before = HotNow(connection);

            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo(Migration)));

            Assert.Equal(before, HotNow(connection));
            Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM catalog_pages WHERE link = 'xmas12_mysterybox'"));
            Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM catalog_page_texts WHERE page_id = 6"));
        });
    }

    [RoomComponentDatabaseFact]
    public void AHotelWithoutTheCraftingTablePageIsNotChanged()
    {
        WithCatalogue(false, connection =>
        {
            Assert.Equal((1, "catalog.pages.administrator"), connection.QuerySingle<(int, string)>(
                "SELECT parent_id, required_permission FROM catalog_pages WHERE id = 5"));
            Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM catalog_pages WHERE link = 'xmas12_mysterybox'"));
        });
    }

    private static (int Id, int Parent, int Position, string Link, string? Permission)[] HotNow(MySqlConnection connection) =>
        connection.Query<(int, int, int, string, string?)>(
            "SELECT id, parent_id, position, link, required_permission FROM catalog_pages WHERE parent_id = 2 ORDER BY position").ToArray();

    private static void WithCatalogue(bool craftingPage, Action<MySqlConnection> body)
    {
        var server = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        var schema = "public_crafting_" + Guid.NewGuid().ToString("N");

        using (var setup = new MySqlConnection(server)) {
            setup.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        }

        try {
            using var connection = new MySqlConnection(new MySqlConnectionStringBuilder(server) { Database = schema }.ConnectionString);
            connection.Open();
            var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
            connection.Execute("SET FOREIGN_KEY_CHECKS = 0");

            foreach (var table in new[] { "acl_permissions", "furniture", "catalog_pages", "catalog_page_texts", "catalog_offers", "catalog_offer_products", "catalog_page_offers" }) {
                connection.Execute(Table(fresh, table));
            }

            connection.Execute("SET FOREIGN_KEY_CHECKS = 1");

            connection.Execute($"""
                INSERT INTO acl_permissions (`key`, category) VALUES ('catalog.pages.administrator', 'catalog');
                INSERT INTO furniture (id, item_name, public_name, sprite_id, interaction_type) VALUES (10, 'mystery_box', 'mystery_box', 4797, 'mystery_box');
                INSERT INTO catalog_offers (id, localization_key, cost_credits) VALUES (1800004797, 'mystery_box', 0);
                INSERT INTO catalog_offer_products (offer_id, position, product_type, furniture_id) VALUES (1800004797, 0, 'furni', 10);
                INSERT INTO catalog_pages (id, parent_id, caption, required_permission, position, link) VALUES
                    (1, NULL, 'Staff', 'catalog.pages.administrator', 0, 'staff'),
                    (2, NULL, 'Hot now', NULL, 1, '2020_hotnow'),
                    (5, 1, 'Tactical Supplies', 'catalog.pages.administrator', 34, 'nov26crackb');
                {(craftingPage
                    ? "INSERT INTO catalog_pages (id, parent_id, caption, position, link) VALUES (3, 2, 'Survivalist Crafting Table', 3, 'nov26craft'), (4, 2, 'Seasonal Currency Items', 4, 'hween26sc');"
                    : "")}
                """);
            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo(Migration)));
            body(connection);
        }
        finally {
            using var cleanup = new MySqlConnection(server);
            cleanup.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private static string Table(string dump, string table)
    {
        var start = dump.IndexOf($"CREATE TABLE `{table}` (", StringComparison.Ordinal);
        var end = dump.IndexOf(';', dump.IndexOf("ENGINE=", start, StringComparison.Ordinal));

        return dump[start..(end + 1)];
    }
}
