using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Conditions;

/// <summary>Positive item predicates. Negative boxes invert the outcome in their adapter.</summary>
public static class WiredItemConditions
{
    public static bool MatchesSnapshot(WiredConfiguration config, IEnumerable<Item> items)
    {
        var resolved = items.ToArray();
        var outcomes = resolved.Select(item =>
        {
            var snapshot = config.Snapshots.FirstOrDefault(entry => entry.ItemId == item.Id);

            return snapshot != null && WiredRoomOperations.MatchesSnapshot(item, snapshot,
                Param(config, 0) == 1, Param(config, 1) == 1, Param(config, 2) == 1, Param(config, 3) == 1);
        });

        // A picked snapshot remains a subject after pickup; losing it cannot make "all" pass.
        if (config.FurniSources.GetValueOrDefault("items", Param(config, 4, 100)) == 100) {
            var liveIds = resolved.Select(item => item.Id).ToHashSet();
            outcomes = outcomes.Concat(config.SelectedItems.Where(id => !liveIds.Contains(id)).Select(_ => false));
        }

        return WiredRoomOperations.Quantify(outcomes, Param(config, 5));
    }

    public static bool HasAvatars(WiredConfiguration config, IEnumerable<Item> items,
        IReadOnlyCollection<RoomUser> roomAvatars)
    {
        var subjects = items.ToArray();

        // Observed on AIR WIN63-202610131318-860371703: "Furnis have avatars" passes on an empty selection under both "all" and "any"
        // (unlike "Furni has furni on", which fails).
        return subjects.Length == 0 || WiredRoomOperations.Quantify(subjects.Select(item => roomAvatars.Any(avatar =>
            WiredRoomOperations.IsStandingOn(avatar, item))), Param(config, 0) == 1 ? 0 : 1);
    }

    public static bool TriggererOnItems(WiredConfiguration config, IReadOnlyCollection<Item> items,
        IEnumerable<RoomUser> avatars) => items.Count != 0 && WiredRoomOperations.Quantify(
        avatars.Select(avatar => items.Any(item => WiredRoomOperations.IsStandingOn(avatar, item))), Param(config, 2));

    public static bool HasStackedFurniture(WiredConfiguration config, IEnumerable<Item> items,
        Func<Item, bool> hasStackedFurniture) => WiredRoomOperations.Quantify(
        items.Select(hasStackedFurniture), Param(config, 0) == 1 ? 0 : 1);

    public static bool MatchesType(WiredConfiguration config, IEnumerable<Item> items,
        IEnumerable<Item> comparisonItems)
    {
        var ids = comparisonItems.Select(item => item.Id).ToHashSet();

        return ids.Count != 0 && WiredRoomOperations.Quantify(
            items.Select(item => ids.Contains(item.Id)), Param(config, 2));
    }

    public static bool MatchesAltitude(WiredConfiguration config, IEnumerable<Item> items)
    {
        if (!WiredRoomOperations.TryAltitude(config.Text, out var target)) {
            return false;
        }

        var targetHundredths = (long)Math.Round(target * 100, MidpointRounding.AwayFromZero);
        var comparison = Param(config, 0, 1);

        // Observed on AIR WIN63-202610201032-107964540: a stored comparison of 3 passes when the altitude is at most the value.
        return WiredRoomOperations.Quantify(items.Select(item =>
        {
            var altitude = (long)Math.Round(item.GetZ * 100, MidpointRounding.AwayFromZero);

            return comparison == 3 ? altitude <= targetHundredths : WiredRoomOperations.Compare(altitude, targetHundredths, comparison);
        }), Param(config, 2));
    }

    /// <summary>
    /// Legacy rule for rows that still pick furniture: each item needs a valid cardinal neighbour. The hotel editor has no furni
    /// source; the native condition checks the stack's own planned moves instead (WiredModernCondition, observed r92k r92h-vm-fail).
    /// </summary>
    public static bool ValidMoves(IEnumerable<Item> items, Func<Item, int, int, bool> canMove) =>
        WiredRoomOperations.Quantify(items.Select(item => Enumerable.Range(0, 4).Any(index =>
        {
            var offset = WiredRoomOperations.Offset(index * 2);

            return canMove(item, item.GetX + offset.X, item.GetY + offset.Y);
        })), 0);

    public static bool SelectionQuantity(WiredConfiguration config, int furniCount, int avatarCount)
    {
        var count = Param(config, 2) == 0 ? avatarCount : furniCount;
        var amount = Param(config, 1);

        // Observed on AIR WIN63-202610201032-107964540: a stored comparison of 3 passes when the count is at most the amount and a
        // stored comparison of 4 passes when it differs from the amount.
        return Param(config, 0, 1) switch
        {
            3 => count <= amount,
            4 => count != amount,
            var comparison => WiredRoomOperations.Compare(count, amount, comparison)
        };
    }

    private static int Param(WiredConfiguration config, int index, int fallback = 0) =>
        index < config.IntParams.Length ? config.IntParams[index] : fallback;
}
