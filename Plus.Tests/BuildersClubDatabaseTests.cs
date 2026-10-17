using System.Data;
using Dapper;
using MySqlConnector;
using Plus.Database;
using Plus.HabboHotel.Catalog.BuildersClub;
using Xunit;

namespace Plus.Tests;

public sealed class BuildersClubDatabaseTests
{
    [RoomComponentDatabaseFact]
    public void MigrationGivesPagesTheirModeAndTwiceEndsInTheSameBuildersClubTree()
    {
        InSchema((connection, _) =>
        {
            var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/89_BuildersClubCatalog.sql"));
            connection.Execute(migration);
            connection.Execute(migration);

            Assert.Equal(["NORMAL", "NORMAL", "NORMAL"], connection.Query<string>("SELECT catalog_mode FROM catalog_pages WHERE id < 100 ORDER BY id"));
            Assert.Equal([("Furni", "furni", (int?)null), ("Building Blocks", "bc_blocks_recolorable", 1), ("Alphabet Blocks", "bc_alphabet_recolorable", 1)],
                connection.Query<(string, string, int?)>("""
                    SELECT caption, link, IF(parent_id IS NULL, NULL, 1) FROM catalog_pages WHERE catalog_mode = 'BUILDERS_CLUB' ORDER BY id
                    """));
            Assert.Equal([35202, 35203], connection.Query<int>("""
                SELECT offer.offer_id FROM catalog_page_offers offer JOIN catalog_pages page ON page.id = offer.page_id
                WHERE page.catalog_mode = 'BUILDERS_CLUB' AND page.link = 'bc_blocks_recolorable' ORDER BY offer.position
                """));
            Assert.Equal(["catalog_teaser_bcblocks"], connection.Query<string>("""
                SELECT image.image FROM catalog_page_images image JOIN catalog_pages page ON page.id = image.page_id
                WHERE page.catalog_mode = 'BUILDERS_CLUB' AND page.link = 'bc_blocks_recolorable'
                """));
            // A link is unique within its mode only.
            Assert.Throws<MySqlException>(() => connection.Execute("INSERT INTO catalog_pages (caption, link, catalog_mode) VALUES ('x', 'furni', 'BUILDERS_CLUB')"));
        });
    }

    [RoomComponentDatabaseFact]
    public void TheStoreCountsOnlyPlacedFurniAndDeletesThemWithTheirMark()
    {
        InSchema((connection, connectionString) =>
        {
            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/89_BuildersClubCatalog.sql")));
            var store = new BuildersClubStore(new ProbeDatabase(connectionString));
            var expiry = new DateTimeOffset(2040, 1, 2, 3, 4, 5, TimeSpan.Zero);
            connection.Execute("INSERT INTO user_builders_club (user_id, expires_at) VALUES (7, @expiry)", new { expiry = expiry.UtcDateTime });

            var placed = store.Create(19348, 7, "255,255,255");
            var stored = store.Create(19348, 7, "");
            connection.Execute("UPDATE items SET room_id = 42 WHERE id = @placed", new { placed });

            Assert.Equal(expiry, store.MembershipExpiry(7));
            Assert.Null(store.MembershipExpiry(8));
            Assert.Equal(1, store.FurniCount(7));
            Assert.Equal(0, store.FurniCount(8));
            Assert.Equal("255,255,255", connection.QuerySingle<string>("SELECT extra_data FROM items WHERE id = @placed", new { placed }));

            store.Delete(placed);

            Assert.Equal(0, store.FurniCount(7));
            Assert.Equal([stored], connection.Query<uint>("SELECT item_id FROM items_builders_club"));
        });
    }

    private static void InSchema(Action<MySqlConnection, string> run)
    {
        var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!);
        using var admin = new MySqlConnection(options.ConnectionString);
        admin.Open();
        var schema = "builders_club_" + Guid.NewGuid().ToString("N");
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            options.Database = schema;
            options.AllowUserVariables = true;
            using var connection = new MySqlConnection(options.ConnectionString);
            connection.Open();
            connection.Execute("""
                CREATE TABLE users (id INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;
                INSERT INTO users (id) VALUES (7), (8);
                CREATE TABLE items (id INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, room_id INT NOT NULL DEFAULT 0,
                    base_item INT UNSIGNED NOT NULL, extra_data TEXT NOT NULL, x INT NOT NULL DEFAULT 0, y INT NOT NULL DEFAULT 0, z DOUBLE NOT NULL DEFAULT 0,
                    rot INT NOT NULL DEFAULT 0, wall_pos VARCHAR(100) NULL, limited_number INT NULL DEFAULT 0, limited_stack INT NULL DEFAULT 0) ENGINE=InnoDB;
                CREATE TABLE catalog_pages (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, parent_id INT NULL, caption VARCHAR(128) NOT NULL,
                    icon INT NOT NULL DEFAULT 0, required_permission VARCHAR(191) NULL, position INT NOT NULL DEFAULT 0, link VARCHAR(128) NULL,
                    layout VARCHAR(64) NOT NULL DEFAULT 'default_3x3', visible TINYINT(1) NOT NULL DEFAULT 1, enabled TINYINT(1) NOT NULL DEFAULT 1,
                    required_club_level INT NOT NULL DEFAULT 0, UNIQUE KEY link (link)) ENGINE=InnoDB;
                CREATE TABLE catalog_page_offers (page_id INT NOT NULL, offer_id INT NOT NULL, position INT NOT NULL DEFAULT 0, PRIMARY KEY (page_id, offer_id)) ENGINE=InnoDB;
                CREATE TABLE catalog_page_images (page_id INT NOT NULL, slot INT NOT NULL, image VARCHAR(255) NOT NULL, PRIMARY KEY (page_id, slot)) ENGINE=InnoDB;
                CREATE TABLE catalog_page_texts (page_id INT NOT NULL, slot INT NOT NULL, text TEXT NOT NULL, PRIMARY KEY (page_id, slot)) ENGINE=InnoDB;
                INSERT INTO catalog_pages (id, parent_id, caption, link, layout) VALUES
                    (1, NULL, 'Furni', 'furni', 'default_3x3'),
                    (2, 1, 'Recolorable Blocks', 'bc_blocks_recolorable', 'recolorable_furniture'),
                    (3, 1, 'Recolorable Alphabet Blocks', 'bc_alphabet_recolorable', 'recolorable_furniture');
                INSERT INTO catalog_page_offers (page_id, offer_id, position) VALUES (2, 35203, 1), (2, 35202, 0), (3, 35175, 0);
                INSERT INTO catalog_page_images (page_id, slot, image) VALUES (2, 1, 'catalog_teaser_bcblocks');
                INSERT INTO catalog_page_texts (page_id, slot, text) VALUES (2, 0, 'Blocks');
                ALTER TABLE catalog_pages AUTO_INCREMENT = 1000;
                """);
            run(connection, options.ConnectionString);
        }
        finally {
            admin.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private sealed class ProbeDatabase(string connectionString) : IDatabase
    {
        public IDbConnection Connection() => new MySqlConnection(connectionString);
        public bool IsConnected() => true;
    }
}
