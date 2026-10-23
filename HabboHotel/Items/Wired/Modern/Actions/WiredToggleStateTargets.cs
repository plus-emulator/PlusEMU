namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>
/// Which furni Toggle State changes. Observed on AIR WIN63-202610201032-107964540: it changes colour tiles and other state furni and
/// every Wired box in its list, but never a floor switch, an antenna, a pressure plate, an arrow plate, a box, a pyramid or the two
/// plain game tiles, whatever number of states they have. A one-state furni, such as the stairs, is listed with the state it has. A furni
/// whose height depends on its state is skipped while furni stand on it.
/// </summary>
internal static class WiredToggleStateTargets
{
    private static readonly HashSet<string> Untouched = new(StringComparer.OrdinalIgnoreCase)
    {
        "wf_floor_switch1", "wf_floor_switch2", "wf_antenna1", "wf_antenna2", "wf_pressureplate", "wf_arrowplate",
        "wf_box", "wf_pyramid", "wf_tile1", "wf_tile2"
    };

    /// <summary>
    /// Flashes each Wired box, which is the only change a box shows (a box that flashes in the same half second still shows one change),
    /// lists each one-state furni unchanged, and returns the furni that change state the usual way.
    /// </summary>
    internal static Item[] Resolve(IEnumerable<Item> targets, Action<Item> flashBox, Action<Item> listState, out bool flashed)
    {
        var rest = new List<Item>();
        flashed = false;

        foreach (var item in targets) {
            if (item.IsWired) {
                flashBox(item);
                flashed = true;
            }
            else if (Untouched.Contains(item.Definition.ItemName)) {
                continue;
            }
            else if (item.Definition.Modes <= 1) {
                listState(item);
            }
            else if (item.StateHeldByFurniOnTop) {
                continue;
            }
            else {
                rest.Add(item);
            }
        }

        return [.. rest];
    }

    private static readonly HashSet<string> RandomUntouched = new(StringComparer.OrdinalIgnoreCase)
    {
        "wf_tile1", "wf_tile2", "wf_pyramid", "wf_box", "wf_arrowplate", "wf_arrowplate_8dir", "wf_pressureplate", "wf_ringplate",
        "wf_antenna1", "wf_antenna2"
    };

    private static bool LeftAloneByRandom(string name) => RandomUntouched.Contains(name)
        || name.StartsWith("wf_blob", StringComparison.OrdinalIgnoreCase) || name.StartsWith("wf_button", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The furni Toggle to Random changes. Observed on AIR WIN63-202610201032-107964540 (3 of 3 runs, 24 firings each): it changes colour
    /// tiles, floor switches, number tiles, glow balls, blocks, knobs, toggles, glass doors, sliders, team tiles, team meters, mazes and
    /// fire gates, but no Wired box, the two plain game tiles, the pyramid, the box, the arrow and pressure plates, the antennas, the
    /// blobs or the buttons.
    /// </summary>
    internal static Item[] ResolveRandom(IEnumerable<Item> targets) => [.. targets.Where(item => !item.IsWired && !LeftAloneByRandom(item.Definition.ItemName))];

    /// <summary>A plain state furni whose new state only goes out in the tick's ObjectsDataUpdate (see WiredComponent.PublishState).</summary>
    internal static bool WritesOnlyBatchData(Item item) => item.IsFloorItem && item.ExtraData is DataFormat.LegacyDataFormat
        && item.Definition.AdjustableHeights.Count <= 1 && item.Definition.InteractionType == InteractionType.None;
}
