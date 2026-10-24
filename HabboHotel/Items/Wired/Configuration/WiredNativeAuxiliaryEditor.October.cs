using System.Collections.Immutable;
using System.Text;
using Plus.HabboHotel.Items.Wired.Leaderboard;
using Plus.HabboHotel.Items.Wired.Links;
using Plus.HabboHotel.Items.Wired.Variables;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// The October boxes. Every grammar is the official editor's own save. Merged selections pair a furni and a user source slot,
/// so a value-or-variable reference adds one of each after the box's own targets.
/// </summary>
internal static partial class WiredNativeAuxiliaryEditor
{
    // Properties, not fields: the spec table in the other part of this class is built before this part's static fields.
    private static ImmutableArray<int> TargetFurni => [0, 100, 200, 201];
    private static ImmutableArray<int> TargetUsers => [0, 200, 201];
    private static ImmutableArray<int> LinkReceiveFurni => [100, 200];
    private static ImmutableArray<int> LinkRowFurni => [0, 100, 101, 200, 201];

    private static WiredConfiguration Roles(WiredConfiguration draft, (string Role, int Source)[] furni, (string Role, int Source)[] users) => draft with
    {
        FurniSources = furni.ToImmutableDictionary(pair => pair.Role, pair => pair.Source),
        UserSources = users.ToImmutableDictionary(pair => pair.Role, pair => pair.Source)
    };

    // A value-or-variable: 0 literal or 1 variable, the literal and the variable's target. An unchosen variable is the inactive default.
    private static bool ValueOrVariable(int option, int target) => option is 0 or 1 && TryTarget(target, out _);

    private static IEnumerable<KeyValuePair<string, Spec>> October()
    {
        // [progression (1 add, 0 set), option, number 0..2147483647, target]; achievement name; the variable.
        yield return new("wf_act_progress_ach", new(Sources([TargetFurni], [TargetUsers, TargetUsers], [1, 0, 0, -10], 1) with
        {
            FurniDefaults = [0]
        }, n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 4, 1, 2, 1) && p[0] is 0 or 1 && ValueOrVariable(p[1], p[3]) && p[2] >= 0 && n.Text.Length <= 100
                && n.Text.IndexOfAny(['\t', '\r', '\n']) < 0 && TryTarget(p[3], out var target)
                ? Roles(Draft(n, [p[0], p[1], p[2], target], n.Text), [("reference", n.FurniSourceTypes[0])],
                    [("users", n.UserSourceTypes[0]), ("reference", n.UserSourceTypes[1])]) : null;
        }));
        // [effect id 0..10000, priority 0..2, type 0/1].
        yield return new("wf_act_give_effect", new(Sources([], [TargetUsers], [0, 0, 0]), n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 3, 0, 1, 0) && n.Text.Length == 0 && In(p[0], 0, 10000) && In(p[1], 0, 2) && p[2] is 0 or 1
                ? Roles(Draft(n, p), [], [("users", n.UserSourceTypes[0])]) : null;
        }));
        // [height 0..8000 in hundredths, type (0 override, 1 restore)].
        yield return new("wf_act_override_height", new(Sources([TargetFurni], [], [0, 0]) with { FurniDefaults = [100] }, n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 2, 1, 0, 0) && n.Text.Length == 0 && In(p[0], 0, 8000) && p[1] is 0 or 1
                ? Roles(Draft(n, p), [("items", n.FurniSourceTypes[0])], []) : null;
        }));
        // [add to existing, option, score 1..2147483647, target]; track id TAB task id ([A-Za-z0-9_], 100 each).
        yield return new("wf_act_progress_reward_track", new(Sources([TargetFurni], [TargetUsers, TargetUsers], [1, 0, 1, -10], 1) with
        {
            FurniDefaults = [0]
        }, n =>
        {
            var p = n.OwnedIntParams;
            var parts = n.Text.Split('\t');

            return Shape(n, 4, 1, 2, 1) && p[0] is 0 or 1 && ValueOrVariable(p[1], p[3]) && (p[1] == 1 ? p[2] >= 0 : p[2] >= 1)
                && (n.Text.Length == 0 || parts.Length == 2 && parts.All(TrackId)) && TryTarget(p[3], out var target)
                ? Roles(Draft(n, [p[0], p[1], p[2], target], n.Text), [("reference", n.FurniSourceTypes[0])],
                    [("users", n.UserSourceTypes[0]), ("reference", n.UserSourceTypes[1])]) : null;
        }));
        // The track id (at most 100, no TAB); no ints.
        yield return new("wf_act_reset_reward_track", new(Sources([], [TargetUsers], []), n =>
            Shape(n, 0, 0, 1, 0) && (n.Text.Length == 0 || TrackId(n.Text))
                ? Roles(Draft(n, [], n.Text), [], [("users", n.UserSourceTypes[0])]) : null));
        // A stored row keeps the wider bounds it was saved under; the stricter value rules are checked on a new save only (WiredFurniVisualRules).
        // Both editors offer wall furni.
        // [audience (1 everyone), visibility 0/100, click through, easing 0..4, target users]; merged [[0,1],[-1,0]]:
        // furni 0 with user 1 are the targets, user 0 the audience.
        yield return new("wf_act_set_visibility", new(Sources([TargetFurni], [TargetUsers, TargetUsers], [0, 100, 0, 4, 0]) with
        {
            FurniDefaults = [100],
            AllowWall = true
        }, n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 5, 1, 2, 0) && n.Text.Length == 0
                ? Roles(Draft(n, p), [("items", n.FurniSourceTypes[0])], [("audience", n.UserSourceTypes[0]), ("targets", n.UserSourceTypes[1])]) : null;
        }));
        // [audience, type 0..5, strength, colour, animation 0/1, easing 0..4, target users]; the strengths and colours
        // of each type are WiredFurniVisualRules.
        yield return new("wf_act_set_highlight", new(Sources([TargetFurni], [TargetUsers, TargetUsers], [0, 0, 100, 16771584, 0, 4, 0]) with
        {
            FurniDefaults = [100],
            AllowWall = true
        }, n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 7, 1, 2, 0) && n.Text.Length == 0
                ? Roles(Draft(n, p), [("items", n.FurniSourceTypes[0])], [("audience", n.UserSourceTypes[0]), ("targets", n.UserSourceTypes[1])]) : null;
        }));
        // [source (0 the trigger's text, 1 custom)]; the message, at most 1000 UTF-8 bytes. Furni 0 picks the endpoints.
        yield return new("wf_act_send_message", new(Sources([TargetFurni], [], [0]) with { FurniDefaults = [100] }, n =>
            Shape(n, 1, 1, 0, 0) && n.OwnedIntParams[0] is 0 or 1 && WiredLinkDataframeCodec.Utf8Length(n.Text) <= WiredLinkDataframeCodec.MaxLinkMessageBytes
                ? Roles(Draft(n, n.OwnedIntParams, n.Text), [("endpoints", n.FurniSourceTypes[0])], []) : null));
        // [count, 12 x (mode, literal, target)] = 37 ints, JSON [key, text] rows, 12 variable ids.
        // Merged rows pair furni i+1 with user i; furni 0 picks the endpoints.
        yield return new("wf_act_send_dataframe", new(Sources([.. Enumerable.Repeat(LinkRowFurni, 13)],
            [.. Enumerable.Repeat(TargetUsers, 12)], [0, .. Enumerable.Range(0, 12).SelectMany(_ => new[] { 0, 0, -20 })], 12) with
        {
            FurniDefaults = [100, .. Enumerable.Repeat(200, 12)],
            AllowWall = true
        }, SendDataframe));
        yield return new("wf_trg_room_initializes", new(Meta(0, 0, []), n => Shape(n, 0, 0, 0, 0) && n.Text.Length == 0 ? Draft(n, []) : null));
        // Furni 0 picks the endpoints it listens on.
        yield return new("wf_trg_receive_message", new(Sources([LinkReceiveFurni], [], []) with { FurniDefaults = [100] }, n =>
            Shape(n, 0, 1, 0, 0) && n.Text.Length == 0 ? Roles(Draft(n, []), [("endpoints", n.FurniSourceTypes[0])], []) : null));
        // [count, 12 x kind] = 13 ints, JSON [key, placeholder] rows, 12 variable ids.
        yield return new("wf_trg_receive_dataframe", new(Sources([LinkReceiveFurni], [], [0, .. Enumerable.Repeat(0, 12)], 12) with
        {
            FurniDefaults = [100]
        }, ReceiveDataframe));
        // [level 1..30, comparison 0 lower, 1 equal, 2 higher]. LevelMatches overrides only code, inputMode, buildInputs,
        // onEditStart and readIntParamsFromForm, exactly like the badge condition: its user source picker and
        // user quantifier come from the server's footer, so it advertises the footer of the other user conditions.
        yield return new("wf_cnd_user_level", new(Sources([], [[0, 200, 201]], [1, 1]) with { QuantifierType = 2 }, n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 2, 0, 1, 0) && n.Text.Length == 0 && In(p[0], 1, 30) && In(p[1], 0, 2)
                ? Roles(Draft(n, [p[0], p[1], n.Quantifier ?? 0]), [], [("users", n.UserSourceTypes[0])]) : null;
        }));

        // Base of the share and import add-ons: [text connectors, subvariable creators, variable FX], each 0/1.
        // The flags and the string are put in their stored form first (see StoredForm).
        foreach (var name in new[] { "wf_xtra_var_share", "wf_xtra_var_import" }) {
            yield return new(name, new(Meta(0, 0, [1, 1, 1]), n =>
                n.FurniSourceTypes.Length == 0 && n.UserSourceTypes.Length == 0 && n.VariableIds.Length == 0 ? Draft(n, n.OwnedIntParams) : null));
        }

        // 19 ints, 12 variable ids, 17 TAB-joined fields (empty while unpublished).
        yield return new("wf_xtra_var_leaderboard", new(Meta(0, 0, WiredLeaderboardConfig.DefaultInts) with
        {
            VariableDefaults = [.. Enumerable.Repeat(WiredLeaderboardConfig.NoVariable, WiredLeaderboardConfig.VariableCount)]
        }, n => n.FurniSourceTypes.Length == 0 && n.UserSourceTypes.Length == 0
            && WiredLeaderboardConfig.Decode(n.OwnedIntParams, n.VariableIds, n.Text) is not null
                ? Draft(n, n.OwnedIntParams, WiredLeaderboardConfig.Normalize(n.Text, n.OwnedIntParams)) : null));
        // [mode (0 custom text, 1 room), 0, room index]; placeholder name TAB text.
        yield return new("wf_xtra_global_placeholder", new(Meta(0, 0, [0, 0, 0]), n =>
        {
            var p = n.OwnedIntParams;
            var parts = n.Text.Split('\t', 2);

            return Shape(n, 3, 0, 0, 0) && p[0] is 0 or 1 && p[1] == 0 && p[2] >= 0 && (p[0] == 1 || p[2] == 0)
                && (n.Text.Length == 0 || WiredLinkDataframeCodec.IsPlaceholder(parts[0]) && (parts.Length == 1 || parts[1].Length <= 1000))
                ? Draft(n, p, n.Text) : null;
        }));
        // The text: one achievement per line, at most 2000 characters.
        yield return new("wf_xtra_achievement_enabler", new(Meta(0, 0, []), n => Shape(n, 0, 0, 0, 0) && n.Text.Length <= 2000
            ? Draft(n, [], n.Text) : null));
        // The text: read key TAB write key. The int: [bulk delete], only with a write key. Keys are the server's own.
        yield return new("wf_xtra_var_web_api", new(Meta(0, 0, [0]), n =>
        {
            var parts = n.Text.Split('\t');

            return Shape(n, 1, 0, 0, 0) && n.OwnedIntParams[0] is 0 or 1 && (n.Text.Length == 0 || parts.Length == 2
                && parts.All(key => key.Length == 0 || WiredWebApiKeyFormat.IsKey(key)))
                && (n.OwnedIntParams[0] == 0 || parts.Length == 2 && parts[1].Length > 0) ? Draft(n, n.OwnedIntParams, n.Text) : null;
        }));
        // [message, dataframe]; the speaker always speaks for its picked links and has no furni source.
        yield return new("wf_xtra_link_speaker", new(Sources([], [], [1, 1]), n =>
            Shape(n, 2, 0, 0, 0) && n.Text.Length == 0 && n.OwnedIntParams.All(flag => flag is 0 or 1)
                ? Roles(Draft(n, n.OwnedIntParams), [("speakers", WiredSources.Selected)], []) : null));
        // The text: variable name TAB task text (at most 100). No ints or variables.
        yield return new("wf_var_dailytask", new(Meta(0, 0, []), n =>
        {
            var parts = n.Text.Split('\t');

            return Shape(n, 0, 0, 0, 0) && parts.Length <= 2 && (parts.Length == 1 || parts[1].Length <= 100) ? Draft(n, [], n.Text) : null;
        }));
        // [source type (0 furni, 1 user, -10 global, -20 context)], the accessor (a context variable) and the name.
        // The ints and the name are put in their stored form first (see StoredForm). The accessor is checked against the room's variables
        // when the box validates the save.
        yield return new("wf_var_pointer", new(Meta(0, 0, [1], 1), n =>
            n.FurniSourceTypes.Length == 0 && n.UserSourceTypes.Length == 0 && n.VariableIds.Length == 1 ? Draft(n, n.OwnedIntParams, n.Text) : null));
    }

    /// <summary>
    /// The form a save is stored in, and the form the editor opens on again.
    /// <list type="bullet">
    /// <item>Share and Import: any non-zero flag is stored as 1, a missing flag stays on, a fourth int is dropped and a string is accepted
    /// and not kept.</item>
    /// <item>Pointer: a fresh pointer opens on the user target. A target the form does not offer is stored as furni, missing ints give the
    /// user target and extra ints are dropped. A space in the name becomes an underscore. A second variable is refused with the count key
    /// (WiredEditorStoreRules).</item>
    /// </list>
    /// </summary>
    internal static WiredNativeEditorConfiguration StoredForm(string name, WiredNativeEditorConfiguration native)
    {
        switch (name) {
            case "wf_xtra_var_share" or "wf_xtra_var_import":
                return native with
                {
                    OwnedIntParams = [.. Enumerable.Range(0, 3).Select(index => index < native.OwnedIntParams.Length ? (native.OwnedIntParams[index] != 0 ? 1 : 0) : 1)],
                    Text = ""
                };
            // The speaker had a furni source before it lost it, and any non-zero flag is stored as 1.
            case "wf_xtra_link_speaker":
                return native with
                {
                    FurniSourceTypes = [],
                    OwnedIntParams = native.OwnedIntParams.Length == 2 ? [.. native.OwnedIntParams.Select(flag => flag != 0 ? 1 : 0)] : native.OwnedIntParams
                };
            case "wf_trg_receive_dataframe" when native.VariableIds.IsEmpty:
                return StoredForm(name, native with { VariableIds = [.. Enumerable.Repeat(WiredVariableAbsent.WireId, 12)] });
            case "wf_trg_receive_dataframe" when !native.NeverSaved && WiredLinkDataframeCodec.Decode(native.Text, native.OwnedIntParams.IsEmpty ? -1 : native.OwnedIntParams[0]) is { } rows
                && native.OwnedIntParams.Length == 13:
                return native with
                {
                    Text = WiredLinkDataframeCodec.Encode(rows.Select((row, index) => (WiredLinkDataframeCodec.NormalizeKey(row.Key),
                        native.OwnedIntParams[1 + index] == WiredLinkDataframeCodec.PlaceholderDestination ? row.Text.ToLowerInvariant() : row.Text)))
                };
            case "wf_act_send_dataframe" when !native.NeverSaved && native.OwnedIntParams.Length == 37
                && WiredLinkDataframeCodec.Decode(native.Text, native.OwnedIntParams[0]) is { Length: 0 }:
                return native with { Text = WiredLinkDataframeCodec.Encode([]) };
            case "wf_var_pointer":
                var requested = native.OwnedIntParams.IsEmpty ? 1 : native.OwnedIntParams[0];

                return native with
                {
                    OwnedIntParams = [requested is 1 or -10 or -20 ? requested : 0],
                    VariableIds = native.VariableIds.IsEmpty ? [Plus.HabboHotel.Items.Wired.Variables.WiredVariableAbsent.WireId] : native.VariableIds,
                    Text = native.Text.Replace(' ', '_')
                };
            // These cards keep any number in a variable slot and any other text as "0".
            case "wf_act_give_var" or "wf_act_change_var_val" or "wf_act_remove_var":
                return native with { VariableIds = [.. native.VariableIds.Select(Plus.HabboHotel.Items.Wired.Variables.WiredVariableDescription.StoredId)] };
            // Variable Age keeps any number in its variable slot, and Variable Changed does too
            // while it watches one variable (not the picked variable boxes), and any other text becomes "0".
            case "wf_cnd_var_age_match" when native.VariableIds.Length > 0:
                return native with { VariableIds = [Plus.HabboHotel.Items.Wired.Variables.WiredVariableDescription.StoredId(native.VariableIds[0]), .. native.VariableIds.Skip(1)] };
            case "wf_trg_var_changed" when native.VariableIds.Length == 1 && (native.OwnedIntParams.Length != 6 || native.OwnedIntParams[5] == 0):
                return native with { VariableIds = [Plus.HabboHotel.Items.Wired.Variables.WiredVariableDescription.StoredId(native.VariableIds[0])] };
            default:
                return ChestStoredForm(name, native);
        }
    }

    // NamedTextInput a-zA-Z0-9_, at most 100 characters.
    private static bool TrackId(string id) => id.Length <= 100 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static WiredConfiguration? SendDataframe(WiredNativeEditorConfiguration n)
    {
        var p = n.OwnedIntParams;

        if (!Shape(n, 37, 13, 12, 12) || !WiredLinkDataframeCodec.FitsStorage(n.Text, p, n.VariableIds)
            || WiredLinkDataframeCodec.Decode(n.Text, p[0]) is not { } rows) {
            return null;
        }

        var targets = new int[12];

        for (var row = 0; row < 12; row++) {
            var (mode, target) = (p[1 + row * 3], p[3 + row * 3]);

            if (mode is < 0 or > 3 || !TryTarget(target, out targets[row])) {
                return null;
            }

            if (row < rows.Length && (!WiredLinkDataframeCodec.IsKey(rows[row].Key)
                || mode == WiredLinkDataframeCodec.CustomMessage && WiredLinkDataframeCodec.Utf8Length(rows[row].Text) > WiredLinkDataframeCodec.MaxMessageBytes)) {
                return null;
            }
        }

        // Runtime: count, then per row mode, literal and scalar target, then the endpoint source and each row's furni and user source.
        int[] ints = [p[0], .. Enumerable.Range(0, 12).SelectMany(row => new[] { p[1 + row * 3], p[2 + row * 3], targets[row] })];
        var furni = new List<(string, int)> { ("endpoints", n.FurniSourceTypes[0]) };
        var users = new List<(string, int)>();

        for (var row = 0; row < 12; row++) {
            furni.Add(("row" + row, n.FurniSourceTypes[1 + row]));
            users.Add(("row" + row, n.UserSourceTypes[row]));
        }

        return Roles(Draft(n, [.. ints], n.Text), [.. furni], [.. users]);
    }

    private static WiredConfiguration? ReceiveDataframe(WiredNativeEditorConfiguration n)
    {
        var p = n.OwnedIntParams;

        if (!Shape(n, 13, 1, 0, 12) || !WiredLinkDataframeCodec.FitsStorage(n.Text, p, n.VariableIds)
            || WiredLinkDataframeCodec.Decode(n.Text, p[0]) is not { } rows || p.Skip(1).Any(kind => kind is not (0 or 1))) {
            return null;
        }

        for (var row = 0; row < rows.Length; row++) {
            if (!WiredLinkDataframeCodec.IsKey(rows[row].Key) || p[1 + row] == WiredLinkDataframeCodec.PlaceholderDestination
                && !WiredLinkDataframeCodec.IsPlaceholder(rows[row].Text)) {
                return null;
            }
        }

        return Roles(Draft(n, p, n.Text), [("endpoints", n.FurniSourceTypes[0])], []);
    }
}

/// <summary>Web API keys are 32 random bytes in unpadded base64url: 43 characters.</summary>
public static class WiredWebApiKeyFormat
{
    public const int Length = 43;

    public static bool IsKey(string key) => key.Length == Length && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static string Create() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
