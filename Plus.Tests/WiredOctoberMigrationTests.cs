using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>Migration 81: the October boxes, WIRED Label and WIRED Link get furniture rows, interactions, offers and tables.</summary>
public sealed class WiredOctoberMigrationTests
{
    private static string Read(string path) => File.ReadAllText(HabbiconPacketTests.Repo(path)).Replace("\r\n", "\n");

    private static readonly string Migration = Read("Database/Migrations/81_WiredOctoberBoxes.sql");

    [Theory]
    [InlineData("wf_act_progress_ach", 19393, "wired_effect")]
    [InlineData("wf_act_send_dataframe", 19394, "wired_effect")]
    [InlineData("wf_act_send_message", 19395, "wired_effect")]
    [InlineData("wf_act_set_highlight", 19396, "wired_effect")]
    [InlineData("wf_act_set_visibility", 19397, "wired_effect")]
    [InlineData("wf_label", 19398, "wired_label")]
    [InlineData("wf_link", 19399, "wired_link")]
    [InlineData("wf_trg_receive_dataframe", 19400, "wired_trigger")]
    [InlineData("wf_trg_receive_message", 19401, "wired_trigger")]
    [InlineData("wf_trg_room_initializes", 19402, "wired_trigger")]
    [InlineData("wf_var_dailytask", 19403, "wired_variable")]
    [InlineData("wf_var_pointer", 19404, "wired_variable")]
    [InlineData("wf_xtra_achievement_enabler", 19405, "wired_addon")]
    [InlineData("wf_xtra_link_speaker", 19406, "wired_addon")]
    [InlineData("wf_xtra_var_import", 19407, "wired_addon")]
    [InlineData("wf_xtra_var_leaderboard", 19408, "wired_addon")]
    [InlineData("wf_xtra_var_share", 19409, "wired_addon")]
    [InlineData("wf_xtra_var_web_api", 19190, "wired_addon")]
    public void EveryFurniHasItsOfficialSpriteInteractionAndOffer(string classname, int sprite, string interaction)
    {
        // The furnidata row ends on its interaction; the offer plan row names a Wired page.
        Assert.Matches($"\\('{classname}', {sprite}, '[^']+', '[^']+', \\d+, [^\\n]*'{interaction}'\\)", Migration);
        Assert.Matches($"\\('{classname}', {sprite}, 'wired_[a-z_]+', \\d+, 1, \\d+\\)", Migration);

        // The interaction resolves to the engine type: the boxes through their canonical item name, the two furni by name.
        var type = InteractionTypes.GetTypeFromString(interaction);
        Assert.NotEqual(InteractionType.None, type);

        if (interaction.StartsWith("wired_", StringComparison.Ordinal) && classname.StartsWith("wf_", StringComparison.Ordinal)
            && classname is not ("wf_label" or "wf_link")) {
            Assert.True(WiredBoxRegistry.TryGet(classname, out _));
        }
    }

    [Theory]
    [InlineData("wired_labels")]
    [InlineData("wired_link_networks")]
    [InlineData("wired_link_endpoints")]
    [InlineData("wired_web_api_keys")]
    public void RuntimeTablesAreCreatedIdempotently(string table) => Assert.Contains($"CREATE TABLE IF NOT EXISTS `{table}`", Migration);

    [Fact]
    public void WebApiKeysAreStoredAsHashesOnly()
    {
        Assert.Contains("`read_key_hash` CHAR(64)", Migration);
        Assert.DoesNotContain("`read_key` ", Migration);
    }

    [Fact]
    public void OriginalDatabaseCarriesTheMigration() =>
        Assert.Contains("-- 81_WiredOctoberBoxes\n" + Migration, Read("Resources/SQLs/Original Database.sql"));
}
