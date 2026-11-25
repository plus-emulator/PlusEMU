using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms.AI;

/// <summary>Pet toys. A toy shows state 1 while a pet plays with it.</summary>
public static class PetToy
{
    public static bool Is(ItemDefinition definition) => definition.ItemName == "pet_toy_ball";

    public static bool Suits(ItemDefinition definition, int petType) => throw new NotImplementedException();
}
