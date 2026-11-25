using System.Globalization;
using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;

namespace Plus.HabboHotel.Rooms.AI;

/// <summary>Water bowls and the horse trough. Their furni state is the water left, from 5 (full) to 0.</summary>
public static class PetWater
{
    public const int Full = 5;

    private static readonly TimeSpan RefillAfter = TimeSpan.FromMinutes(5);
    private static readonly ConditionalWeakTable<Item, StrongBox<DateTimeOffset>> LastDrinks = new();

    public static bool Is(ItemDefinition definition) =>
        definition.ItemName.StartsWith("waterbowl", StringComparison.Ordinal) || definition.ItemName.StartsWith("water_bowl", StringComparison.Ordinal)
        || definition.ItemName == "horse_trough";

    public static bool Suits(ItemDefinition definition, int petType) => throw new NotImplementedException();

    /// <summary>One drink at <paramref name="now"/>: the water that nobody drank for a while is full again first.</summary>
    public static void Drink(Item water, DateTimeOffset now)
    {
        var last = LastDrinks.GetOrCreateValue(water);
        var left = last.Value == default || now - last.Value >= RefillAfter || !int.TryParse(water.LegacyDataString, out var stored)
            ? Full
            : Math.Clamp(stored, 0, Full);
        last.Value = now;

        if (water.ExtraData is not LegacyDataFormat) {
            water.ExtraData = new LegacyDataFormat();
        }

        water.LegacyDataString = Math.Max(0, left - 1).ToString(CultureInfo.InvariantCulture);
    }
}
