using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

public sealed class ConfigurationFurniInteractionTests
{
    [Theory]
    [InlineData("handitem_block", InteractionType.HandItemBlocker)]
    [InlineData("wired_disable", InteractionType.WiredDisabler)]
    [InlineData("queue_speed", InteractionType.RollerSpeedController)]
    public void TheConfigurationFurniHaveTheirOwnInteractions(string name, InteractionType expected) =>
        Assert.Equal(expected, InteractionTypes.GetTypeFromString(name));

    [Fact]
    public void TheMigrationOnlyRepairsDefaultRows()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/104_ConfigurationFurniInteractions.sql"));

        foreach (var name in new[] { "conf_wired_disable", "conf_queue_speed", "conf_handitem_block" }) {
            Assert.Contains($"item_name = '{name}'", sql);
        }

        Assert.Equal(3, sql.Split("interaction_type IN ('default', 'default_floor',").Length - 1);
    }

    [Fact]
    public void TheFreshInstallUsesTheNewInteractions()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));

        foreach (var (name, interaction) in new[] { ("conf_wired_disable", "wired_disable"), ("conf_queue_speed", "queue_speed"), ("conf_handitem_block", "handitem_block") }) {
            var row = sql.Split('\n').Single(line => line.StartsWith('(') && line.Contains($",'{name}','"));
            Assert.Contains($",'{interaction}',0,", row);
        }
    }
}
