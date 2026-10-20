using System.Text.RegularExpressions;
using Dapper;
using MySqlConnector;
using Xunit;

namespace Plus.Tests;

// Migration 101: nine Wired boxes that Habbo started selling on 2026-10-20 are sold under Habbo's offer ids, prices, names and page order.
public sealed class WiredBuildersClubOfferMigrationTests
{
    private const string Migration = "Database/Migrations/101_WiredBuildersClubOffers.sql";

    private static readonly (string Key, int Offer, int Credits)[] Released =
    [
        ("wf_act_set_highlight", 35251, 7), ("wf_act_set_visibility", 35252, 7), ("wf_trg_room_initializes", 35257, 5),
        ("wf_var_pointer", 35259, 6), ("wf_xtra_var_import", 35262, 6), ("wf_xtra_var_share", 35264, 6),
        ("conf_chooser_disable", 35246, 5), ("wf_label", 35253, 3), ("wf_xtra_var_leaderboard", 35263, 15)
    ];

    [RoomComponentDatabaseFact]
    public void ReleasedBoxesTakeOfficialIdsPricesAndPageOrderOnBothCatalogues()
    {
        InSchema(connection =>
        {
            var sql = File.ReadAllText(HabbiconPacketTests.Repo(Migration));
            connection.Execute(sql);

            Assert.Equal(Released.OrderBy(r => r.Offer).Select(r => (r.Offer, r.Key, r.Credits)),
                connection.Query<(int, string, int)>("SELECT id, localization_key, cost_credits FROM catalog_offers WHERE localization_key IN @keys ORDER BY id",
                    new { keys = Released.Select(r => r.Key) }));
            Assert.Equal(0, connection.QuerySingle<int>("SELECT COUNT(*) FROM catalog_offers WHERE id >= 1900000000 AND localization_key IN @keys",
                new { keys = Released.Select(r => r.Key) }));
            // The offer keeps its furni and the promotion that named it follows the new id.
            Assert.Equal(107, connection.QuerySingle<int>("SELECT furniture_id FROM catalog_offer_products WHERE offer_id = 35259"));
            Assert.Equal(35257, connection.QuerySingle<int>("SELECT offer_id FROM catalog_promotions WHERE id = 1"));

            Assert.Equal([(35257, 0), (35252, 1), (35251, 2), (35264, 3), (35262, 4), (35259, 5), (35246, 8), (35263, 9), (35253, 10), (1999, 40)],
                connection.Query<(int, int)>("SELECT offer_id, position FROM catalog_page_offers WHERE page_id = 10 ORDER BY position"));
            Assert.Equal([(35257, 0), (35252, 1), (35251, 2), (35264, 3), (35262, 4), (35259, 5), (35246, 6), (35263, 7), (35253, 8)],
                connection.Query<(int, int)>("SELECT offer_id, position FROM catalog_page_offers WHERE page_id = 20 ORDER BY position"));
            Assert.Equal([(9138, 0), (35257, 20)],
                connection.Query<(int, int)>("SELECT offer_id, position FROM catalog_page_offers WHERE page_id = 11 ORDER BY position"));

            // Boxes Habbo still does not sell stay on the staff page; the released ones leave it.
            Assert.Equal([1900000300], connection.Query<int>("SELECT offer_id FROM catalog_page_offers WHERE page_id = 2"));

            Assert.Equal(["WIRED Effect: Set Highlight", "WIRED Effect: Set Visibility"],
                connection.Query<string>("SELECT name FROM furniture WHERE id IN (101, 102) ORDER BY id"));
            Assert.Equal(["WIRED Effect: Set Highlight", "WIRED Effect: Set Visibility"],
                connection.Query<string>("SELECT public_name FROM furniture WHERE id IN (101, 102) ORDER BY id"));

            var once = Snapshot(connection);
            connection.Execute(sql);
            Assert.Equal(once, Snapshot(connection));
        });
    }

    [RoomComponentDatabaseFact]
    public void AMissingOfferIsCreatedUnderTheOfficialId()
    {
        InSchema(connection =>
        {
            connection.Execute("DELETE FROM catalog_offers WHERE localization_key IN ('wf_var_pointer', 'wf_xtra_var_import')");

            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo(Migration)));

            Assert.Equal([(35259, "wf_var_pointer", 6, 107), (35262, "wf_xtra_var_import", 6, 108)],
                connection.Query<(int, string, int, int)>(
                    "SELECT o.id, o.localization_key, o.cost_credits, p.furniture_id FROM catalog_offers o JOIN catalog_offer_products p ON p.offer_id = o.id " +
                    "WHERE o.id IN (35259, 35262) ORDER BY o.id"));
            Assert.Equal(2, connection.QuerySingle<int>("SELECT COUNT(*) FROM catalog_page_offers WHERE page_id = 10 AND offer_id IN (35259, 35262)"));
        });
    }

    [Fact]
    public void TheFreshInstallSellsTheReleasedBoxesAndStaffKeepsOnlyUnreleasedOnes()
    {
        var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));

        foreach (var (key, offer, credits) in Released) {
            Assert.Contains($"({offer},'{key}',{credits},0,0,0,1,1,'')", fresh);
        }

        Assert.Equal(0, Regex.Matches(fresh, @"\(19000003\d\d,'(wf_act_set_highlight|wf_act_set_visibility|wf_trg_room_initializes|wf_var_pointer|wf_xtra_var_import|wf_xtra_var_share|wf_label|wf_xtra_var_leaderboard)'").Count);
        Assert.Contains("'WIRED Effect: Set Highlight'", fresh);
        Assert.Contains("'WIRED Effect: Set Visibility'", fresh);
        Assert.DoesNotContain("Set Furni Highlight'", fresh);
        Assert.DoesNotContain("Set Furni Transparency'", fresh);

        var normal = PageId(fresh, "wired_new", "NORMAL");
        var builders = PageId(fresh, "wired_new", "BUILDERS_CLUB");
        (int Offer, int Position)[] normalOrder = [(35257, 0), (35252, 1), (35251, 2), (35264, 3), (35262, 4), (35259, 5), (35246, 8), (35263, 9), (35253, 10)];
        (int Offer, int Position)[] builderOrder = [(35257, 0), (35252, 1), (35251, 2), (35264, 3), (35262, 4), (35259, 5), (35246, 6), (35263, 7), (35253, 8)];

        foreach (var (offer, position) in normalOrder) {
            Assert.Contains($"({normal},{offer},{position})", fresh);
        }

        foreach (var (offer, position) in builderOrder) {
            Assert.Contains($"({builders},{offer},{position})", fresh);
        }
    }

    private static string PageId(string fresh, string link, string mode) =>
        Regex.Match(fresh, @"\((\d+),(?:\d+|NULL),'[^']*',\d+,(?:NULL|'[^']*'),\d+,'" + link + @"','[^']*','" + mode + "'").Groups[1].Value;

    private static string Snapshot(MySqlConnection connection) => string.Join("|",
        connection.Query<string>("SELECT CONCAT_WS(',', 'o', id, localization_key, cost_credits, cost_points, points_type, bulk_purchase, enabled) FROM catalog_offers ORDER BY id")
            .Concat(connection.Query<string>("SELECT CONCAT_WS(',', 'p', page_id, offer_id, position) FROM catalog_page_offers ORDER BY page_id, offer_id"))
            .Concat(connection.Query<string>("SELECT CONCAT_WS(',', 'c', offer_id, position, furniture_id, amount) FROM catalog_offer_products ORDER BY offer_id, position"))
            .Concat(connection.Query<string>("SELECT CONCAT_WS(',', 'f', id, name, public_name) FROM furniture ORDER BY id")));

    private static void InSchema(Action<MySqlConnection> run)
    {
        var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!);
        using var admin = new MySqlConnection(options.ConnectionString);
        admin.Open();
        var schema = "wired_bc_offers_" + Guid.NewGuid().ToString("N");
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            options.Database = schema;
            using var connection = new MySqlConnection(options.ConnectionString);
            connection.Open();
            connection.Execute("""
                CREATE TABLE furniture (id INT NOT NULL PRIMARY KEY, item_name VARCHAR(70) NOT NULL, type CHAR(1) NOT NULL DEFAULT 's',
                    public_name VARCHAR(56) NOT NULL, name VARCHAR(56) NOT NULL) ENGINE=InnoDB;
                CREATE TABLE catalog_pages (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, parent_id INT NULL, caption VARCHAR(128) NOT NULL,
                    required_permission VARCHAR(191) NULL, position INT NOT NULL DEFAULT 0, link VARCHAR(128) NULL,
                    catalog_mode ENUM('NORMAL','BUILDERS_CLUB') NOT NULL DEFAULT 'NORMAL', UNIQUE KEY mode_link (catalog_mode, link)) ENGINE=InnoDB;
                CREATE TABLE catalog_offers (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, localization_key VARCHAR(100) NOT NULL,
                    cost_credits INT UNSIGNED NOT NULL DEFAULT 0, cost_points INT UNSIGNED NOT NULL DEFAULT 0, points_type INT UNSIGNED NOT NULL DEFAULT 0,
                    club_level TINYINT UNSIGNED NOT NULL DEFAULT 0, bulk_purchase TINYINT(1) NOT NULL DEFAULT 1, enabled TINYINT(1) NOT NULL DEFAULT 1,
                    preview_image VARCHAR(255) NOT NULL DEFAULT '') ENGINE=InnoDB;
                CREATE TABLE catalog_offer_products (offer_id INT NOT NULL, position TINYINT UNSIGNED NOT NULL, product_type VARCHAR(20) NOT NULL,
                    furniture_id INT UNSIGNED NULL, amount INT UNSIGNED NOT NULL DEFAULT 1, extra_param VARCHAR(1024) NOT NULL DEFAULT '',
                    PRIMARY KEY (offer_id, position),
                    CONSTRAINT fk_products_offer FOREIGN KEY (offer_id) REFERENCES catalog_offers (id) ON DELETE CASCADE ON UPDATE CASCADE) ENGINE=InnoDB;
                CREATE TABLE catalog_page_offers (page_id INT NOT NULL, offer_id INT NOT NULL, position INT NOT NULL DEFAULT 0, PRIMARY KEY (page_id, offer_id),
                    CONSTRAINT fk_page_offers_offer FOREIGN KEY (offer_id) REFERENCES catalog_offers (id) ON DELETE CASCADE ON UPDATE CASCADE,
                    CONSTRAINT fk_page_offers_page FOREIGN KEY (page_id) REFERENCES catalog_pages (id) ON DELETE CASCADE) ENGINE=InnoDB;
                CREATE TABLE catalog_promotions (id INT NOT NULL PRIMARY KEY, offer_id INT NOT NULL DEFAULT -1) ENGINE=InnoDB;

                INSERT INTO furniture (id, item_name, public_name, name) VALUES
                    (100, 'wf_trg_room_initializes', 'WIRED Trigger: Room Initialized', 'WIRED Trigger: Room Initialized'),
                    (101, 'wf_act_set_highlight', 'WIRED Effect: Set Furni Highlight', 'WIRED Effect: Set Furni Highlight'),
                    (102, 'wf_act_set_visibility', 'WIRED Effect: Set Furni Transparency', 'WIRED Effect: Set Furni Transparency'),
                    (103, 'conf_chooser_disable', ':chooser disabler', ':chooser disabler'),
                    (104, 'wf_xtra_achievement_enabler', 'WIRED Add-on: Achievement Enabler', 'WIRED Add-on: Achievement Enabler'),
                    (105, 'wf_trg_walks_on_furni', 'WIRED Trigger: Walks On Furni', 'WIRED Trigger: Walks On Furni'),
                    (106, 'wf_label', 'WIRED Label', 'WIRED Label'),
                    (107, 'wf_var_pointer', 'WIRED Variable: Pointer Variable', 'WIRED Variable: Pointer Variable'),
                    (108, 'wf_xtra_var_import', 'Variable Add-on: Import', 'Variable Add-on: Import'),
                    (109, 'wf_xtra_var_share', 'Variable Add-on: Share', 'Variable Add-on: Share'),
                    (110, 'wf_xtra_var_leaderboard', 'Variable Add-on: Leaderboard', 'Variable Add-on: Leaderboard'),
                    (111, 'my_custom_box', 'Custom', 'Custom');
                INSERT INTO catalog_pages (id, parent_id, caption, required_permission, position, link, catalog_mode) VALUES
                    (1, NULL, 'Staff', 'catalog.pages.administrator', 6, 'staff', 'NORMAL'),
                    (2, 1, 'Wired (staff)', 'catalog.pages.administrator', 0, 'staff_wired', 'NORMAL'),
                    (10, NULL, 'New Additions', NULL, 0, 'wired_new', 'NORMAL'), (11, NULL, 'Wired Triggers', NULL, 1, 'wired_triggers', 'NORMAL'),
                    (20, NULL, 'New Additions', NULL, 0, 'wired_new', 'BUILDERS_CLUB'), (21, NULL, 'Wired Triggers', NULL, 1, 'wired_triggers', 'BUILDERS_CLUB');
                -- The state after migration 100: released boxes only on the staff page under local ids, the rest where migration 82 put them.
                INSERT INTO catalog_offers (id, localization_key, cost_credits) VALUES
                    (1900000336, 'wf_act_set_visibility', 7), (1900000337, 'wf_act_set_highlight', 7), (1900000342, 'wf_trg_room_initializes', 6),
                    (1900000346, 'wf_var_pointer', 7), (1900000347, 'wf_xtra_var_share', 8), (1900000348, 'wf_xtra_var_import', 8),
                    (1900000300, 'wf_xtra_achievement_enabler', 5), (35246, 'conf_chooser_disable', 5), (1900000340, 'wf_label', 3),
                    (1900000349, 'wf_xtra_var_leaderboard', 15), (9138, 'wf_trg_walks_on_furni', 4), (1999, 'my_custom_box', 9);
                INSERT INTO catalog_offer_products (offer_id, position, product_type, furniture_id, amount) VALUES
                    (1900000336, 0, 'furni', 102, 1), (1900000337, 0, 'furni', 101, 1), (1900000342, 0, 'furni', 100, 1), (1900000346, 0, 'furni', 107, 1),
                    (1900000347, 0, 'furni', 109, 1), (1900000348, 0, 'furni', 108, 1), (1900000300, 0, 'furni', 104, 1), (35246, 0, 'furni', 103, 1),
                    (1900000340, 0, 'furni', 106, 1), (1900000349, 0, 'furni', 110, 1), (9138, 0, 'furni', 105, 1), (1999, 0, 'furni', 111, 1);
                INSERT INTO catalog_page_offers (page_id, offer_id, position) VALUES
                    (2, 1900000336, 0), (2, 1900000337, 1), (2, 1900000342, 2), (2, 1900000346, 3), (2, 1900000347, 4), (2, 1900000348, 5), (2, 1900000300, 6),
                    (10, 35246, 2), (10, 1900000349, 3), (10, 1900000340, 4), (10, 1999, 40), (11, 9138, 0),
                    (20, 35246, 0), (20, 1900000349, 1), (20, 1900000340, 2), (21, 9138, 0);
                INSERT INTO catalog_promotions (id, offer_id) VALUES (1, 1900000342);
                """);
            run(connection);
        }
        finally {
            admin.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }
}
