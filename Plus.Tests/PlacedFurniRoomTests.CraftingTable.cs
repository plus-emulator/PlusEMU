using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Crafting;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Crafting;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint CraftingTableId = 60;
    private const uint CraftingTableDefinition = 300;
    private const string Stone = "survival_c26_stonesymbol";
    private const string Food = "survival_c26_foodsupplies";
    private const string Sleepingbag = "survival_c26_sleepingbag";
    private const string Stream = "survival_c26_stream";
    private const string Cave = "survival_c26_cave";
    private const string Faction = "survival_c26_factionsymbols";
    private const string Rope = "survival_c26_rope";
    private const string Trap = "survival_c26_trap";

    // The fake clock reads 2040-02-03 04:05:06 UTC.
    private const string CraftingTimestamp = "2/3/40, 4:05 AM";
    private static readonly byte[] FailedCraftingResult = [0, 0, 0, 0, 0, 0, 0];

    [Theory]
    [InlineData(7u)]
    [InlineData(8u)]
    public async Task ACraftingTableListsItsRecipesByCodeWithTheirIngredientsInFirstUseOrder(uint owner)
    {
        var (service, _) = CraftingTable(owner);

        await new GetCraftableProductsEvent(service).Parse(_client, ClientPacket((int)CraftingTableId));

        Assert.Equal([ServerPacketHeader.CraftableProductsComposer], _client.Sent);
        var packet = new FlashIncomingPacket { Buffer = Body(ServerPacketHeader.CraftableProductsComposer) };
        Assert.Equal(3, packet.ReadInt());

        foreach (var product in new[] { Cave, Faction, Stream }) {
            Assert.Equal((product, product, product), (packet.ReadString(), packet.ReadString(), packet.ReadString()));
        }

        Assert.Equal(5, packet.ReadInt());
        Assert.Equal([Stream, Sleepingbag, Food, Stone, Rope], Enumerable.Range(0, 5).Select(_ => packet.ReadString()));
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public async Task AskingForTheRecipesOfAFurniThatIsNotACraftingTableReportsError1300()
    {
        var (service, _) = CraftingTable(7, InteractionType.None);

        await new GetCraftableProductsEvent(service).Parse(_client, ClientPacket((int)CraftingTableId));

        Assert.Equal([ServerPacketHeader.ErrorReportComposer], _client.Sent);
        Assert.Equal(ErrorReport(1300), Body(ServerPacketHeader.ErrorReportComposer));
    }

    [Fact]
    public async Task AnyoneGetsARecipeWithItsIngredientsInRecipeOrder()
    {
        var (service, _) = CraftingTable(8);

        await new GetCraftingRecipeEvent(service).Parse(_client, ClientPacket(Cave));

        var packet = new FlashIncomingPacket { Buffer = Body(ServerPacketHeader.CraftingRecipeComposer) };
        Assert.Equal(3, packet.ReadInt());
        Assert.Equal([(1, Stream), (2, Sleepingbag), (1, Food)], Enumerable.Range(0, 3).Select(_ => (packet.ReadInt(), packet.ReadString())));
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public async Task AnUndiscoveredSecretRecipeStillGetsItsIngredients()
    {
        var (service, _) = CraftingTable(8);

        await new GetCraftingRecipeEvent(service).Parse(_client, ClientPacket(Trap));

        Assert.Equal([ServerPacketHeader.CraftingRecipeComposer], _client.Sent);
        var packet = new FlashIncomingPacket { Buffer = Body(ServerPacketHeader.CraftingRecipeComposer) };
        Assert.Equal(2, packet.ReadInt());
        Assert.Equal([(1, Rope), (1, Food)], Enumerable.Range(0, 2).Select(_ => (packet.ReadInt(), packet.ReadString())));
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public async Task AnUnknownRecipeGetsNoAnswer()
    {
        var (service, _) = CraftingTable(7);

        await new GetCraftingRecipeEvent(service).Parse(_client, ClientPacket("no_such_recipe"));

        Assert.Empty(_client.Sent);
    }

    [Theory]
    [InlineData(Stream)]
    [InlineData("no_such_recipe")]
    public async Task CraftingOnSomeoneElsesTableGetsNoAnswer(string code)
    {
        var (service, store) = CraftingTable(8);
        CraftingIngredients((20, Stone), (21, Stone), (22, Sleepingbag));

        await new CraftEvent(service).Parse(_client, ClientPacket((int)CraftingTableId, code));
        await new CraftSecretEvent(service).Parse(_client, ClientPacket((int)CraftingTableId, 3, 20, 21, 22));

        Assert.Empty(_client.Sent);
        Assert.Empty(store.Consumed);
        Assert.Equal(3, CraftingInventory().Furniture.GetItems.Count());
    }

    [Fact]
    public async Task CraftingAnUnknownRecipeAnswersFalseWithThreeEmptyStrings()
    {
        var (service, store) = CraftingTable(7);

        await new CraftEvent(service).Parse(_client, ClientPacket((int)CraftingTableId, "no_such_recipe"));

        Assert.Equal([ServerPacketHeader.CraftingResultComposer], _client.Sent);
        Assert.Equal(FailedCraftingResult, Body(ServerPacketHeader.CraftingResultComposer));
        Assert.Empty(store.Consumed);
    }

    [Fact]
    public async Task CraftingWithoutEveryIngredientGetsNoAnswer()
    {
        var (service, store) = CraftingTable(7);
        CraftingIngredients((20, Stone), (22, Sleepingbag));

        await new CraftEvent(service).Parse(_client, ClientPacket((int)CraftingTableId, Stream));

        Assert.Empty(_client.Sent);
        Assert.Empty(store.Consumed);
        Assert.Equal(2, CraftingInventory().Furniture.GetItems.Count());
    }

    [Fact]
    public async Task CraftingARecipeSwapsTheIngredientsForTheProductAndInvalidatesTheFurniList()
    {
        var (service, store) = CraftingTable(7);
        CraftingIngredients((20, Stone), (21, Stone), (22, Sleepingbag), (23, Food));

        await new CraftEvent(service).Parse(_client, ClientPacket((int)CraftingTableId, Stream));

        Assert.Equal(new uint[] { 20, 21, 22 }, store.Consumed);
        Assert.Equal([Food, Stream], CraftingInventory().Furniture.GetItems.Select(item => item.Definition.ItemName).Order());
        Assert.Equal([ServerPacketHeader.CraftingResultComposer, ServerPacketHeader.FurniListUpdateComposer], _client.Sent.TakeLast(2));
        Assert.Equal(new object[] { true, Stream, Stream, Stream }, CraftingResult());
    }

    [Fact]
    public async Task CraftingTheRightSecretMixCraftsTheSecretRecipe()
    {
        var (service, store) = CraftingTable(7);
        CraftingIngredients((25, Rope), (26, Food));

        await new CraftSecretEvent(service).Parse(_client, ClientPacket((int)CraftingTableId, 2, 25, 26));

        Assert.Equal(new uint[] { 25, 26 }, store.Consumed);
        Assert.Equal(Trap, Assert.Single(CraftingInventory().Furniture.GetItems).Definition.ItemName);
        Assert.Equal(new object[] { true, Trap, Trap, Trap }, CraftingResult());
        Assert.Equal(ServerPacketHeader.FurniListUpdateComposer, _client.Sent[^1]);
    }

    [Fact]
    public async Task CraftingAWrongSecretMixAnswersFalse()
    {
        var (service, store) = CraftingTable(7);
        CraftingIngredients((20, Stone), (22, Sleepingbag));

        await new CraftSecretEvent(service).Parse(_client, ClientPacket((int)CraftingTableId, 2, 20, 22));

        Assert.Equal([ServerPacketHeader.CraftingResultComposer], _client.Sent);
        Assert.Equal(FailedCraftingResult, Body(ServerPacketHeader.CraftingResultComposer));
        Assert.Empty(store.Consumed);
    }

    [Theory]
    [InlineData(23)]
    [InlineData(24)]
    [InlineData(999)]
    public async Task ASecretMixWithANonIngredientOrAnItemYouDoNotOwnReportsError1303(int item)
    {
        var (service, store) = CraftingTable(7);
        CraftingIngredients((20, Stone), (23, "rare_dragonlamp"), (24, Stone));
        CraftingInventory().Furniture.GetItem(24)!.OwnerId = 8;

        await new CraftSecretEvent(service).Parse(_client, ClientPacket((int)CraftingTableId, 2, 20, item));

        Assert.Equal([ServerPacketHeader.ErrorReportComposer], _client.Sent);
        Assert.Equal(ErrorReport(1303), Body(ServerPacketHeader.ErrorReportComposer));
        Assert.Empty(store.Consumed);
    }

    [Fact]
    public async Task RecipesAvailableCountsTheMixAndReportsANonIngredientWithError1304()
    {
        var (service, _) = CraftingTable(7);
        CraftingIngredients((23, "rare_dragonlamp"), (25, Rope), (26, Food));
        var available = new GetCraftingRecipesAvailableEvent(service);

        await available.Parse(_client, ClientPacket((int)CraftingTableId, 1, 25));
        Assert.Equal(Ints(1).Append((byte)0), Body(ServerPacketHeader.CraftingRecipesAvailableComposer));

        _client.Packets.Clear();
        await available.Parse(_client, ClientPacket((int)CraftingTableId, 2, 25, 26));
        Assert.Equal(Ints(0).Append((byte)1), Body(ServerPacketHeader.CraftingRecipesAvailableComposer));

        _client.Sent.Clear();
        _client.Packets.Clear();
        await available.Parse(_client, ClientPacket((int)CraftingTableId, 2, 25, 23));
        Assert.Equal([ServerPacketHeader.ErrorReportComposer], _client.Sent);
        Assert.Equal(ErrorReport(1304), Body(ServerPacketHeader.ErrorReportComposer));
    }

    [Fact]
    public async Task UsingACraftingTableChangesNothingInTheRoom()
    {
        CraftingTable(7);
        var table = _room.GetRoomItemHandler().GetItem(CraftingTableId)!;

        await new UseFurnitureEvent(new FurnitureUseService(null!, Proxy<IQuestManager>((_, _) => null)))
            .Parse(_room, _client, ClientPacket((int)CraftingTableId, 0));

        Assert.Equal("0", table.LegacyDataString);
        Assert.Empty(_client.Sent);
        Assert.Equal(InteractionType.CraftingTable, InteractionTypes.GetTypeFromString("crafting_table"));
    }

    private (CraftingService Service, CraftingTableStore Store) CraftingTable(uint owner, InteractionType interaction = InteractionType.CraftingTable)
    {
        var table = Furni(CraftingTableId, interaction, WiredBoxType.None);
        table.OwnerId = owner;
        table.Definition.Id = CraftingTableDefinition;
        table.Definition.ItemName = "survival_c26_craftingtable";
        table.Definition.Modes = 3;
        table.ExtraData = new LegacyDataFormat { Data = "0" };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, table, 1, 1, 0, true, false, false));
        var habbo = _client.GetHabbo();
        habbo.Client = _client;
        habbo.Inventory = new InventoryComponent { Furniture = new FurnitureInventoryComponent([], []) };
        Assert.True(_room.GetRoomUserManager().AddAvatarToRoom(_client));

        // Item ids differ from the official ingredient order on purpose.
        var definitions = new CraftingDefinitions(new[] { Stone, Food, Sleepingbag, Stream, Cave, Faction, Rope, Trap, "rare_dragonlamp" }
            .Select((name, index) => new ItemDefinition { Id = 301 + (uint)index, ItemName = name, Type = ItemType.Floor, ProductType = "s" }));
        uint Id(string name) => definitions.GetItemByName(name)!.Id;
        var store = new CraftingTableStore([
            new(1, Stream, Stream, Id(Stream), false, false, null, "", [new(Id(Stone), 2), new(Id(Sleepingbag), 1)]),
            new(2, Cave, Cave, Id(Cave), false, false, null, "", [new(Id(Stream), 1), new(Id(Sleepingbag), 2), new(Id(Food), 1)]),
            new(3, Faction, Faction, Id(Faction), false, false, null, "", [new(Id(Food), 1), new(Id(Stone), 1)]),
            new(4, Trap, Trap, Id(Trap), true, false, null, "", [new(Id(Rope), 1), new(Id(Food), 1)])
        ]);
        _craftingDefinitions = definitions;
        var service = new CraftingService(store, definitions, Proxy<IAchievementManager>((_, _) => false),
            TestLogging.For<CraftingService>(), _interactionClock);
        _client.Sent.Clear();
        _client.Packets.Clear();

        return (service, store);
    }

    private CraftingDefinitions? _craftingDefinitions;

    private void CraftingIngredients(params (uint Id, string Name)[] items)
    {
        foreach (var (id, name) in items) {
            Assert.True(CraftingInventory().Furniture.AddItem(new InventoryItem { Id = id, OwnerId = 7, Definition = _craftingDefinitions!.GetItemByName(name)! }));
        }
    }

    private InventoryComponent CraftingInventory() => Assert.IsType<InventoryComponent>(_client.GetHabbo().Inventory);

    private object[] CraftingResult()
    {
        var packet = new FlashIncomingPacket { Buffer = Body(ServerPacketHeader.CraftingResultComposer) };
        object[] result = [packet.ReadBool(), packet.ReadString(), packet.ReadString(), packet.ReadString()];
        Assert.False(packet.HasDataRemaining());

        return result;
    }

    private static byte[] ErrorReport(int messageId)
    {
        var text = Encoding.UTF8.GetBytes(CraftingTimestamp);
        var length = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(length, (short)text.Length);

        return [.. Ints(messageId, 1), .. length, .. text];
    }

    private sealed class CraftingTableStore(ImmutableArray<CraftingRecipe> recipes) : ICraftingStore
    {
        public uint[] Consumed { get; private set; } = [];

        public ImmutableArray<CraftingRecipe> Load(uint altarDefinitionId, int userId)
        {
            Assert.Equal((CraftingTableDefinition, 7), (altarDefinitionId, userId));

            return recipes;
        }

        public CraftingRecipe? Find(string code, int userId) => recipes.SingleOrDefault(recipe => recipe.Code == code);

        public CraftedItem? Craft(int userId, uint roomId, uint altarId, CraftingRecipe recipe, IReadOnlyList<uint> ids, bool secretCraft)
        {
            Assert.Equal((7, RoomId, CraftingTableId), (userId, roomId, altarId));
            Consumed = ids.ToArray();

            return new(99, recipe, recipe.Secret && !recipe.Discovered);
        }
    }
}
