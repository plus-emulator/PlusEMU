using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.AI.Types;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void TiredHighLevelPetRefusesCommandAndGainsNoExperience(int energy)
    {
        var (petUser, pet) = HighLevelPet(energy);

        var owner = LegacyRider();
        var ai = new PetBot(petUser.VirtualId, new RecordingLocale(), new RecordingCommands(3));
        ai.Init(50, petUser.VirtualId, RoomId, petUser, _room);

        ai.OnUserSay(owner, "horse sit");

        Assert.DoesNotContain("sit", petUser.Statusses.Keys);
        Assert.Equal(1600, pet.Experience);
        Assert.DoesNotContain(ServerPacketHeader.AddExperiencePointsComposer, _client.Sent);
    }

    [Fact]
    public void RestedHighLevelPetObeysCommandAndGainsExperience()
    {
        var (petUser, pet) = HighLevelPet(100);

        var owner = LegacyRider();
        var ai = new PetBot(petUser.VirtualId, new RecordingLocale(), new RecordingCommands(3));
        ai.Init(50, petUser.VirtualId, RoomId, petUser, _room);

        ai.OnUserSay(owner, "horse sit");

        Assert.Contains("sit", petUser.Statusses.Keys);
        Assert.Equal(1610, pet.Experience);
        Assert.Contains(ServerPacketHeader.AddExperiencePointsComposer, _client.Sent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(11)]
    public void SpendingEnergyNeverGoesBelowZero(int energy)
    {
        var pet = Assert.IsType<Pet>(LegacyHorse(2, 1).PetData);
        pet.Energy = energy;

        for (var i = 0; i < 50; i++) {
            pet.PetEnergy(false);
            Assert.True(pet.Energy >= 0, $"energy {pet.Energy}");
        }
    }

    private (RoomUser PetUser, Pet Pet) HighLevelPet(int energy)
    {
        var petUser = LegacyHorse(2, 1);
        var pet = Assert.IsType<Pet>(petUser.PetData);
        pet.ExperienceLevels = Enumerable.Range(1, 20).Select(i => i * 100).ToArray();
        pet.Experience = 1600;
        pet.Energy = energy;
        Assert.True(pet.Level > 15);

        return (petUser, pet);
    }
}
