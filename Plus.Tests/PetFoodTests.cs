using System.Buffers.Binary;
using System.Data;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.AI.Types;
using Xunit;

namespace Plus.Tests;

// Pet food furni feed pets like on Habbo. Eat sends the pet to the food without experience. On the food the pet
// takes bites with the "eat" posture while it is below its maximum energy, and at least one bite after the command.
// Each bite adds a tenth of the food's energy (rounded half up) and a fifth of its experience and moves the furni one state on.
// The fifth bite eats the food up: it leaves the room and the database. A hungry pet goes to eat by itself.
public partial class PlacedFurniRoomTests
{
    private static readonly string[] HorseEatingLines = ["What a delicacy, yum, yum", "Yum, yum, this is delicious", "This is delicious!"];

    [Theory]
    [InlineData("petfood1", "pet_food", "375,10,0", 375, 10, 0, "")]
    [InlineData("petfood18", "pet_food", "375,15,0 8,26,30", 375, 15, 0, "8,26,30")]
    [InlineData("goodie1", "pet_food", "0,0,100", 0, 0, 100, "")]
    [InlineData("petfood_19", "", "1375,20,0 9,26", 1375, 20, 0, "9,26")]
    public void PetFoodReadsEnergyExperienceHappinessAndPetTypes(string name, string line, string customParams, int energy, int experience, int happiness, string types)
    {
        var food = PetFood.Of(new ItemDefinition { ItemName = name, FurniLine = line, CustomParams = customParams });

        Assert.NotNull(food);
        Assert.Equal((energy, experience, happiness), (food.Energy, food.Experience, food.Happiness));
        Assert.Equal(types.Length == 0 ? [] : types.Split(',').Select(int.Parse).ToArray(), food.PetTypes);
    }

    [Theory]
    [InlineData("petfood19", "pet_food", "8,26")]
    [InlineData("waterbowl*1", "pet_accessories", "")]
    [InlineData("chair", "", "375,10,0")]
    public void FurniWithoutFoodValuesIsNoPetFood(string name, string line, string customParams)
    {
        Assert.Null(PetFood.Of(new ItemDefinition { ItemName = name, FurniLine = line, CustomParams = customParams }));
    }

    [Theory]
    [InlineData("petfood28", "pet_food")]
    [InlineData("petfood31", "")]
    public void PetFoodWithoutValuesGivesEveryPetTwentyFiveEnergyAndTenExperiencePerBite(string name, string line)
    {
        var food = PetFood.Of(new ItemDefinition { ItemName = name, FurniLine = line, CustomParams = "" });

        Assert.NotNull(food);
        Assert.Equal((25, 10), (food.BiteEnergy, food.BiteExperience));
        Assert.True(food.Feeds(0));
        Assert.True(food.Feeds(11));
    }

    [Fact]
    public void FoodWithPetTypesFeedsOnlyThoseTypes()
    {
        var food = PetFood.Of(new ItemDefinition { ItemName = "petfood26", FurniLine = "pet_food", CustomParams = "375,15,0 15,26,35" })!;

        Assert.True(food.Feeds(15));
        Assert.False(food.Feeds(0));
        Assert.True(PetFood.Of(new ItemDefinition { ItemName = "petfood1", CustomParams = "375,10,0" })!.Feeds(0));
    }

    [Fact]
    public void EatSendsThePetToTheFoodWithoutExperience()
    {
        var owner = LegacyRider();
        var pet = CommandPet(15, 17_600, 420);
        var petUser = PetUser(pet);
        PlaceFood(70, "petfood1", "375,10,0", 3, 3);
        _client.Packets.Clear();

        Command(petUser, owner, PetCommandSet.Eat);
        DrainPetCommands();

        Assert.Equal((3, 3), WalkTarget(petUser));
        Assert.Equal((17_600, 420), (pet.Experience, pet.Energy));
        Assert.Equal([0], ExperiencePackets());
    }

    [Fact]
    public void APetAtItsMaximumTakesOneBiteAfterTheEatCommand()
    {
        var owner = LegacyRider();
        var pet = CommandPet(15, 17_600, 420);
        var petUser = PetUser(pet);
        var food = PlaceFood(70, "petfood1", "375,10,0", 3, 3);
        var ai = CommandBot(petUser, PetCommandSet.Eat);
        ai.OnUserSay(owner, "horse eat");
        DrainPetCommands();
        _client.Packets.Clear();

        ArriveAt(ai, petUser, 3, 3);

        Assert.Contains("eat", petUser.Statusses.Keys);
        Assert.Equal("1", food.LegacyDataString);
        Assert.Equal((17_602, 420 + 38), (pet.Experience, pet.Energy));
        Assert.Equal([2], ExperiencePackets());
    }

    [Fact]
    public void AHungryPetKeepsEatingUntilItReachesItsMaximum()
    {
        var owner = LegacyRider();
        var pet = CommandPet(15, 17_600, 300);
        var petUser = PetUser(pet);
        var food = PlaceFood(70, "petfood1", "375,10,0", 3, 3);
        var ai = CommandBot(petUser, PetCommandSet.Eat);
        ai.OnUserSay(owner, "horse eat");
        DrainPetCommands();
        _client.Packets.Clear();

        ArriveAt(ai, petUser, 3, 3);

        Assert.Equal("4", food.LegacyDataString);
        Assert.Equal((17_608, 300 + 4 * 38), (pet.Experience, pet.Energy));
        Assert.Equal([8], ExperiencePackets());
    }

    [Fact]
    public void TheFifthBiteEatsTheFoodUpAndDeletesIt()
    {
        var store = UseRecordingItemStore();
        var owner = LegacyRider();
        var pet = CommandPet(15, 17_600, 20);
        var petUser = PetUser(pet);
        var food = PlaceFood(70, "petfood1", "375,10,0", 3, 3);
        var ai = CommandBot(petUser, PetCommandSet.Eat);
        ai.OnUserSay(owner, "horse eat");
        DrainPetCommands();
        _client.Packets.Clear();

        ArriveAt(ai, petUser, 3, 3);

        Assert.Null(_room.GetRoomItemHandler().GetItem(70));
        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectRemoveComposer);
        Assert.Empty(store.Deleted);
        store.FailNextDelete = true;
        _room.GetRoomItemHandler().Dispose();
        Assert.Equal([(70u, RoomId)], store.Deleted);
        Assert.Equal(20 + 5 * 38, pet.Energy);
    }

    [Fact]
    public void PetCannotEatAtTheOldFoodPosition()
    {
        var owner = LegacyRider();
        var pet = CommandPet(15, 17_600, 420);
        var user = PetUser(pet);
        var food = PlaceFood(70, "petfood1", "375,10,0", 3, 3);
        var ai = CommandBot(user, PetCommandSet.Eat);
        ai.OnUserSay(owner, "horse eat");
        DrainPetCommands();
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, food, 2, 2, 0, false, false, false));

        ArriveAt(ai, user, 3, 3);

        Assert.Equal("0", food.LegacyDataString);
        Assert.Equal(17_600, pet.Experience);
    }

    [Fact]
    public void TemporaryFoodCannotGiveExperience()
    {
        var owner = LegacyRider();
        var pet = CommandPet(15, 17_600, 420);
        var user = PetUser(pet);
        var definition = Furni(70, InteractionType.None, WiredBoxType.None).Definition;
        definition.ItemName = "petfood1";
        definition.CustomParams = "375,10,0";
        definition.Walkable = true;
        var food = new Item
        {
            Id = TemporaryItemIds.First,
            OwnerId = 7,
            RoomId = RoomId,
            IsTemporary = true,
            Definition = definition,
            GetX = 3,
            GetY = 3,
            ExtraData = new LegacyDataFormat { Data = "4" }
        };
        _room.GetRoomItemHandler().LoadFurniture([food]);
        var ai = CommandBot(user, PetCommandSet.Eat);
        ai.OnUserSay(owner, "horse eat");
        DrainPetCommands();

        ArriveAt(ai, user, 3, 3);

        Assert.Equal(17_600, pet.Experience);
        Assert.Equal("4", food.LegacyDataString);
        Assert.Same(food, _room.GetRoomItemHandler().GetItem(food.Id));
    }

    [Fact]
    public void APetRefusesToEatFoodMadeForOtherPetTypes()
    {
        var owner = LegacyRider();
        var pet = CommandPet(0, 17_600, 420);
        var petUser = PetUser(pet);
        PlaceFood(70, "petfood26", "375,15,0 15,26,35", 3, 3);

        Command(petUser, owner, PetCommandSet.Eat);
        DrainPetCommands();

        Assert.NotEqual((3, 3), WalkTarget(petUser));
        Assert.Equal(17_600, pet.Experience);
    }

    [Fact]
    public void AHungryPetGoesToEatByItself()
    {
        var pet = CommandPet(15, 17_600, 100);
        var petUser = PetUser(pet);
        PlaceFood(70, "petfood1", "375,10,0", 3, 3);
        var ai = CommandBot(petUser, -1);

        for (var tick = 0; tick < 120 && WalkTarget(petUser) != (3, 3); tick++) {
            ai.OnTimerTick();
        }

        Assert.Equal((3, 3), WalkTarget(petUser));
    }

    [Fact]
    public void APetAtItsMaximumDoesNotGoToEatByItself()
    {
        var pet = CommandPet(15, 17_600, 420);
        var petUser = PetUser(pet);
        PlaceFood(70, "petfood1", "375,10,0", 3, 3);
        var ai = CommandBot(petUser, -1);

        for (var tick = 0; tick < 120; tick++) {
            ai.OnTimerTick();
        }

        Assert.Equal("0", _room.GetRoomItemHandler().GetItem(70)!.LegacyDataString);
        Assert.Equal(420, pet.Energy);
    }

    [Fact]
    public void AnEatingHorseSaysAHorseEatingLine()
    {
        var owner = LegacyRider();
        var pet = CommandPet(15, 17_600, 420);
        var petUser = PetUser(pet);
        PlaceFood(70, "petfood1", "375,10,0", 3, 3);
        var ai = CommandBot(petUser, PetCommandSet.Eat);
        ai.OnUserSay(owner, "horse eat");
        DrainPetCommands();
        _client.Packets.Clear();

        ArriveAt(ai, petUser, 3, 3);

        Assert.Contains(Assert.Single(ChatLines()), HorseEatingLines);
    }

    private Item PlaceFood(uint id, string name, string customParams, int x, int y)
    {
        var food = Furni(id, InteractionType.None, WiredBoxType.None);
        food.Definition.ItemName = name;
        food.Definition.FurniLine = "pet_food";
        food.Definition.CustomParams = customParams;
        food.Definition.Walkable = true;
        food.ExtraData = new LegacyDataFormat { Data = "0" };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, food, x, y, 0, true, false, false));

        return food;
    }

    private PetBot CommandBot(RoomUser petUser, int command)
    {
        var ai = new PetBot(petUser.VirtualId, new RecordingLocale(), new RecordingCommands(command));
        ai.Init(50, petUser.VirtualId, RoomId, petUser, _room);
        petUser.BotAi = ai;

        return ai;
    }

    private static (int X, int Y)? WalkTarget(RoomUser petUser) => petUser.Movement.Commands.Read() is { } walk ? (walk.X, walk.Y) : null;

    private static void ArriveAt(PetBot ai, RoomUser petUser, int x, int y)
    {
        petUser.X = x;
        petUser.Y = y;
        petUser.IsWalking = false;
        ai.OnTimerTick();
    }

    private List<int> ExperiencePackets() => _client.Packets.Where(packet => packet.Header == ServerPacketHeader.AddExperiencePointsComposer)
        .Select(packet => BinaryPrimitives.ReadInt32BigEndian(packet.Body.AsSpan(8))).ToList();

    private DeletingItemStore UseRecordingItemStore()
    {
        var store = new DeletingItemStore();
        Set("_roomItemHandling", new RoomItemHandling(_room, store, TestRoomItemMetadataStore.Instance, TestGameClientManager.Empty, TestLanguageManager.RoomItems,
            TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards));

        return store;
    }

    private sealed class TemporaryStoreFailure : System.Data.Common.DbException
    {
        public override bool IsTransient => true;
    }

    private sealed class DeletingItemStore : IRoomItemStore
    {
        public List<(uint ItemId, uint RoomId)> Deleted { get; } = [];
        public void AssignOwner(uint itemId, int userId) { }
        public void ClearRoom(uint itemId) { }
        public void SaveWallPosition(uint itemId, string wallPosition) { }
        public void SaveMoved(IReadOnlyList<RoomItemSave> items) { }
        public void PlaceFloor(uint itemId, uint roomId, int x, int y, double z, int rotation) { }
        public void PlaceWall(uint itemId, uint roomId, int x, int y, double z, int rotation, string wallPosition) { }
        public bool FailNextDelete { get; set; }

        public void DeleteConsumed(uint roomId, IReadOnlyCollection<uint> itemIds)
        {
            if (FailNextDelete) {
                FailNextDelete = false;

                throw new TemporaryStoreFailure();
            }

            Deleted.AddRange(itemIds.Select(id => (id, roomId)));
        }
    }
}
