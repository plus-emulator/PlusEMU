using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;

namespace Plus.HabboHotel.Items.Wired.Runtime;

/// <summary>
/// Wired context section 7 (TextPlaceholders): the placeholder names a message editor's "add placeholder" list offers,
/// each inserted as $(name), then the names of the stack's capturers. The October client reads two string lists.
/// </summary>
public sealed record WiredTextPlaceholders(ImmutableArray<string> PlaceholderNames, ImmutableArray<string> CapturerNames)
{
    public const int ContextType = 7;

    /// <summary>The placeholder a "user says keyword" trigger brings to its stack, filled with what was said.</summary>
    public const string TriggerTextName = "trigger_text";

    private static readonly string TriggerTextToken = "$(" + TriggerTextName + ")";

    /// <summary>The boxes whose editor is a message editor: Show Message and the two bot talk effects.</summary>
    public static bool Applies(string name) => name is "wf_act_show_message" or "wf_act_bot_talk" or "wf_act_bot_talk_to_avatar";

    /// <summary>The stack on a box's tile, bottom up: the trigger's placeholder, the text output add-ons and the capturers.</summary>
    public static WiredTextPlaceholders Capture(IWiredConfiguredItem box)
    {
        var room = box.Instance;
        var wired = room.GetWired();
        var stack = room.GetGameMap().GetCoordinatedItems(new(box.Item.GetX, box.Item.GetY))
            .OrderBy(item => item.GetZ).ThenBy(item => item.Id)
            .Select(item => wired.TryGet(item.Id, out var found) ? found : null).OfType<IWiredItem>().ToArray();
        var placeholders = new List<string>();
        var capturers = new List<string>();

        if (stack.Any(IsSaysKeyword)) {
            placeholders.Add(TriggerTextName);
        }

        foreach (var configured in stack.OfType<IWiredConfiguredItem>()) {
            var parts = configured.Configuration.Text.Split('\t');

            switch (configured.Descriptor.CanonicalName) {
                case "wf_xtra_text_output_furni_name":
                case "wf_xtra_text_output_username":
                    placeholders.Add(parts[0]);
                    break;
                case "wf_xtra_text_output_variable":
                    placeholders.Add(parts.Length > 1 ? parts[1] : "");
                    break;
                case "wf_xtra_text_input_variable":
                    capturers.Add(parts[0]);
                    break;
            }
        }

        return new([.. placeholders.Where(name => name.Length > 0).Distinct(StringComparer.Ordinal)],
            [.. capturers.Where(name => name.Length > 0).Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>$(trigger_text) is what the user said, when a "user says keyword" trigger started the stack.</summary>
    public static string FillTriggerText(WiredRuntimeContext context, string text) =>
        context.Event.Kind == WiredEventKind.Speech && context.Trigger is { } trigger && IsSaysKeyword(trigger)
            ? text.Replace(TriggerTextToken, context.Event.Message, StringComparison.Ordinal) : text;

    private static bool IsSaysKeyword(IWiredItem box) => box is IWiredConfiguredItem configured
        ? configured.Descriptor.CanonicalName == "wf_trg_says_something"
        : box.Type is WiredBoxType.TriggerUserSays or WiredBoxType.TriggerUserSaysCommand;
}
