using Plus.Core.Settings;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public sealed class ForcedEntryEffectTests
{
    [Fact]
    public void Staff_and_ambassadors_get_the_default_effects_without_settings()
    {
        Assert.Equal(102, ForcedEntryEffect.For(User(PermissionKeys.ModerationTool), Settings()));
        Assert.Equal(178, ForcedEntryEffect.For(User(PermissionKeys.Ambassador), Settings()));
        Assert.Equal(102, ForcedEntryEffect.For(User(PermissionKeys.ModerationTool, PermissionKeys.Ambassador), Settings()));
        Assert.Equal(0, ForcedEntryEffect.For(User(), Settings()));
    }

    [Fact]
    public void Zero_turns_the_effect_off_for_every_staff_member()
    {
        var settings = Settings(("room.forced_effect.staff", "0"), ("room.forced_effect.ambassador", "0"));

        Assert.Equal(0, ForcedEntryEffect.For(User(PermissionKeys.ModerationTool), settings));
        Assert.Equal(0, ForcedEntryEffect.For(User(PermissionKeys.Ambassador), settings));
    }

    [Fact]
    public void A_hotel_can_choose_another_effect()
    {
        var settings = Settings(("room.forced_effect.staff", "187"));

        Assert.Equal(187, ForcedEntryEffect.For(User(PermissionKeys.ModerationTool), settings));
        Assert.Equal(178, ForcedEntryEffect.For(User(PermissionKeys.Ambassador), settings));
    }

    [Fact]
    public void An_invalid_value_keeps_the_default_effect()
    {
        Assert.Equal(102, ForcedEntryEffect.For(User(PermissionKeys.ModerationTool), Settings(("room.forced_effect.staff", "abc"))));
        Assert.Equal(102, ForcedEntryEffect.For(User(PermissionKeys.ModerationTool), Settings(("room.forced_effect.staff", "-1"))));
    }

    [Fact]
    public void A_user_who_turned_forced_effects_off_gets_none()
    {
        var staff = User(PermissionKeys.ModerationTool);
        staff.DisableForcedEffects = true;

        Assert.Equal(0, ForcedEntryEffect.For(staff, Settings(("room.forced_effect.staff", "187"))));
    }

    private static Habbo User(params string[] keys) => new()
    {
        Access = UserAccess.Create([], keys.Select(key => new UserPermissionOverride(key, false)), keys)
    };

    private static ISettingsManager Settings(params (string Key, string Value)[] values) => new DictionarySettings(values.ToDictionary(v => v.Key, v => v.Value));

    private sealed class DictionarySettings(Dictionary<string, string> values) : ISettingsManager
    {
        public string TryGetValue(string value) => values.GetValueOrDefault(value, "0");
        public string? GetOptionalValue(string key) => values.GetValueOrDefault(key);
        public Task Reload() => Task.CompletedTask;
    }
}
