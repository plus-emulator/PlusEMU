using Plus.HabboHotel.Items.Wired.Configuration;

namespace Plus.HabboHotel.Items.Wired;

/// <summary>
/// The states a wired box flips between when it acts, as Habbo's room 79874981 shows them (WIN63-202610121709):
/// most boxes hold 0 or 1 and flip each time; a selector rests at 0, or 3 in filter mode, and flips between its
/// resting state + 1 and + 2. The flips go out together every 500 ms.
/// </summary>
public static class WiredFlashStates
{
    public const int StateUpdateMilliseconds = 500;

    // Observed on AIR WIN63-202610151745-318700621 (and r90): a variable definition box shows the kind of its write, 6/7 for a
    // give, 2/3 for a change that raises the value, 4/5 for one that lowers it and 8/9 for a remove, alternating within the pair on
    // repeated writes of that kind (6 then 7).
    public const int DefinitionChanged = 2;
    public const int DefinitionDecreased = 4;
    public const int DefinitionGiven = 6;
    public const int DefinitionRemoved = 8;

    public const int DefinitionUnchanged = 0;

    public static string NextDefinition(string current, int kind) => kind == DefinitionUnchanged ? current == "0" ? "1" : "0" :
        (current == kind.ToString(System.Globalization.CultureInfo.InvariantCulture) ? kind + 1 : kind)
        .ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string Next(string current, IWiredItem? box)
    {
        if (RestingState(box) is { } resting) {
            return current == (resting + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                ? (resting + 2).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : (resting + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return current == "1" ? "0" : "1";
    }

    /// <summary>
    /// A selector's state while it is not flashing: 0 (orange), or 3 (green) with "Filter existing selection" ticked. A save switches
    /// to it. Observed on AIR WIN63-202610131318-860371703. Other boxes have none.
    /// </summary>
    public static int? RestingState(IWiredItem? box) => box is IWiredConfiguredItem { Descriptor.Category: WiredBoxCategory.Selector } selector
        ? selector.Configuration.Origin?.Native?.Filter == true ? 3 : 0
        : null;
}
