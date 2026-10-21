using Xunit;

namespace Plus.Tests;

public sealed class RoomBackgroundSettingsMigrationTests
{
    [Fact]
    public void TheMigrationGrantsTheBrandingPermissionFromSecurityLevelFourAndKeepsExistingValues()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/106_RoomBackgroundSettings.sql"));

        Assert.Contains("INSERT IGNORE INTO role_permissions", sql);
        Assert.Contains("'room.item_save_branding_items' FROM roles WHERE security_level >= 4", sql);
        Assert.Contains("INSERT IGNORE INTO server_settings (`key`, `value`) VALUES ('room.background.allowed_hosts', '')", sql);
    }

    [Fact]
    public void TheFreshInstallGrantsTheBrandingPermissionToEveryRoleFromSecurityLevelFour()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));

        foreach (var role in new[] { 4, 5, 6, 7, 8, 9 }) {
            Assert.Contains($"\n({role},'room.item_save_branding_items'),", sql);
        }

        foreach (var role in new[] { 1, 2, 3 }) {
            Assert.DoesNotContain($"\n({role},'room.item_save_branding_items'),", sql);
        }

        Assert.Contains("\n('room.background.allowed_hosts',''),", sql);
    }
}
