using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Addons;

/// <summary>
/// The October add-ons. Most of them are room-wide settings read where they apply: Achievement Enabler by Progress Achievement,
/// Global Placeholder by message text, Web API by the HTTP variable API, Leaderboard by the room's overlays, Link Speaker by the link transport.
/// </summary>
public sealed class WiredOctoberAddonBox(Room room, Item item, WiredBoxDescriptor descriptor)
    : WiredConfiguredBehaviorBox(room, item, descriptor, configuration => configuration), IWiredContextualAddon
{
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "wf_xtra_var_leaderboard", "wf_xtra_global_placeholder",
        "wf_xtra_achievement_enabler", "wf_xtra_var_web_api", "wf_xtra_link_speaker"
    };

    public bool AfterConditions => false;

    public bool Apply(WiredRuntimeContext context) => true;

    public void Reset() { }
}
