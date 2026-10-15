using System.Collections.Immutable;
using System.Text;
using Plus.HabboHotel.Items.Wired.Leaderboard;
using Plus.HabboHotel.Items.Wired.Links;
using Plus.HabboHotel.Items.Wired.Variables;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// The October boxes of the r88 dossier. Every grammar is the AIR editor's own save (WIN63-202610131318-860371703); the cited
/// line is the editor's readIntParamsFromForm / readStringParamFromForm. Merged selections pair a furni and a user source slot,
/// so a value-or-variable reference adds one of each after the box's own targets.
/// </summary>
internal static partial class WiredNativeAuxiliaryEditor
{
    // Properties, not fields: the spec table in the other part of this class is built before this part's static fields.
    private static ImmutableArray<int> TargetFurni => [0, 100, 200, 201];
    private static ImmutableArray<int> TargetUsers => [0, 200, 201];

    private static WiredConfiguration Roles(WiredConfiguration draft, (string Role, int Source)[] furni, (string Role, int Source)[] users) => draft with
    {
        FurniSources = furni.ToImmutableDictionary(pair => pair.Role, pair => pair.Source),
        UserSources = users.ToImmutableDictionary(pair => pair.Role, pair => pair.Source)
    };

    // A value-or-variable: 0 literal or 1 variable, the literal and the variable's AIR target. An unchosen variable is the inactive default.
    private static bool ValueOrVariable(int option, int target) => option is 0 or 1 && TryTarget(target, out _);

    private static IEnumerable<KeyValuePair<string, Spec>> October()
    {
        // ProgressAchievement.as:36 [progression (1 add, 0 set), option, number 0..2147483647, target]; :46 achievement name; :51 the variable.
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
        // class_3931.as:56 [effect id 0..10000, priority 0..2, type 0/1].
        yield return new("wf_act_give_effect", new(Sources([], [TargetUsers], [0, 0, 0]), n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 3, 0, 1, 0) && n.Text.Length == 0 && In(p[0], 0, 10000) && In(p[1], 0, 2) && p[2] is 0 or 1
                ? Roles(Draft(n, p), [], [("users", n.UserSourceTypes[0])]) : null;
        }));
        // class_4092.as:54 [height 0..8000 in hundredths, type (0 override, 1 restore)].
        yield return new("wf_act_override_height", new(Sources([TargetFurni], [], [0, 0]) with { FurniDefaults = [100] }, n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 2, 1, 0, 0) && n.Text.Length == 0 && In(p[0], 0, 8000) && p[1] is 0 or 1
                ? Roles(Draft(n, p), [("items", n.FurniSourceTypes[0])], []) : null;
        }));
        // ProgressRewardTrack.as:58 [add to existing, option, score 1..2147483647, target]; :68 track id TAB task id ([A-Za-z0-9_], 100 each).
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
        // class_4288.as:41 the track id (at most 100, no TAB); no ints.
        yield return new("wf_act_reset_reward_track", new(Sources([], [TargetUsers], []), n =>
            Shape(n, 0, 0, 1, 0) && (n.Text.Length == 0 || TrackId(n.Text))
                ? Roles(Draft(n, [], n.Text), [], [("users", n.UserSourceTypes[0])]) : null));
        // class_4285.as:79 [audience (1 everyone), visibility 0/100, click through, easing 0..4, target users]; merged [[0,1],[-1,0]]:
        // furni 0 with user 1 are the targets, user 0 the audience.
        yield return new("wf_act_set_visibility", new(Sources([TargetFurni], [TargetUsers, TargetUsers], [0, 100, 0, 0, 0]) with
        {
            FurniDefaults = [100]
        }, n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 5, 1, 2, 0) && n.Text.Length == 0 && p[0] is 0 or 1 && p[1] is 0 or 100 && p[2] is 0 or 1 && In(p[3], 0, 4) && p[4] is 0 or 1
                ? Roles(Draft(n, p), [("items", n.FurniSourceTypes[0])], [("audience", n.UserSourceTypes[0]), ("targets", n.UserSourceTypes[1])]) : null;
        }));
        // class_4214.as:112 [audience, type 0..5, strength, colour, animation 0/1, easing 0..4, target users]; strength 0..100 (Brighten
        // stores 0 or 38), colour 24-bit.
        yield return new("wf_act_set_highlight", new(Sources([TargetFurni], [TargetUsers, TargetUsers], [0, 0, 100, 16777215, 0, 0, 0]) with
        {
            FurniDefaults = [100]
        }, n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 7, 1, 2, 0) && n.Text.Length == 0 && p[0] is 0 or 1 && In(p[1], 0, 5) && In(p[2], 0, 100)
                && (p[1] != 4 || p[2] is 0 or 38) && In(p[3], 0, 0xFFFFFF) && p[4] is 0 or 1 && In(p[5], 0, 4) && p[6] is 0 or 1
                ? Roles(Draft(n, p), [("items", n.FurniSourceTypes[0])], [("audience", n.UserSourceTypes[0]), ("targets", n.UserSourceTypes[1])]) : null;
        }));
        // SendLinkMessage.as:95 [source (0 the trigger's text, 1 custom)]; :100 the message, at most 1000 UTF-8 bytes (:105). Furni 0 picks the endpoints.
        yield return new("wf_act_send_message", new(Sources([TargetFurni], [], [1]) with { FurniDefaults = [100] }, n =>
            Shape(n, 1, 1, 0, 0) && n.OwnedIntParams[0] is 0 or 1 && WiredLinkDataframeCodec.Utf8Length(n.Text) <= WiredLinkDataframeCodec.MaxLinkMessageBytes
                ? Roles(Draft(n, n.OwnedIntParams, n.Text), [("endpoints", n.FurniSourceTypes[0])], []) : null));
        // LinkDataframeSendPreset.as:121-142 [count, 12 x (mode, literal, target)] = 37 ints, JSON [key, text] rows, 12 variable ids.
        // Merged rows pair furni i+1 with user i (SendLinkDataframe.as mergedSelections); furni 0 picks the endpoints.
        yield return new("wf_act_send_dataframe", new(Sources([TargetFurni, .. Enumerable.Repeat(TargetFurni, 12)],
            [.. Enumerable.Repeat(TargetUsers, 12)], [0, .. Enumerable.Range(0, 12).SelectMany(_ => new[] { 0, 0, -10 })], 12) with
        {
            FurniDefaults = [100, .. Enumerable.Repeat(0, 12)]
        }, SendDataframe));
        // RoomInitialized.as: no fields.
        yield return new("wf_trg_room_initializes", new(Meta(0, 0, []), n => Shape(n, 0, 0, 0, 0) && n.Text.Length == 0 ? Draft(n, []) : null));
        // ReceiveLinkMessage.as: no fields; furni 0 picks the endpoints it listens on.
        yield return new("wf_trg_receive_message", new(Sources([TargetFurni], [], []) with { FurniDefaults = [100] }, n =>
            Shape(n, 0, 1, 0, 0) && n.Text.Length == 0 ? Roles(Draft(n, []), [("endpoints", n.FurniSourceTypes[0])], []) : null));
        // LinkDataframeReceivePreset.as:91-124 [count, 12 x kind] = 13 ints, JSON [key, placeholder] rows, 12 variable ids.
        yield return new("wf_trg_receive_dataframe", new(Sources([TargetFurni], [], [0, .. Enumerable.Repeat(0, 12)], 12) with
        {
            FurniDefaults = [100]
        }, ReceiveDataframe));
        // LevelMatches.as:49 [level 1..30, comparison 0 lower, 1 equal, 2 higher]. LevelMatches overrides only code, inputMode, buildInputs,
        // onEditStart and readIntParamsFromForm, exactly like the badge condition (conditions/class_4263.as): its user source picker and
        // user quantifier come from the server's footer, so it advertises the footer of the other user conditions.
        yield return new("wf_cnd_user_level", new(Sources([], [[0, 10, 11, 200, 201]], [1, 1]) with { QuantifierType = 2 }, n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 2, 0, 1, 0) && n.Text.Length == 0 && In(p[0], 1, 30) && In(p[1], 0, 2)
                ? Roles(Draft(n, [p[0], p[1], n.Quantifier ?? 0]), [], [("users", n.UserSourceTypes[0])]) : null;
        }));

        // class_3963.as (base of the share and import add-ons): [text connectors, subvariable creators, variable FX], each 0/1.
        foreach (var name in new[] { "wf_xtra_var_share", "wf_xtra_var_import" }) {
            yield return new(name, new(Meta(0, 0, [1, 1, 1]), n => Shape(n, 3, 0, 0, 0) && n.Text.Length == 0
                && n.OwnedIntParams.All(flag => flag is 0 or 1) ? Draft(n, n.OwnedIntParams) : null));
        }

        // VariableLeaderboardConfigCodec.as:182-222: 19 ints, 12 variable ids, 17 TAB-joined fields (empty while unpublished).
        yield return new("wf_xtra_var_leaderboard", new(Meta(0, 0, WiredLeaderboardConfig.DefaultInts) with
        {
            VariableDefaults = [.. Enumerable.Repeat(WiredLeaderboardConfig.NoVariable, WiredLeaderboardConfig.VariableCount)]
        }, n => n.FurniSourceTypes.Length == 0 && n.UserSourceTypes.Length == 0
            && WiredLeaderboardConfig.Decode(n.OwnedIntParams, n.VariableIds, n.Text) is not null ? Draft(n, n.OwnedIntParams, n.Text) : null));
        // GlobalPlaceholderAddon.as:155 [mode (0 custom text, 1 room), 0, room index]; :160 placeholder name TAB text.
        yield return new("wf_xtra_global_placeholder", new(Meta(0, 0, [0, 0, 0]), n =>
        {
            var p = n.OwnedIntParams;
            var parts = n.Text.Split('\t', 2);

            return Shape(n, 3, 0, 0, 0) && p[0] is 0 or 1 && p[1] == 0 && p[2] >= 0 && (p[0] == 1 || p[2] == 0)
                && (n.Text.Length == 0 || WiredLinkDataframeCodec.IsPlaceholder(parts[0]) && (parts.Length == 1 || parts[1].Length <= 1000))
                ? Draft(n, p, n.Text) : null;
        }));
        // class_4012.as:27 one achievement per line, at most 2000 characters.
        yield return new("wf_xtra_achievement_enabler", new(Meta(0, 0, []), n => Shape(n, 0, 0, 0, 0) && n.Text.Length <= 2000
            ? Draft(n, [], n.Text) : null));
        // VariablesWebApiAddon.as:108 read key TAB write key; :113 [bulk delete], only with a write key. Keys are the server's own.
        yield return new("wf_xtra_var_web_api", new(Meta(0, 0, [0]), n =>
        {
            var parts = n.Text.Split('\t');

            return Shape(n, 1, 0, 0, 0) && n.OwnedIntParams[0] is 0 or 1 && (n.Text.Length == 0 || parts.Length == 2
                && parts.All(key => key.Length == 0 || WiredWebApiKeyFormat.IsKey(key)))
                && (n.OwnedIntParams[0] == 0 || parts.Length == 2 && parts[1].Length > 0) ? Draft(n, n.OwnedIntParams, n.Text) : null;
        }));
        // class_4123.as:43 [message, dataframe]; the speaker always picks furni (forceFurniSelection).
        yield return new("wf_xtra_link_speaker", new(Sources([TargetFurni], [], [1, 1]) with { FurniDefaults = [100] }, n =>
            Shape(n, 2, 1, 0, 0) && n.Text.Length == 0 && n.OwnedIntParams.All(flag => flag is 0 or 1)
                ? Roles(Draft(n, n.OwnedIntParams), [("speakers", n.FurniSourceTypes[0])], []) : null));
        // DailyTaskVariable.as: variable name TAB task text (at most 100); no ints or variables.
        yield return new("wf_var_dailytask", new(Meta(0, 0, []), n =>
        {
            var parts = n.Text.Split('\t');

            return Shape(n, 0, 0, 0, 0) && parts.Length <= 2 && (parts.Length == 1 || parts[1].Length <= 100) ? Draft(n, [], n.Text) : null;
        }));
        // PointerVariable.as: [source type (0 furni, 1 user, -10 global, -20 context)], the accessor (a context variable) and the name.
        yield return new("wf_var_pointer", new(Meta(0, 0, [0], 1), n => Shape(n, 1, 0, 0, 1) && n.OwnedIntParams[0] is 0 or 1 or -10 or -20
            ? Draft(n, n.OwnedIntParams, n.Text) : null));
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
