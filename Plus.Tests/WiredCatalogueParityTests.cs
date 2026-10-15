using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

/// <summary>Migration 82: the Wired catalogue matches hhus (WIN63-202610131318) and the :chooser disabler is sold and works.</summary>
public sealed class WiredCatalogueParityTests
{
    private static string Read(string path) => File.ReadAllText(HabbiconPacketTests.Repo(path)).Replace("\r\n", "\n");

    private static readonly string Migration = Read("Database/Migrations/82_WiredCatalogueParity.sql");

    [Fact]
    public void TheChooserDisablerHasItsOwnInteraction()
    {
        Assert.Equal(InteractionType.ChooserDisabler, InteractionTypes.GetTypeFromString("chooser_disable"));
        Assert.Contains("SELECT 'conf_chooser_disable', ':chooser disabler', 's', 1, 1, 2.0, 0, 0, 0, 19391,", Migration);
        Assert.Contains("interaction_type IN ('', 'default', 'default_floor', 'conf_chooser_disable')", Migration);
    }

    [Fact]
    public void NewAdditionsListsTheOfficialOffersInOfficialOrder()
    {
        string[] expected =
        [
            "('wired_new', 0, 34965, 'wf_xtra_var_web_api', 150, 150, 5, 1, 19190, 1, 1)",
            "('wired_new', 1, 35273, 'wf_xtra_var_web_api_deal1', 300, 300, 5, 0, 19190, 3, 1)",
            "('wired_new', 2, 35246, 'conf_chooser_disable', 5, 0, 0, 1, 19391, 1, 1)",
            "('wired_new', 3, 35263, 'wf_xtra_var_leaderboard', 15, 0, 0, 1, 19408, 1, 1)",
            "('wired_new', 4, 35253, 'wf_label', 3, 0, 0, 1, 19398, 1, 1)",
            "('wired_new', 5, 34996, 'wf_xtra_varfx_hp', 15, 0, 0, 1, 19196, 1, 1)",
            "('wired_new', 10, 34995, 'wf_xtra_varfx_boss', 20, 0, 0, 1, 19195, 1, 1)"
        ];

        foreach (var row in expected) {
            Assert.Contains(row, Migration);
        }
    }

    [Theory]
    [InlineData("wired_new", 11)]
    [InlineData("wired_triggers", 20)]
    [InlineData("wired_effects", 39)]
    [InlineData("wired_conditions", 29)]
    [InlineData("wired_selectors", 20)]
    [InlineData("wired_addons", 13)]
    [InlineData("wired_variables_wired", 14)]
    [InlineData("wired_variables", 6)]
    [InlineData("wired_variables_addons", 6)]
    [InlineData("sept26variable", 6)]
    [InlineData("wired_chests", 4)]
    [InlineData("wired_chests_wired", 10)]
    [InlineData("wired_chests_contracts", 4)]
    [InlineData("wired_extras", 8)]
    [InlineData("wired_archive", 12)]
    [InlineData("wired_leaderboard", 21)]
    [InlineData("configitems", 6)]
    public void EveryOfficialPageHasItsOfficialOfferCount(string link, int count)
    {
        for (var position = 0; position < count; position++) {
            Assert.Contains($"('{link}', {position}, ", Migration);
        }

        Assert.DoesNotContain($"('{link}', {count}, ", Migration);
    }

    [Fact]
    public void LeaderboardsMoveToGamingAndWiredArchiveIsCreated()
    {
        Assert.Contains("('wired_leaderboard', 'gaming', 6, 256, 'Leaderboards')", Migration);
        Assert.Contains("('wired_archive', 'wired2', 9, 80, 'Wired Archive')", Migration);
        Assert.Contains("NOT EXISTS (SELECT 1 FROM catalog_pages c WHERE c.link = 'wired_archive')", Migration);
    }

    [Fact]
    public void WiredArchiveTextMatchesHabbo()
    {
        var archive = Read("Database/Migrations/83_WiredArchiveText.sql");
        Assert.Contains("SET t.text = 'Archived Wired boxes that are still available for legacy reasons, but are not recommended to use in new Wired setups'", archive);
        Assert.Contains("-- 83_WiredArchiveText\n" + archive, Read("Resources/SQLs/Original Database.sql"));
    }

    [Fact]
    public void OriginalDatabaseCarriesTheMigration() =>
        Assert.Contains("-- 82_WiredCatalogueParity\n" + Migration, Read("Resources/SQLs/Original Database.sql"));

    [Fact]
    public void FreshInstallCarriesTheChooserDisablerAndItsOffers()
    {
        var fresh = Read("Database/FreshInstall.sql");
        Assert.Contains("'conf_chooser_disable',':chooser disabler','s',1,1,2,0,0,0,19391,", fresh);
        Assert.Contains("(35246,'conf_chooser_disable',5,0,0,0,1,1,'')", fresh);
        Assert.Contains("(35273,'wf_xtra_var_web_api_deal1',300,300,5,0,0,1,'')", fresh);
        Assert.Contains("'Wired Archive'", fresh);
        Assert.Contains("(2000000016,0,'Archived Wired boxes that are still available for legacy reasons, but are not recommended to use in new Wired setups')", fresh);
    }
}
