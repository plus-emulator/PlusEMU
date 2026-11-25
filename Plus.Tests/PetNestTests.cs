using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms.AI;
using Xunit;

namespace Plus.Tests;

// Pets use nests like on Habbo: now and then a pet walks onto a nest by itself, lies down and says a sleeping line.
// It stays there for about a minute and gets no extra energy for it.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData("nest_plow_red", "nests", true)]
    [InlineData("nest", "nests", true)]
    [InlineData("nest_basket", "", true)]
    [InlineData("pet_breeding_dog", "pet_accessories", false)]
    [InlineData("waterbowl*1", "pet_accessories", false)]
    public void NestsAreTheNestFurni(string name, string line, bool nest)
    {
        Assert.Equal(nest, PetNest.Is(new ItemDefinition { ItemName = name, FurniLine = line }));
    }

    [Fact]
    public void APetLiesDownInANestByItselfAndSaysASleepingLine()
    {
        LegacyRider();
        var pet = CommandPet(15, 17_600, 420);
        var petUser = PetUser(pet);
        Nest(90, 3, 3);
        var ai = CommandBot(petUser, -1);
        _client.Packets.Clear();

        for (var tick = 0; tick < 6000 && !petUser.Statusses.ContainsKey("lay"); tick++) {
            ai.OnTimerTick();

            if (WalkTarget(petUser) == (3, 3) && petUser.Coordinate != new System.Drawing.Point(3, 3)) {
                ArriveAt(ai, petUser, 3, 3);
            }
        }

        Assert.Contains("lay", petUser.Statusses.Keys);
        Assert.Equal((3, 3), (petUser.X, petUser.Y));
        Assert.Contains(Assert.Single(ChatLines()), HorseSleepingLines);
    }

    [Fact]
    public void ASleepingPetStaysInTheNestForAboutAMinuteWithoutExtraEnergy()
    {
        var pet = CommandPet(15, 17_600, 300);
        var petUser = PetUser(pet);
        Nest(90, 3, 3);
        var ai = CommandBot(petUser, -1);

        for (var tick = 0; tick < 6000 && !petUser.Statusses.ContainsKey("lay"); tick++) {
            ai.OnTimerTick();

            if (WalkTarget(petUser) == (3, 3) && petUser.Coordinate != new System.Drawing.Point(3, 3)) {
                ArriveAt(ai, petUser, 3, 3);
            }
        }

        var energy = pet.Energy;
        var walk = WalkTarget(petUser);

        for (var tick = 0; tick < 70; tick++) {
            ai.OnTimerTick();
        }

        Assert.Contains("lay", petUser.Statusses.Keys);
        Assert.Equal(walk, WalkTarget(petUser));
        var rest = (int)Math.Ceiling(pet.MaxEnergy * (70 * Plus.HabboHotel.Rooms.RoomCycle.Interval / TimeSpan.FromHours(1)));
        Assert.InRange(pet.Energy - energy, 0, rest);
    }

    private static readonly string[] HorseSleepingLines =
        ["Zzzz.... Marston!", "Zzzz... Old Dan Tucker....", "Zzzz... I have a heart that can never be tamed..."];

    private Item Nest(uint id, int x, int y)
    {
        var nest = Furni(id, InteractionType.None, WiredBoxType.None);
        nest.Definition.ItemName = "nest_plow_red";
        nest.Definition.FurniLine = "nests";
        nest.Definition.Walkable = true;
        nest.ExtraData = new LegacyDataFormat { Data = "" };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, nest, x, y, 0, true, false, false));

        return nest;
    }
}
