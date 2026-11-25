using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.AI.Types;
using Xunit;

namespace Plus.Tests;

// Habbo gives a pet 100 + 20 energy per level and a happiness bar of 100. Rest refills the maximum energy in one hour,
// one point at a time, while the pet is below its maximum; food can push it above the maximum.
public partial class PlacedFurniRoomTests
{
    private static readonly int[] HabboLevels =
        [100, 200, 400, 600, 900, 1300, 1800, 2400, 3200, 4300, 7200, 8500, 10100, 13300, 17500, 23000, 51900, 75000, 128000, 150000];

    [Theory]
    [InlineData(0, 120)]
    [InlineData(110, 140)]
    [InlineData(14_000, 400)]
    public void MaxEnergyIsOneHundredPlusTwentyPerLevel(int experience, int maxEnergy)
    {
        var pet = RestingPet(experience, 50);

        var info = new PetInformationService(TimeProvider.System).Capture(pet);

        Assert.Equal(maxEnergy, info.MaxEnergy);
    }

    [Fact]
    public void MaxHappinessIsOneHundred()
    {
        var pet = RestingPet(0, 50);

        Assert.Equal(100, new PetInformationService(TimeProvider.System).Capture(pet).MaxNutrition);
    }

    [Theory]
    [InlineData(0, 2, 54)]
    [InlineData(110, 6, 64)]
    [InlineData(250, 3, 58)]
    public void RestRefillsTheMaximumEnergyInOneHour(int experience, int minutes, int energy)
    {
        var pet = RestingPet(experience, 50);

        Tick(pet, TimeSpan.FromMinutes(minutes));

        Assert.Equal(energy, pet.Energy);
    }

    [Theory]
    [InlineData(120)]
    [InlineData(157)]
    [InlineData(832)]
    public void RestNeverAddsEnergyAtOrAboveTheMaximum(int energy)
    {
        var pet = RestingPet(0, energy);

        Tick(pet, TimeSpan.FromMinutes(5));

        Assert.Equal(energy, pet.Energy);
    }

    private Pet RestingPet(int experience, int energy)
    {
        var pet = Assert.IsType<Pet>(LegacyHorse(2, 1).PetData);
        pet.ExperienceLevels = HabboLevels;
        pet.Experience = experience;
        pet.Energy = energy;

        return pet;
    }

    private void Tick(Pet pet, TimeSpan duration)
    {
        Assert.True(_room.GetRoomUserManager().TryGetPet(pet.PetId, out var petUser));
        var ai = new PetBot(petUser.VirtualId, new RecordingLocale(), new RecordingCommands(0));
        ai.Init(50, petUser.VirtualId, RoomId, petUser, _room);

        for (var tick = 0; tick < duration / Plus.HabboHotel.Rooms.RoomCycle.Interval; tick++) {
            ai.OnTimerTick();
        }
    }
}
