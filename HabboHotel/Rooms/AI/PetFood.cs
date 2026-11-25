using System.Globalization;
using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms.AI;

/// <summary>
/// Pet food furni: a classname starting with "petfood" or the "pet_food" furniline. Its customparams read
/// "energy,experience,happiness" and an optional list of the pet types that eat it, as in "375,15,0 8,26,30".
/// Pet food without customparams, such as the carrot, feeds every pet type with 250 energy and 50 experience.
/// </summary>
public sealed record PetFood(int Energy, int Experience, int Happiness, IReadOnlyList<int> PetTypes)
{
    /// <summary>A food lasts five bites; its furni state counts the bites taken.</summary>
    public const int Bites = 5;

    private static readonly PetFood WithoutValues = new(250, 50, 0, []);

    public int BiteEnergy => (Energy + 5) / 10;

    public int BiteExperience => Experience / Bites;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ItemDefinition, PetFood> Foods = new();

    public static PetFood? Of(ItemDefinition definition) => Foods.GetValue(definition, static item => Parse(item)!);

    private static PetFood? Parse(ItemDefinition definition)
    {
        if (!definition.ItemName.StartsWith("petfood", StringComparison.Ordinal) && definition.FurniLine != "pet_food") {
            return null;
        }

        var parts = definition.CustomParams.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0) {
            return WithoutValues;
        }

        var values = parts[0].Split(',');

        if (values.Length != 3 || !TryParseAll(values, out var numbers)) {
            return null;
        }

        var types = parts.Length > 1 && TryParseAll(parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries), out var listed) ? listed : [];

        return new PetFood(numbers[0], numbers[1], numbers[2], types);
    }

    public bool Feeds(int petType) => PetTypes.Count == 0 || PetTypes.Contains(petType);

    private static bool TryParseAll(string[] values, out int[] numbers)
    {
        numbers = new int[values.Length];

        for (var i = 0; i < values.Length; i++) {
            if (!int.TryParse(values[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i])) {
                return false;
            }
        }

        return true;
    }
}
