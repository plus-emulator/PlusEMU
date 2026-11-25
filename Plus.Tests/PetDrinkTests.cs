using System.Buffers.Binary;
using System.Text;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;
using Xunit;

namespace Plus.Tests;

// Pets drink from water bowls and the horse trough like on Habbo. Drink sends the pet onto a bowl or next to the
// trough; there it takes the "eat" posture, says a drinking line and the water goes down by one. Water that nobody
// drank for a few minutes is full again at the next drink; an empty bowl stays empty while pets keep drinking.
// Pets also go to drink by themselves. The milk bowl is only for the pets in its custom params and never runs out.
public partial class PlacedFurniRoomTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("waterbowl*1", true)]
    [InlineData("waterbowl_basic*3", true)]
    [InlineData("water_bowl1*2", true)]
    [InlineData("horse_trough", true)]
    [InlineData("petfood1", false)]
    [InlineData("pet_toy_ball", false)]
    public void WaterBowlsAndTheTroughHoldWater(string name, bool water)
    {
        Assert.Equal(water, PetWater.Suits(new ItemDefinition { ItemName = name }, 0));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(28, false)]
    [InlineData(0, false)]
    public void TheMilkBowlIsForTheCat(int petType, bool drinks)
    {
        Assert.Equal(drinks, PetWater.Suits(new ItemDefinition { ItemName = "milkbowl", CustomParams = "1" }, petType));
    }

    [Fact]
    public void ACatDrinksMilkByItselfAndTheBowlStaysFull()
    {
        LegacyRider();
        var petUser = PetUser(CommandPet(1, 17_600, 420));
        var bowl = Water(80, "milkbowl", "5", 3, 3, true);
        bowl.Definition.CustomParams = "1";
        var ai = CommandBot(petUser, -1);
        _client.Packets.Clear();

        for (var tick = 0; tick < 4000 && !petUser.Statusses.ContainsKey("eat"); tick++) {
            ai.OnTimerTick();

            if (WalkTarget(petUser) == (3, 3) && petUser.Coordinate != new System.Drawing.Point(3, 3)) {
                ArriveAt(ai, petUser, 3, 3);
            }
        }

        Assert.Contains("eat", petUser.Statusses.Keys);
        Assert.Equal("5", bowl.LegacyDataString);
        Assert.Equal("*Splish splash*", ChatLines().Last());
    }

    [Fact]
    public void EachDrinkTakesOneWater()
    {
        var bowl = Water(80, "waterbowl*1", "5", 3, 3, true);

        PetWater.Drink(bowl, Noon);
        PetWater.Drink(bowl, Noon.AddSeconds(26));
        PetWater.Drink(bowl, Noon.AddSeconds(144));

        Assert.Equal("2", bowl.LegacyDataString);
    }

    [Fact]
    public void AnEmptyBowlStaysEmptyWhilePetsKeepDrinking()
    {
        var bowl = Water(80, "waterbowl*1", "5", 3, 3, true);
        PetWater.Drink(bowl, Noon);
        bowl.LegacyDataString = "1";

        PetWater.Drink(bowl, Noon.AddSeconds(20));
        PetWater.Drink(bowl, Noon.AddSeconds(65));

        Assert.Equal("0", bowl.LegacyDataString);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3")]
    public void WaterNobodyDrankForMinutesIsFullAgain(string left)
    {
        var bowl = Water(80, "waterbowl*1", "5", 3, 3, true);
        PetWater.Drink(bowl, Noon);
        bowl.LegacyDataString = left;

        PetWater.Drink(bowl, Noon.AddMinutes(9));

        Assert.Equal("4", bowl.LegacyDataString);
    }

    [Fact]
    public void WaterNoPetDrankSinceTheServerStartedIsFull()
    {
        var trough = Water(81, "horse_trough", "0", 3, 3, false);

        PetWater.Drink(trough, Noon);

        Assert.Equal("4", trough.LegacyDataString);
    }

    [Fact]
    public void DrinkSendsThePetOntoTheBowl()
    {
        var owner = LegacyRider();
        var pet = CommandPet(15, 17_600, 420);
        var petUser = PetUser(pet);
        Water(80, "waterbowl*1", "5", 3, 3, true);

        Command(petUser, owner, PetCommandSet.Drink);
        DrainPetCommands();

        Assert.Equal((3, 3), WalkTarget(petUser));
    }

    [Fact]
    public void DrinkSendsThePetNextToTheTrough()
    {
        var owner = LegacyRider();
        var pet = CommandPet(15, 17_600, 420);
        var petUser = PetUser(pet);
        var trough = Water(81, "horse_trough", "5", 3, 3, false);

        Command(petUser, owner, PetCommandSet.Drink);
        DrainPetCommands();

        var target = Assert.NotNull(WalkTarget(petUser));
        Assert.NotEqual((3, 3), target);
        Assert.Equal(1, Math.Max(Math.Abs(target.X - trough.GetX), Math.Abs(target.Y - trough.GetY)));
    }

    [Fact]
    public void WithoutWaterAPetRefusesToDrink()
    {
        var owner = LegacyRider();
        var pet = CommandPet(15, 17_600, 420);
        var petUser = PetUser(pet);

        Command(petUser, owner, PetCommandSet.Drink);
        DrainPetCommands();

        Assert.Null(WalkTarget(petUser));
        Assert.Equal(17_600, pet.Experience);
    }

    [Fact]
    public void MovingTheBowlCancelsTheDrink()
    {
        var owner = LegacyRider();
        var user = PetUser(CommandPet(15, 17_600, 420));
        var bowl = Water(80, "waterbowl*1", "5", 3, 3, true);
        var ai = CommandBot(user, PetCommandSet.Drink);
        ai.OnUserSay(owner, "horse drink");
        DrainPetCommands();
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, bowl, 2, 2, 0, false, false, false));

        ArriveAt(ai, user, 3, 3);

        Assert.Equal("5", bowl.LegacyDataString);
        Assert.DoesNotContain("eat", user.Statusses.Keys);
    }

    [Fact]
    public void AtTheBowlThePetDrinksAndSaysADrinkingLine()
    {
        var owner = LegacyRider();
        var pet = CommandPet(15, 17_600, 420);
        var petUser = PetUser(pet);
        var bowl = Water(80, "waterbowl*1", "5", 3, 3, true);
        var ai = CommandBot(petUser, PetCommandSet.Drink);
        ai.OnUserSay(owner, "horse drink");
        DrainPetCommands();
        _client.Packets.Clear();

        ArriveAt(ai, petUser, 3, 3);

        Assert.Contains("eat", petUser.Statusses.Keys);
        Assert.Equal("4", bowl.LegacyDataString);
        Assert.Equal(["Ah, that's the good stuff."], ChatLines());
        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }

    [Fact]
    public void APetGoesToDrinkByItself()
    {
        var pet = CommandPet(15, 17_600, 420);
        var petUser = PetUser(pet);
        var bowl = Water(80, "waterbowl*1", "5", 3, 3, true);
        var ai = CommandBot(petUser, -1);

        for (var tick = 0; tick < 4000 && bowl.LegacyDataString == "5"; tick++) {
            ai.OnTimerTick();

            if (WalkTarget(petUser) == (3, 3)) {
                ArriveAt(ai, petUser, 3, 3);
            }
        }

        Assert.Equal("4", bowl.LegacyDataString);
    }

    private Item Water(uint id, string name, string state, int x, int y, bool walkable)
    {
        var water = Furni(id, InteractionType.None, WiredBoxType.None);
        water.Definition.ItemName = name;
        water.Definition.FurniLine = "pet_accessories";
        water.Definition.Walkable = walkable;
        water.ExtraData = new LegacyDataFormat { Data = state };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, water, x, y, 0, true, false, false));

        return water;
    }
}
