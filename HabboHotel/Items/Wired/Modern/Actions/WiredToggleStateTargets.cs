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
            else if (Untouched.Contains(item.Definition.ItemName) || item.StateHeldByFurniOnTop) {
                continue;
            }
            else if (item.Definition.Modes <= 1) {
                listState(item);
            }
            else {
                rest.Add(item);
            }
        }

        return [.. rest];
    }
}
