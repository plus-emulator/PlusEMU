using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Links;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Addons;

/// <summary>
/// The October add-ons of the r88 dossier (WIN63-202610131318-860371703). Most of them are room-wide settings read where they apply:
/// Achievement Enabler by Progress Achievement, Global Placeholder by message text, Web API by the HTTP variable API, Leaderboard by
/// the room's overlays, Only the Link Speaker joins its stack's firing.
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

    public bool Apply(WiredRuntimeContext context)
    {
        var configuration = context.ConfigurationOf(this);

        // class_4123.as [message, dataframe]: the speaker's picked WIRED Link furni speak for the stack's send effects.
        if (Descriptor.CanonicalName == "wf_xtra_link_speaker" && configuration.IntParams is [var message, var dataframe, ..]) {
            var speakers = context.Targets.ResolveFurni(context, configuration.SelectedItems, configuration.FurniSources.GetValueOrDefault("speakers"))
                .Where(WiredLinkTransport.IsLink).ToArray();

            if (speakers.Length > 0) {
                context.Policy.Addons.LinkSpeakers.Add(new(message == 1, dataframe == 1, speakers));
            }
        }

        return true;
    }

    public void Reset() { }
}
