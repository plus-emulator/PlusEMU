using Plus.Core.Settings;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms;

// Staff and ambassadors wear an effect when they enter a room. server_settings picks the effect id hotel wide, and 0 turns it off.
public static class ForcedEntryEffect
{
    public const string StaffSetting = "room.forced_effect.staff";
    public const string AmbassadorSetting = "room.forced_effect.ambassador";

    public static int For(Habbo habbo, ISettingsManager settings)
    {
        if (habbo.DisableForcedEffects) {
            return 0;
        }

        if (habbo.Access.Can(PermissionKeys.ModerationTool)) {
            return Read(settings, StaffSetting, 102);
        }

        return habbo.IsAmbassador ? Read(settings, AmbassadorSetting, 178) : 0;
    }

    private static int Read(ISettingsManager settings, string key, int defaultEffect) =>
        int.TryParse(settings.GetOptionalValue(key), out var effect) && effect >= 0 ? effect : defaultEffect;
}
