using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;

namespace Plus.HabboHotel.Items.Wired.Modern.Triggers;

public static class WiredTriggerConfiguration
{
    public static readonly IReadOnlyDictionary<string, WiredEventKind> Events = new Dictionary<string, WiredEventKind>(StringComparer.OrdinalIgnoreCase)
    {
        ["wf_trg_enter_room"] = WiredEventKind.Enter,
        ["wf_trg_leave_room"] = WiredEventKind.Leave,
        ["wf_trg_says_something"] = WiredEventKind.Speech,
        ["wf_trg_walks_on_furni"] = WiredEventKind.WalkOn,
        ["wf_trg_walks_off_furni"] = WiredEventKind.WalkOff,
        ["wf_trg_stuff_state"] = WiredEventKind.StateChanged,
        ["wf_trg_state_changed"] = WiredEventKind.Use,
        ["wf_trg_collision"] = WiredEventKind.Collision,
        ["wf_trg_game_starts"] = WiredEventKind.GameStart,
        ["wf_trg_game_ends"] = WiredEventKind.GameEnd,
        ["wf_trg_score_achieved"] = WiredEventKind.Score,
        ["wf_trg_user_performs_action"] = WiredEventKind.AvatarAction,
        ["wf_trg_bot_reached_avtr"] = WiredEventKind.BotReachedUser,
        ["wf_trg_bot_reached_stf"] = WiredEventKind.BotReachedFurni,
        ["wf_trg_click_furni"] = WiredEventKind.ClickFurni,
        ["wf_trg_click_tile"] = WiredEventKind.ClickTile,
        ["wf_trg_click_user"] = WiredEventKind.ClickUser,
        ["wf_trg_clock_counter"] = WiredEventKind.Counter,
        ["wf_trg_recv_signal"] = WiredEventKind.Signal,
        ["wf_trg_at_given_time"] = WiredEventKind.Elapsed,
        ["wf_trg_at_time_long"] = WiredEventKind.Elapsed,
        ["wf_trg_periodically"] = WiredEventKind.Periodic,
        ["wf_trg_period_short"] = WiredEventKind.Periodic,
        ["wf_trg_period_long"] = WiredEventKind.Periodic,
        ["wf_trg_room_initializes"] = WiredEventKind.RoomInitialized,
        ["wf_trg_receive_message"] = WiredEventKind.LinkMessage,
        ["wf_trg_receive_dataframe"] = WiredEventKind.LinkDataframe
    };
    /// <summary>The saved action 9 that no avatar action matches: the hotel stores it, but the client does not offer it.</summary>
    public const int UnofferedAction = 13;

    public static bool IsTimed(string name) => name is "wf_trg_at_given_time" or "wf_trg_at_time_long"
        or "wf_trg_periodically" or "wf_trg_period_short" or "wf_trg_period_long";
    // The most units each timed editor offers (Volt sliders); the least is 1. At Given Time and Periodically Long keep what was sent instead (1201 pulses and 121 units are stored as sent).
    public static int MaxTimedUnits(string name) => name switch
    {
        "wf_trg_period_short" => 10,
        "wf_trg_at_time_long" => 1200,
        _ => 120
    };

    public static WiredConfiguration Defaults(string name)
    {
        ImmutableArray<int> parameters = name switch
        {
            "wf_trg_says_something" => [0, 0, 0],
            "wf_trg_walks_on_furni" or "wf_trg_walks_off_furni" or "wf_trg_click_furni" or "wf_trg_click_tile" => [100],
            "wf_trg_stuff_state" or "wf_trg_state_changed" => [0, 100],
            "wf_trg_bot_reached_avtr" => [100],
            "wf_trg_bot_reached_stf" => [100, 100],
            "wf_trg_click_user" => [0, 0],
            "wf_trg_clock_counter" => [0, 0, 100],
            "wf_trg_recv_signal" => [0, 100],
            "wf_trg_score_achieved" => [1, 0],
            "wf_trg_user_performs_action" => [1, 0, 0, 0, 1],
            _ when IsTimed(name) => [1],
            _ when Events.ContainsKey(name) => [],
            _ => throw new ArgumentException("Unknown trigger.", nameof(name))
        };

        if (!TryValidate(name, new() { IntParams = parameters }, out var config, out var error)) {
            throw new InvalidOperationException(error);
        }

        return config;
    }

    public static bool TryValidate(string name, WiredConfiguration proposed, out WiredConfiguration validated, out string error)
    {
        validated = proposed;
        error = "Invalid trigger configuration.";

        if (!Events.ContainsKey(name) || !WiredLegacyProtocol.IsWithinLimits(proposed)) {
            return false;
        }

        var p = proposed.IntParams;
        bool F(int i) => p[i] is 0 or 100 or 200 or 201;
        bool B(int i) => p[i] is 0 or 100 or 200 or 201;
        bool Bit(int i) => p[i] is 0 or 1;
        var furni = ImmutableDictionary.CreateBuilder<string, int>();
        var users = ImmutableDictionary.CreateBuilder<string, int>();

        switch (name.ToLowerInvariant()) {
            case "wf_trg_enter_room":
            case "wf_trg_leave_room":
                if (p.Length != 0 || proposed.Text.Length > 64) {
                    return false;
                }

                break;
            case "wf_trg_says_something":
                if (p.Length != 3 || p[0] is < 0 or > 2 || !Bit(1) || !Bit(2)) {
                    return false;
                }

                break;
            case "wf_trg_walks_on_furni":
            case "wf_trg_walks_off_furni":
            case "wf_trg_click_furni":
            case "wf_trg_click_tile":
                if (p.Length != 1 || !F(0)) {
                    return false;
                }

                furni["items"] = p[0];
                break;
            case "wf_trg_stuff_state":
            case "wf_trg_state_changed":
                if (p.Length != 2 || !Bit(0) || !F(1)) {
                    return false;
                }

                furni["items"] = p[1];
                break;
            case "wf_trg_bot_reached_avtr":
                if (p.Length != 1 || !B(0)) {
                    return false;
                }

                users["bots"] = p[0];
                break;
            case "wf_trg_bot_reached_stf":
                // A bot name of any length is stored.
                if (p.Length != 2 || !F(0) || !B(1)) {
                    return false;
                }

                furni["items"] = p[0];
                users["bots"] = p[1];
                break;
            case "wf_trg_click_user":
                if (p.Length != 2 || !Bit(0) || !Bit(1)) {
                    return false;
                }

                break;
            case "wf_trg_clock_counter":
                if (p.Length != 3 || p[0] is < 0 or > 99 || p[1] is < 0 or > 119 || !F(2)) {
                    return false;
                }

                furni["items"] = p[2];
                break;
            case "wf_trg_recv_signal":
                if (p.Length != 2 || p[0] < 0 || !F(1)) {
                    return false;
                }

                furni["items"] = p[1];
                break;
            case "wf_trg_score_achieved":
                // The points slider runs 1 to 1000 and the team radio 0 (any) to 4.
                if (p.Length != 2 || p[0] is < 1 or > 1000 || p[1] is < 0 or > 4) {
                    return false;
                }

                break;
            case "wf_trg_user_performs_action":
                if (p.Length != 5 || p[0] is < 1 or > UnofferedAction || !Bit(1) || p[2] is < 0 or > 17 || !Bit(3) || p[4] is < 0 or > 4) {
                    return false;
                }

                break;
            case "wf_trg_at_given_time":
                // 0 pulses is kept and fires on the first room tick after a timer reset.
                if (p.Length != 1 || p[0] < 0) {
                    return false;
                }

                break;
            case "wf_trg_period_long":
                if (p.Length != 1 || p[0] < 1) {
                    return false;
                }

                break;
            case "wf_trg_at_time_long":
            case "wf_trg_periodically":
            case "wf_trg_period_short":
                if (p.Length != 1 || p[0] < 1 || p[0] > MaxTimedUnits(name)) {
                    return false;
                }

                break;
            case "wf_trg_room_initializes":
            case "wf_trg_receive_message":
            case "wf_trg_receive_dataframe":
            // The October triggers exist only as native editor records (WiredNativeAuxiliaryEditor.October); this is their empty default.
            case "wf_trg_game_starts":
            case "wf_trg_game_ends":
            case "wf_trg_collision":
                if (p.Length != 0) {
                    return false;
                }

                break;
            default:
                return false;
        }

        validated = proposed with { FurniSources = furni.ToImmutable(), UserSources = users.ToImmutable() };
        error = "";

        return true;
    }
}
