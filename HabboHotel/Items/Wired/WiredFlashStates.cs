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

    public static string Next(string current, IWiredItem? box)
    {
        if (box is IWiredConfiguredItem { Descriptor.Category: WiredBoxCategory.Selector } selector) {
            var resting = selector.Configuration.Origin?.Native?.Filter == true ? 3 : 0;

            return current == (resting + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                ? (resting + 2).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : (resting + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return current == "1" ? "0" : "1";
    }
}
