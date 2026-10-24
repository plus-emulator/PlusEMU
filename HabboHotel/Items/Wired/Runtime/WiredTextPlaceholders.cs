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

    /// <summary>Whether a box text inserts $(trigger_text).</summary>
    public static bool UsesTriggerText(string? text) => text?.Contains(TriggerTextToken, StringComparison.Ordinal) == true;

    /// <summary>The boxes whose editor is a message editor: Show Message, the two bot talk effects and Send Link Message.</summary>
    public static bool Applies(string name) => name is "wf_act_show_message" or "wf_act_bot_talk" or "wf_act_bot_talk_to_avatar" or "wf_act_send_message";

    /// <summary>
    /// The stack on a box's tile, bottom up: the trigger's placeholder, the text output add-ons and the capturers. Placeholder and capturer
    /// names go out sorted by name.
    /// </summary>
    public static WiredTextPlaceholders Capture(IWiredConfiguredItem box)
    {
        var room = box.Instance;
        var wired = room.GetWired();
        var stack = room.GetGameMap().GetCoordinatedItems(new(box.Item.GetX, box.Item.GetY))
            .OrderBy(item => item.GetZ).ThenBy(item => item.Id)
            .Select(item => wired.TryGet(item.Id, out var found) ? found : null).OfType<IWiredItem>().ToArray();
        var placeholders = new List<string>();
        var capturers = new List<string>();

        if (stack.Any(box => IsSaysKeyword(box) || box is IWiredConfiguredItem { Descriptor.CanonicalName: "wf_trg_receive_message" })) {
            placeholders.Add(TriggerTextName);
        }

        foreach (var receiver in stack.OfType<IWiredConfiguredItem>().Where(box => box.Descriptor.CanonicalName == "wf_trg_receive_dataframe")) {
            placeholders.AddRange(DataframePlaceholders(receiver.Configuration).Select(row => row.Placeholder));
        }

        placeholders.AddRange(wired.GlobalPlaceholders().Keys);

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

        return new([.. placeholders.Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            [.. capturers.Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// $(trigger_text) is what the user said, when a "user says keyword" trigger started the stack, or the link message a Receive Link
    /// Message trigger received. A Receive Link Dataframe trigger fills its placeholder rows from the dataframe's keys, "null" for a key the dataframe lacks, and the room's
    /// Global Placeholders fill their own names.
    /// </summary>
    public static string FillTriggerText(WiredRuntimeContext context, string text)
    {
        if (context.Event.Kind == WiredEventKind.Speech && context.Trigger is { } trigger && IsSaysKeyword(trigger)
            || context.Event.Kind == WiredEventKind.LinkMessage && context.Trigger is IWiredConfiguredItem { Descriptor.CanonicalName: "wf_trg_receive_message" }) {
            text = text.Replace(TriggerTextToken, context.Event.Message, StringComparison.Ordinal);
        }

        if (!text.Contains("$(", StringComparison.Ordinal)) {
            return text;
        }

        if (context.Event is { Kind: WiredEventKind.LinkDataframe, Link: { } link }
            && context.Trigger is IWiredConfiguredItem { Descriptor.CanonicalName: "wf_trg_receive_dataframe" } receiver) {
            foreach (var (key, placeholder) in DataframePlaceholders(context.ConfigurationOf(receiver))) {
                var shown = link.Rows.FirstOrDefault(row => row.Key == key) is { } value ? value.Text : "null";
                text = text.Replace("$(" + placeholder + ")", shown, StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach (var (name, value) in context.Room.GetWired()?.GlobalPlaceholders() ?? new Dictionary<string, string>()) {
            text = text.Replace("$(" + name + ")", value, StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    // Kind 0 rows of a Receive Link Dataframe trigger: the normalized key and the placeholder it fills.
    private static IEnumerable<(string Key, string Placeholder)> DataframePlaceholders(WiredConfiguration config) =>
        Plus.HabboHotel.Items.Wired.Links.WiredLinkDataframeCodec.Decode(config.Text, config.IntParams.Length > 0 ? config.IntParams[0] : 0) is { } rows
            ? rows.Select((row, index) => (Row: row, Index: index))
                .Where(pair => config.IntParams.Length > 1 + pair.Index && config.IntParams[1 + pair.Index] == 0)
                .Select(pair => (Plus.HabboHotel.Items.Wired.Links.WiredLinkDataframeCodec.NormalizeKey(pair.Row.Key), pair.Row.Text))
            : [];

    private static bool IsSaysKeyword(IWiredItem box) => box is IWiredConfiguredItem configured
        ? configured.Descriptor.CanonicalName == "wf_trg_says_something"
        : box.Type is WiredBoxType.TriggerUserSays or WiredBoxType.TriggerUserSaysCommand;
}
