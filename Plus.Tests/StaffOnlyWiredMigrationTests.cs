using System.Text.RegularExpressions;
using Dapper;
using MySqlConnector;
using Xunit;

namespace Plus.Tests;

// Official Habbo sells none of these Wired boxes (no catalogue offer, no Builders Club offer, absent from the public catalogue),
// so only staff may buy them. Catalogue search only finds offers on public pages.
public sealed class StaffOnlyWiredMigrationTests
{
    private const string Migration = "Database/Migrations/100_StaffOnlyUnreleasedWired.sql";

    private static readonly string[] Unreleased =
    [
        "wf_act_move_furni_to", "wf_act_mute_triggerer", "wf_act_progress_ach", "wf_act_send_dataframe", "wf_act_send_message",
        "wf_act_set_highlight", "wf_act_set_visibility", "wf_cnd_not_wearing_b", "wf_cnd_not_wearing_fx", "wf_cnd_wearing_badge",
        "wf_cnd_wearing_effect", "wf_link", "wf_storage_furni_starter", "wf_trg_at_time_long", "wf_trg_receive_dataframe",
        "wf_trg_receive_message", "wf_trg_room_initializes", "wf_var_dailytask", "wf_var_pointer", "wf_var_quest", "wf_var_quest_chain",
        "wf_xtra_achievement_enabler", "wf_xtra_link_speaker", "wf_xtra_var_import", "wf_xtra_var_share"
    ];

    // Offers still unreleased after Habbo began selling Set Highlight, Set Visibility, Room Initialized, Pointer Variable, Import and Share.
    private static readonly int[] UnreleasedOffers =
    [
        1900000318, 1900000319, 1900000320, 1900000321, 1900000322, 1900000324, 1900000325, 1900000326, 1900000327, 1900000328, 1900000333,
        1900000334, 1900000335, 1900000338, 1900000339, 1900000341, 1900000343, 1900000344, 1900000345
    ];

    [RoomComponentDatabaseFact]
    public void UnreleasedWiredMovesToAStaffPageUnderStaffAndPublicWiredStays()
    {
        InSchema(connection =>
        {
            var sql = File.ReadAllText(HabbiconPacketTests.Repo(Migration));
            connection.Execute(sql);
            connection.Execute(sql);

            var staffWired = connection.QuerySingle<(int Id, int? Parent, string? Permission)>(
                "SELECT id, parent_id, required_permission FROM catalog_pages WHERE link = 'staff_wired'");
            Assert.Equal((1, "catalog.pages.administrator"), (staffWired.Parent, staffWired.Permission));
            Assert.Equal([(staffWired.Id, 10), (staffWired.Id, 11), (staffWired.Id, 13)],
                connection.Query<(int, int)>("SELECT page_id, offer_id FROM catalog_page_offers WHERE offer_id IN (10, 11, 13) ORDER BY offer_id, page_id"));
            Assert.Equal([(4, 12)], connection.Query<(int, int)>("SELECT page_id, offer_id FROM catalog_page_offers WHERE offer_id = 12"));
            Assert.Equal(0, connection.QuerySingle<int>("SELECT enabled FROM catalog_pages WHERE link = 'plus_wired_more'"));
        });
    }

    [RoomComponentDatabaseFact]
    public void AHotelWithoutAStaffPageGetsAStaffOnlyPageAtTheTop()
    {
        InSchema(connection =>
        {
            connection.Execute("DELETE FROM catalog_pages WHERE link = 'staff'");

            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo(Migration)));

            Assert.Equal(((int?)null, "catalog.pages.administrator"), connection.QuerySingle<(int?, string?)>(
                "SELECT parent_id, required_permission FROM catalog_pages WHERE link = 'staff_wired'"));
            Assert.Equal(0, connection.QuerySingle<int>(
                "SELECT COUNT(*) FROM catalog_page_offers po JOIN catalog_pages p ON p.id = po.page_id WHERE po.offer_id IN (10, 11, 13) AND p.required_permission IS NULL"));
        });
    }

    [Fact]
    public void TheFreshInstallSellsUnreleasedWiredOnlyOnTheStaffPage()
    {
        var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));

        Assert.Contains("(1700000143,1700000000,'Wired (staff)',80,'catalog.pages.administrator',", fresh);
        Assert.Contains("(2000000017,2000000008,'More Wired',80,NULL,10,'plus_wired_more','default_3x3','NORMAL',1,0,0)", fresh);
        var pages = Regex.Matches(fresh, @"\((\d+),(19000003\d\d),\d+\)").Where(match => UnreleasedOffers.Contains(int.Parse(match.Groups[2].Value)))
            .Select(match => match.Groups[1].Value).Distinct().ToArray();
        Assert.Equal(["1700000143"], pages);
        Assert.Equal(UnreleasedOffers.Length, Regex.Matches(fresh, @"\(1700000143,(19000003\d\d),\d+\)").Count);
        Assert.Equal(25, Unreleased.Length);
    }

    private static void InSchema(Action<MySqlConnection> run)
    {
        var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!);
        using var admin = new MySqlConnection(options.ConnectionString);
        admin.Open();
        var schema = "staff_wired_" + Guid.NewGuid().ToString("N");
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            options.Database = schema;
            using var connection = new MySqlConnection(options.ConnectionString);
            connection.Open();
            connection.Execute("""
                CREATE TABLE catalog_pages (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, parent_id INT NULL, caption VARCHAR(128) NOT NULL,
                    icon INT NOT NULL DEFAULT 1, required_permission VARCHAR(191) NULL, position INT NOT NULL DEFAULT 0, link VARCHAR(128) NULL,
                    layout VARCHAR(64) NOT NULL DEFAULT 'default_3x3', catalog_mode VARCHAR(32) NOT NULL DEFAULT 'NORMAL', visible TINYINT NOT NULL DEFAULT 1,
                    enabled TINYINT NOT NULL DEFAULT 1, required_club_level INT NOT NULL DEFAULT 0) ENGINE=InnoDB;
                CREATE TABLE catalog_page_images (page_id INT NOT NULL, slot INT NOT NULL, image VARCHAR(255) NOT NULL, PRIMARY KEY (page_id, slot)) ENGINE=InnoDB;
                CREATE TABLE catalog_page_texts (page_id INT NOT NULL, slot INT NOT NULL, text TEXT NOT NULL, PRIMARY KEY (page_id, slot)) ENGINE=InnoDB;
                CREATE TABLE catalog_page_offers (page_id INT NOT NULL, offer_id INT NOT NULL, position INT NOT NULL, PRIMARY KEY (page_id, offer_id)) ENGINE=InnoDB;
                CREATE TABLE catalog_offer_products (offer_id INT NOT NULL, furniture_id INT NOT NULL, PRIMARY KEY (offer_id, furniture_id)) ENGINE=InnoDB;
                CREATE TABLE furniture (id INT NOT NULL PRIMARY KEY, item_name VARCHAR(100) NOT NULL) ENGINE=InnoDB;
                INSERT INTO catalog_pages (id, parent_id, caption, required_permission, position, link) VALUES
                    (1, NULL, 'Staff', 'catalog.pages.administrator', 6, 'staff'), (2, NULL, 'Wired', NULL, 0, 'wired2'),
                    (3, 2, 'More Wired', NULL, 10, 'plus_wired_more'), (4, 2, 'New Additions', NULL, 0, 'wired_new');
                INSERT INTO furniture VALUES (100, 'wf_act_progress_ach'), (101, 'wf_xtra_achievement_enabler'), (102, 'conf_chooser_disable'),
                    (103, 'wf_act_set_visibility');
                INSERT INTO catalog_offer_products VALUES (10, 100), (11, 101), (12, 102), (13, 103);
                INSERT INTO catalog_page_offers VALUES (3, 10, 0), (3, 11, 1), (4, 12, 0), (4, 13, 5);
                """);
            run(connection);
        }
        finally {
            admin.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }
}
