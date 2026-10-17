using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Modern.Actions;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>Reads a schema 1 row, saved before native editor records, as the native record its editor would save today.</summary>
internal static class WiredLegacyConfigurationUpgrade
{
    public static bool TryUpgrade(WiredBoxDescriptor descriptor, string json, out WiredNativeEditorConfiguration native)
    {
        native = new();
        WiredConfiguration? legacy;

        try {
            legacy = JsonSerializer.Deserialize<WiredConfiguration>(json);
        }
        catch (JsonException) {
            return false;
        }

        if (legacy == null || legacy.Version != 1) {
            return false;
        }

        return descriptor.CanonicalName switch
        {
            "wf_act_move_furni_as_group" => TryMoveFurniAsGroup(descriptor, legacy, out native),
            _ => false
        };
    }

    private static bool TryMoveFurniAsGroup(WiredBoxDescriptor descriptor, WiredConfiguration legacy, out WiredNativeEditorConfiguration native)
    {
        native = new();

        if (!WiredMovementConfiguration.TryValidate(descriptor.CanonicalName, legacy, out var validated, out _)) {
            return false;
        }

        var p = validated.IntParams;
        var movers = p.Length == 2 ? p[1] : p[3];
        int[] owned;
        int[] furni;
        int users;

        if (p.Length == 2) {
            // The old direction editor moved the group one tile. The target editor says the same with the movers as their own target:
            // the first mover lands one step from itself and the rest keep their places around it.
            var offset = WiredRoomOperations.Offset(p[0]);
            owned = [0, offset.X, offset.Y];
            furni = [movers, movers];
            users = 0;
        }
        else {
            // The old target form kept its target picks in the text; the native record keeps them as the secondary picks (source 101).
            owned = [p[0], p[1], p[2]];
            furni = [movers, p[4] == WiredSources.Selected ? 101 : p[4]];
            users = p[5];
        }

        native = new()
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [.. owned],
            PrimaryItems = [.. validated.SelectedItems.Select(id => new WiredNativeItemReference(id, false))],
            SecondaryItems = p.Length == 2 ? [] : [.. validated.SecondarySelectedItems.Select(id => new WiredNativeItemReference(id, false))],
            FurniSourceTypes = [.. furni],
            UserSourceTypes = [users],
            Delay = validated.Delay
        };

        return true;
    }
}
