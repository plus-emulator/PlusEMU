using Dapper;
using MySqlConnector;
using Plus.Database.Migrator;
using Xunit;

namespace Plus.Tests;

// Runs Database/Migrations/102_PetCompanionsCatalog.sql twice on a fresh install.
public sealed class PetCompanionsCatalogTests
{
    // Every pet companion ("pt" figure set) with a name in furnidata, newest first.
    private static readonly string[] Companions =
    [
        "clothing_nftballoonbuddy", "hween_cloth_r26_possessedpumpkin", "hween_cloth_c26_tinyowl", "hween_cloth_c26_raven",
        "hween_cloth_c26_hauntedpuppet", "hween_cloth_c26_cat", "hween_cloth_c26_batfriend", "clothing_toastfriend",
        "clothing_scallywagseagull", "clothing_adorablepetshark", "clothing_nftlittlemonkey", "clothing_r26_toucancompanion",
        "clothing_nftsquirrel", "clothing_r26_wisp", "clothing_r26_mysticdragon", "clothing_r26_robotcompanion",
        "clothing_r26_minielephant", "clothing_nftshoulderdragon1", "clothing_r25_yellowaisha", "clothing_r25_shroomie",
        "clothing_blessingofhades", "clothing_r25_unicorncompanion", "clothing_nftmonkeyfriend", "clothing_nftsnake",
        "clothing_r25_bunnycompanion", "clothing_nftcat", "clothing_teddycompanion", "clothing_r25_goosefriend",
        "clothing_nftbabysnowman", "clothing_nftbabyrudolf", "clothing_r24_ghostfriend", "clothing_nftsummerbird",
        "clothing_nftshiba"
    ];

    [RoomComponentDatabaseFact]
    public async Task TheCompanionsPageSellsEveryNamedPetCompanionAfterAccessories()
    {
        await InFreshInstall(async connection =>
        {
            var clothing = connection.QuerySingle<int>("SELECT id FROM catalog_pages WHERE link = 'clothing' AND catalog_mode = 'NORMAL'");
            var before = connection.Query<string>($"SELECT link FROM catalog_pages WHERE parent_id = {clothing} ORDER BY position, id").ToList();

            await Migrate(connection);
            await Migrate(connection);

            // A regenerated fresh install may already have the page.
            var expected = before.Where(link => link != "clothing_companions").ToList();
            expected.Insert(expected.IndexOf("clothing_accessories") + 1, "clothing_companions");
            Assert.Equal(expected, connection.Query<string>($"SELECT link FROM catalog_pages WHERE parent_id = {clothing} ORDER BY position, id"));
            Assert.Equal(("Companions", "default_3x3", "NORMAL", true, true, 0),
                connection.QuerySingle<(string, string, string, bool, bool, int)>("""
                    SELECT caption, layout, catalog_mode, visible, enabled, required_club_level FROM catalog_pages WHERE link = 'clothing_companions'
                    """));
            Assert.Equal("clothing_catalog_header", connection.QuerySingle<string>("""
                SELECT i.image FROM catalog_page_images i JOIN catalog_pages p ON p.id = i.page_id WHERE p.link = 'clothing_companions' AND i.slot = 0
                """));
            Assert.NotEmpty(connection.QuerySingle<string>("""
                SELECT t.text FROM catalog_page_texts t JOIN catalog_pages p ON p.id = t.page_id WHERE p.link = 'clothing_companions' AND t.slot = 0
                """));

            var offers = connection.Query<(string Item, string Key, int Credits, int Points, int Products, int Amount, int Pages)>("""
                SELECT f.item_name, o.localization_key, o.cost_credits, o.cost_points,
                       (SELECT COUNT(*) FROM catalog_offer_products x WHERE x.offer_id = o.id), op.amount,
                       (SELECT COUNT(*) FROM catalog_page_offers y WHERE y.offer_id = o.id)
                FROM catalog_page_offers po
                JOIN catalog_pages p ON p.id = po.page_id AND p.link = 'clothing_companions'
                JOIN catalog_offers o ON o.id = po.offer_id AND o.enabled = 1
                JOIN catalog_offer_products op ON op.offer_id = o.id AND op.product_type = 'furni'
                JOIN furniture f ON f.id = op.furniture_id AND f.interaction_type = 'purchasable_clothing'
                ORDER BY po.position
                """).ToList();

            Assert.Equal(Companions, offers.Select(o => o.Item));
            Assert.All(offers, o => Assert.Equal((o.Item, 25, 0, 1, 1, 1), (o.Key, o.Credits, o.Points, o.Products, o.Amount, o.Pages)));
        });
    }

    private static async Task Migrate(MySqlConnection connection)
    {
        foreach (var statement in SqlScript.Split(await File.ReadAllTextAsync(HabbiconPacketTests.Repo("Database/Migrations/102_PetCompanionsCatalog.sql")))) {
            await connection.ExecuteAsync(statement);
        }
    }

    private static async Task InFreshInstall(Func<MySqlConnection, Task> run)
    {
        var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!) { AllowUserVariables = true };
        await using var admin = new MySqlConnection(options.ConnectionString);
        await admin.OpenAsync();
        var schema = "pet_companions_" + Guid.NewGuid().ToString("N");
        await admin.ExecuteAsync($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            options.Database = schema;
            await using var connection = new MySqlConnection(options.ConnectionString);
            await connection.OpenAsync();

            foreach (var statement in SqlScript.Split(await File.ReadAllTextAsync(HabbiconPacketTests.Repo("Database/FreshInstall.sql")))) {
                await connection.ExecuteAsync(statement, commandTimeout: 300);
            }

            await run(connection);
        }
        finally {
            await admin.ExecuteAsync($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }
}
