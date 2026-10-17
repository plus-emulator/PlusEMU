using System.Collections.Immutable;
using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// Reads a schema 1 row, saved as a runtime configuration before native editor records, as the native record its editor saves today.
/// A row whose fields are already today's runtime layout is kept as it is; a few old layouts are rewritten to today's (see Remap).
/// Either way the record must compile back to exactly that runtime, so nothing is guessed; any other row is rejected with a reason.
/// </summary>
internal static class WiredLegacyConfigurationUpgrade
{
    public static bool TryUpgrade(uint itemId, WiredBoxDescriptor descriptor, string json,
        out WiredNativeEditorConfiguration native, out string reason)
    {
        native = new();
        WiredConfiguration? legacy;

        try {
            legacy = JsonSerializer.Deserialize<WiredConfiguration>(json);
        }
        catch (JsonException error) {
            reason = "unreadable schema 1 JSON: " + error.Message;

            return false;
        }

        if (legacy == null || legacy.Version != 1 || legacy.IntParams.IsDefault || legacy.Text == null
            || legacy.SelectedItems.IsDefault || legacy.SecondarySelectedItems.IsDefault || legacy.VariableIds.IsDefault || legacy.Snapshots.IsDefault) {
            reason = "not a schema 1 runtime configuration";

            return false;
        }

        var name = descriptor.CanonicalName;

        if (!WiredNativeEditorProjection.Supports(name)) {
            reason = $"{name} has no native editor record";

            return false;
        }

        if (!TryRemap(name, legacy, out var current, out reason)) {
            return false;
        }

        native = Native(descriptor, current);

        if (!WiredNativeEditorProjection.TryCompile(itemId, descriptor, native, out var compiled, storedRow: true)) {
            reason = $"{name} IntParams [{string.Join(',', current.IntParams)}] do not form a valid native record";

            return false;
        }

        if (!WiredNativeEditorProjection.Matches(compiled, current)) {
            reason = $"{name} IntParams [{string.Join(',', current.IntParams)}] do not compile back to the same configuration";

            return false;
        }

        // A source today's editor does not offer for its slot (the walk trigger once took 0, the triggering furni) becomes the slot's
        // default, as the editor shows it when the card is opened.
        var metadata = WiredNativeEditorProjection.WithTriggerContext(WiredNativeEditorProjection.Metadata(name), WiredNativeEditorProjection.AnyTriggerContext);
        var offered = native with
        {
            FurniSourceTypes = Offered(native.FurniSourceTypes, metadata.FurniAllowed, metadata.FurniDefaults),
            UserSourceTypes = Offered(native.UserSourceTypes, metadata.UsersAllowed, metadata.UserDefaults)
        };

        if (offered != native) {
            if (!WiredNativeEditorProjection.TryCompile(itemId, descriptor, offered, out _, storedRow: true)) {
                reason = $"{name} sources [{string.Join(',', native.FurniSourceTypes)}|{string.Join(',', native.UserSourceTypes)}] cannot take today's defaults";

                return false;
            }

            native = offered;
        }

        reason = "";

        return true;
    }

    private static ImmutableArray<int> Offered(ImmutableArray<int> sources, ImmutableArray<ImmutableArray<int>> allowed, ImmutableArray<int> defaults) =>
        sources.Length != allowed.Length || defaults.Length != allowed.Length ? sources
            : [.. sources.Select((source, slot) => allowed[slot].Contains(source) ? source : defaults[slot])];

    /// <summary>The schema 1 fields in today's runtime layout: unchanged unless the box's old editor stored a different layout.</summary>
    internal static bool TryRemap(string name, WiredConfiguration legacy, out WiredConfiguration current, out string reason)
    {
        current = legacy;
        reason = "";
        var p = legacy.IntParams;

        switch (name) {
            case "wf_act_move_furni_as_group":
                if (!WiredMovementConfiguration.TryValidate(name, legacy, out var validated, out _)) {
                    reason = $"{name} IntParams [{string.Join(',', p)}] are not a move-as-group layout";

                    return false;
                }

                p = validated.IntParams;

                if (p.Length == 2) {
                    // The old direction editor moved the group one tile. The target editor says the same with the movers as their own
                    // target: both resolve the same picks in the same order, so the first mover steps one tile from itself.
                    var offset = WiredRoomOperations.Offset(p[0]);
                    current = validated with { IntParams = [0, offset.X, offset.Y, p[1], p[1], 0], SecondarySelectedItems = [], Text = "" };
                }
                else {
                    // The old target form kept its target picks in the text; the native record keeps them as secondary picks (source 101).
                    current = validated with { IntParams = p.SetItem(4, p[4] == WiredSources.Selected ? 101 : p[4]) };
                }

                current = current with
                {
                    Text = string.Join(';', current.SecondarySelectedItems),
                    FurniSources = ImmutableDictionary<string, int>.Empty.Add("movers", current.IntParams[3]).Add("targets", current.IntParams[4]),
                    UserSources = ImmutableDictionary<string, int>.Empty.Add("users", current.IntParams[5])
                };

                return true;
            case "wf_act_log" or "wf_act_neg_log":
                // The old editor also stored a user source; the log writes the message only and today's editor has no user source.
                if (p.Length != 2) {
                    break;
                }

                current = legacy with { IntParams = [p[0]], UserSources = ImmutableDictionary<string, int>.Empty };

                return true;
            case "wf_slc_furni_neighborhood" or "wf_slc_users_neighborhood":
                return TryRemapNeighborhood(name, legacy, out current, out reason);
        }

        return true;
    }

    /// <summary>
    /// The old neighborhood layout is [anchor, -, -, shift x, shift y, tile count, tile offsets as x/y pairs]: anchors 0-2 are the users of
    /// sources 0, 201 and 11, anchors 3-5 the floor furni of sources 0, 100 and 201. Today it is [0 furni / 1 users, shift x, shift y] and
    /// the 441-tile spiral bitmap of the editor.
    /// </summary>
    private static bool TryRemapNeighborhood(string name, WiredConfiguration legacy, out WiredConfiguration current, out string reason)
    {
        current = legacy;
        reason = "";
        var p = legacy.IntParams;

        if (p.Length == 17) {
            return true;
        }

        if (p.Length < 6 || p[0] is < 0 or > 5 || p[5] < 0 || p.Length != 6 + p[5] * 2) {
            reason = $"{name} IntParams [{string.Join(',', p)}] are not an old neighborhood layout";

            return false;
        }

        var spiral = WiredSelectorModule.NeighborhoodOffsets([0, 0, 0, .. Enumerable.Repeat(-1, 14)])
            .Select((offset, index) => (offset, index)).ToDictionary(entry => entry.offset, entry => entry.index);
        var fields = new int[17];
        fields[0] = p[0] <= 2 ? 1 : 0;
        fields[1] = p[3];
        fields[2] = p[4];

        for (var tile = 0; tile < p[5]; tile++) {
            if (!spiral.TryGetValue((p[6 + tile * 2], p[7 + tile * 2]), out var index)) {
                reason = $"{name} tile ({p[6 + tile * 2]}, {p[7 + tile * 2]}) is outside the 21x21 editor grid";

                return false;
            }

            fields[3 + index / 32] |= unchecked((int)(1u << (index % 32)));
        }

        var furni = p[0] switch { 3 => WiredSources.Trigger, 5 => WiredSources.Signal, _ => WiredSources.Selected };
        var users = p[0] switch { 1 => WiredSources.Signal, 2 => WiredSources.ClickedUser, _ => WiredSources.Trigger };
        current = legacy with
        {
            IntParams = [.. fields],
            FurniSources = ImmutableDictionary<string, int>.Empty.Add("anchor", furni),
            UserSources = ImmutableDictionary<string, int>.Empty.Add("anchor", users)
        };

        return true;
    }

    /// <summary>The native record for a runtime in today's layout: the editor-owned ints and source slots in the editor's order.</summary>
    private static WiredNativeEditorConfiguration Native(WiredBoxDescriptor descriptor, WiredConfiguration c)
    {
        var p = c.IntParams;
        var defaults = WiredNativeEditorProjection.DefaultNative(descriptor);
        ImmutableArray<int> owned = p;
        var furni = defaults.FurniSourceTypes;
        var users = defaults.UserSourceTypes;
        var quantifier = defaults.Quantifier;
        var secondary = c.SecondarySelectedItems;
        int FurniRole(string role, int fallback) => c.FurniSources.GetValueOrDefault(role, fallback);
        int UserRole(string role, int fallback) => c.UserSources.GetValueOrDefault(role, fallback);

        switch (descriptor.CanonicalName) {
            case "wf_trg_says_something" when p.Length == 3:
                owned = [p[2], p[0], p[1]];
                break;
            case "wf_trg_walks_on_furni" or "wf_trg_walks_off_furni" or "wf_trg_click_furni" or "wf_trg_click_tile" when p.Length == 1:
                owned = [];
                furni = [p[0]];
                break;
            case "wf_act_teleport_to" when p.Length == 3:
                owned = [p[0]];
                furni = [p[1]];
                users = [p[2]];
                break;
            case "wf_act_show_message" when p.Length == 4:
                owned = [p[1], p[2], p[3]];
                users = [p[0]];
                break;
            case "wf_cnd_trggrer_on_frn" or "wf_cnd_not_trggrer_on" when p.Length == 3:
                owned = [];
                furni = [p[0]];
                users = [p[1]];
                quantifier = p[2];
                break;
            case "wf_xtra_random" when p.Length == 2:
                owned = [p[1], p[0]];
                break;
            case "wf_act_move_furni_as_group" when p.Length == 6:
                owned = [p[0], p[1], p[2]];
                furni = [p[3], p[4]];
                users = [p[5]];
                break;
            case "wf_slc_furni_neighborhood" or "wf_slc_users_neighborhood":
                furni = [FurniRole("anchor", furni[0])];
                users = [UserRole("anchor", users[0])];
                break;
        }

        static WiredNativeItemReference Floor(uint id) => new(id, false);

        return defaults with
        {
            OwnedIntParams = owned,
            Text = c.Text,
            PrimaryItems = [.. c.SelectedItems.Distinct().Select(Floor)],
            SecondaryItems = [.. secondary.Distinct().Select(Floor)],
            FurniSourceTypes = furni,
            UserSourceTypes = users,
            VariableIds = c.VariableIds.IsEmpty ? defaults.VariableIds : c.VariableIds,
            Delay = descriptor.Category == WiredBoxCategory.Action ? c.Delay : null,
            Quantifier = quantifier,
            SavedState = new() { Snapshots = c.Snapshots }
        };
    }
}
