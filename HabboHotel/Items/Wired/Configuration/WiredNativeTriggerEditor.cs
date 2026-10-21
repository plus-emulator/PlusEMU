using System.Collections.Immutable;
using System.Globalization;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;

namespace Plus.HabboHotel.Items.Wired.Configuration;

internal static class WiredNativeTriggerEditor
{
    internal static bool Supports(string name) => WiredTriggerConfiguration.Events.ContainsKey(name)
        && name is not ("wf_trg_says_something" or "wf_trg_walks_on_furni" or "wf_trg_periodically");

    internal static WiredNativeEditorMetadata Metadata(string name) => name switch
    {
        "wf_trg_walks_off_furni" => new([[100, 200]], [], [100], [], [], false),
        // Observed on hhus (AIR WIN63-202610131318-860371703): click, use and state-change triggers allow wall picks; wall clicks and state writes fire them.
        // The click trigger offers picks (100) and the selector (200) only; with 200 a click on furni in the stack's selector pool fires it.
        "wf_trg_click_furni" => new([[100, 200]], [], [100], [], [], true),
        "wf_trg_click_tile" => new([[100, 200]], [], [100], [], [], false),
        "wf_trg_recv_signal" => new([[100, 200, 201]], [], [100], [], [], false),
        // The use trigger has no state radio; the state-change trigger does.
        "wf_trg_state_changed" => new([[100, 200]], [], [100], [], [], true),
        "wf_trg_stuff_state" => new([[100, 200]], [], [100], [], [1], true),
        "wf_trg_clock_counter" => new([[100, 200]], [], [100], [], [0, 0, 0], false),
        "wf_trg_bot_reached_stf" => new([[100, 200]], [[100, 200]], [100], [100], [], false),
        "wf_trg_bot_reached_avtr" => new([], [[100, 200]], [], [100], [], false),
        "wf_trg_click_user" => new([], [], [], [], [1, 0], false),
        "wf_trg_score_achieved" => new([], [], [], [], [100, 0], false),
        "wf_trg_user_performs_action" => new([], [], [], [], [0], false),
        "wf_trg_period_short" => new([], [], [], [], [4], false),
        "wf_trg_period_long" => new([], [], [], [], [10], false),
        _ when WiredTriggerConfiguration.IsTimed(name) => new([], [], [], [], [1], false),
        _ when Supports(name) => new([], [], [], [], [], false),
        _ => throw new InvalidDataException("Unknown native trigger.")
    };

    internal static bool TryCompile(string name, WiredNativeEditorConfiguration native, out WiredConfiguration runtime)
    {
        runtime = new();

        if (!Supports(name) || native.Delay != null || native.Quantifier != null || !native.VariableIds.IsEmpty) {
            return false;
        }

        var p = native.OwnedIntParams;
        var f = native.FurniSourceTypes;
        var u = native.UserSourceTypes;
        ImmutableArray<int> parameters;

        switch (name) {
            case "wf_trg_enter_room":
            case "wf_trg_leave_room":
            case "wf_trg_game_starts":
            case "wf_trg_game_ends":
            case "wf_trg_collision":
                if (!p.IsEmpty) {
                    return false;
                }

                parameters = [];
                break;
            case "wf_trg_walks_off_furni":
            case "wf_trg_click_furni":
            case "wf_trg_click_tile":
                if (!p.IsEmpty || f.Length != 1) {
                    return false;
                }

                parameters = [f[0]];
                break;
            case "wf_trg_state_changed":
            case "wf_trg_recv_signal":
                if (!p.IsEmpty || f.Length != 1) {
                    return false;
                }

                parameters = [0, f[0]];
                break;
            case "wf_trg_stuff_state":
                if (p.Length != 1 || f.Length != 1) {
                    return false;
                }

                parameters = [p[0], f[0]];
                break;
            case "wf_trg_bot_reached_stf":
                if (!p.IsEmpty || f.Length != 1 || u.Length != 1) {
                    return false;
                }

                parameters = [f[0], u[0]];
                break;
            case "wf_trg_bot_reached_avtr":
                if (!p.IsEmpty || u.Length != 1) {
                    return false;
                }

                parameters = [u[0]];
                break;
            case "wf_trg_clock_counter":
                if (p.Length != 3 || f.Length != 1 || p[0] is < 0 or > 59 || p[2] is < 0 or > 1) {
                    return false;
                }

                parameters = [p[1], p[0] * 2 + p[2], f[0]];
                break;
            case "wf_trg_click_user":
            case "wf_trg_score_achieved":
                if (p.Length != 2) {
                    return false;
                }

                parameters = p;
                break;
            case "wf_trg_user_performs_action":
                if (p.Length != 1 || !TryStoredAvatarAction(p[0], native.Text, out parameters)) {
                    return false;
                }

                break;
            default:
                if (!WiredTriggerConfiguration.IsTimed(name) || p.Length != 1) {
                    return false;
                }

                parameters = p;
                break;
        }

        if (name is not ("wf_trg_enter_room" or "wf_trg_leave_room" or "wf_trg_bot_reached_avtr"
            or "wf_trg_bot_reached_stf" or "wf_trg_user_performs_action") && native.Text.Length != 0) {
            return false;
        }

        var proposed = new WiredConfiguration
        {
            IntParams = parameters,
            Text = native.Text,
            SelectedItems = native.PrimaryItems.Select(item => item.ItemId).ToImmutableArray(),
            SecondarySelectedItems = native.SecondaryItems.Select(item => item.ItemId).ToImmutableArray(),
            Snapshots = native.SavedState.Snapshots
        };

        return WiredTriggerConfiguration.TryValidate(name, proposed, out runtime, out _);
    }

    /// <summary>
    /// The action of a trigger or condition save. Observed on AIR WIN63-202610201032-107964540: the hotel stores action 9, which the client
    /// does not offer, and any filter text for every action. No avatar action matches 9, and a filter this server cannot read leaves the
    /// action unfiltered.
    /// </summary>
    internal static bool TryStoredAvatarAction(int code, string text, out ImmutableArray<int> parameters)
    {
        if (code == 9) {
            parameters = [WiredTriggerConfiguration.UnofferedAction, 0, 0, 0, 1];

            return true;
        }

        return TryAvatarAction(code, text, out parameters) || TryAvatarAction(code, "", out parameters);
    }

    internal static bool TryAvatarAction(int code, string text, out ImmutableArray<int> parameters)
    {
        parameters = [];
        var action = code switch
        {
            0 => 1,
            1 => 2,
            2 => 3,
            3 => 12,
            >= 4 and <= 8 => code,
            10 => 9,
            11 => 10,
            67 => 11,
            _ => -1
        };

        if (action < 0 || text.Length != 0 && code is not (10 or 11)) {
            return false;
        }

        var value = code == 11 ? 1 : 0;

        if (text.Length != 0) {
            var raw = code == 11 && text.StartsWith("dance ", StringComparison.Ordinal) ? text[6..] : text;

            if (code == 11 && !text.StartsWith("dance ", StringComparison.Ordinal)
                || !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value)
                || value < 0 || value > (code == 11 ? 4 : 17) || code == 11 && value == 0) {
                return false;
            }
        }

        parameters = [action, code == 10 && text.Length != 0 ? 1 : 0, code == 10 ? value : 0,
            code == 11 && text.Length != 0 ? 1 : 0, code == 11 ? value : 1];

        return true;
    }
}
