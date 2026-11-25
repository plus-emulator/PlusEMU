using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms.AI;

/// <summary>Pet toys. A toy shows state 1 while a pet plays with it. The pond is a toy only for the pets in its custom params.</summary>
public static class PetToy
{
    private static readonly string[] ForEveryPet = ["pet_toy_ball", "toy1", "toy2", "pet_ufo_toy", "pet_puppy_toy"];

    public static bool Suits(ItemDefinition definition, int petType) =>
        IsForEveryPet(definition.ItemName) || (IsPond(definition) && ListsPetType(definition, petType));

    public static bool IsPond(ItemDefinition definition) => definition.ItemName == "pond";

    /// <summary>True when the comma separated custom params name the pet type.</summary>
    internal static bool ListsPetType(ItemDefinition definition, int petType)
    {
        var values = definition.CustomParams.AsSpan();

        foreach (var range in values.Split(',')) {
            if (int.TryParse(values[range].Trim(), out var type) && type == petType) {
                return true;
            }
        }

        return false;
    }

    private static bool IsForEveryPet(string name)
    {
        var star = name.IndexOf('*');
        var baseName = star < 0 ? name.AsSpan() : name.AsSpan(0, star);

        foreach (var toy in ForEveryPet) {
            if (baseName.SequenceEqual(toy)) {
                return true;
            }
        }

        return false;
    }
}
