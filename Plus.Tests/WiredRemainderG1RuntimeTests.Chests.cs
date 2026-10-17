using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Chests;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// The two chest conditions, the chest scanner and the custom contract add-on.
public sealed partial class WiredRemainderG1RuntimeTests
{
    // The AIR comparison radios: < 0, = 1, > 2, <= 3, != 4, >= 5.
    private static bool Compares(int comparison, long amount, long reference) => comparison switch
    {
        0 => amount < reference,
        1 => amount == reference,
        2 => amount > reference,
        3 => amount <= reference,
        4 => amount != reference,
        _ => amount >= reference
    };

    /// <summary>Stack (6,0): enter room, the condition, a witness. Returns the witness: it ran when the condition passed.</summary>
    private static G1Witness HasItemsStack(G1World world, int value, int comparison, Item[] chests, int x = 6, int option = 0, string variable = "n", int target = 2)
    {
        world.AddBox("wf_trg_enter_room", null, x, 0);
        world.AddBox("wf_cnd_chest_has_items", _ => G1World.Chest("wf_cnd_chest_has_items", [value, option, target, comparison], [2], [100, 200], [0], [variable],
            [.. chests.Select(chest => chest.Id)], []), x, 0);

        return world.AddWitness(x, 0);
    }

    private static G1Witness HasTypeStack(G1World world, Item[] samples, int value, int comparison, Item[] chests, int x = 6)
    {
        world.AddBox("wf_trg_enter_room", null, x, 0);
        world.AddBox("wf_cnd_chest_has_item_type", _ => G1World.Chest("wf_cnd_chest_has_item_type", [value, 0, 2, comparison], [2], [100, 101, 0], [0], ["n"],
            [.. samples.Select(sample => sample.Id)], [.. chests.Select(chest => chest.Id)]), x, 0);

        return world.AddWitness(x, 0);
    }

    [Theory]
    [InlineData(0, 4, false)]
    [InlineData(0, 5, false)]
    [InlineData(0, 6, true)]
    [InlineData(1, 5, true)]
    [InlineData(1, 6, false)]
    [InlineData(2, 4, true)]
    [InlineData(2, 5, false)]
    [InlineData(3, 5, true)]
    [InlineData(3, 4, false)]
    [InlineData(4, 5, false)]
    [InlineData(4, 4, true)]
    [InlineData(5, 5, true)]
    [InlineData(5, 6, false)]
    public void HasItemsComparesTheCoinsInAChestWithTheAmountByEveryOperator(int comparison, int value, bool passes)
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Coins, 5);
        var witness = HasItemsStack(world, value, comparison, [chest]);

        world.Fire(Enter(player));

        Assert.Equal(passes, witness.Runs.Count == 1);
        Assert.Equal(passes, Compares(comparison, 5, value));
    }

    [Fact]
    public void HasItemsSumsEveryPickedChestOfEitherKind()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var coins = world.AddChest(WiredChestKind.Coins, 5);
        var furni = world.AddChest(WiredChestKind.Furni, 0, 3, 3, G1World.Furni(1), G1World.Furni(2), G1World.Furni(3));
        var exact = HasItemsStack(world, 8, 1, [coins, furni], 6);
        var below = HasItemsStack(world, 9, 1, [coins, furni], 7);

        world.Fire(Enter(player));

        Assert.Single(exact.Runs);
        Assert.Empty(below.Runs);
    }

    [Fact]
    // Observed on AIR WIN63-202610151745-318700621 (r97_chest_none2): with no chest to check, the condition passes whatever the comparison.
    public void HasItemsPassesWithoutAnyChestWhateverTheComparison()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chair = world.AddItem("chair", InteractionType.None, 4, 4);
        var chest = world.AddChest(WiredChestKind.Coins, 5);
        var none = HasItemsStack(world, 100, 0, [], 6);
        // A picked furni that is not a chest is refused at save (observed on AIR WIN63-202610151745-318700621, r97_chest_none).
        Assert.Equal("wiredfurni.error.require_chests", Assert.ThrowsAny<Exception>(() => HasItemsStack(world, 100, 0, [chair], 7)).Message);
        var gone = HasItemsStack(world, 100, 0, [chest], 8);
        world.Floor.TryRemove(chest.Id, out _);

        world.Fire(Enter(player));

        Assert.Single(none.Runs);
        Assert.Single(gone.Runs);
    }

    [Fact]
    public void HasItemsCountsAChestThatIsLockedOrNotWiredEnabledToo()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Coins, 5);
        world.Store.Chests[chest.Id].Settings = world.Store.Chests[chest.Id].Settings with { Locked = true, WiredEnabled = false };
        var witness = HasItemsStack(world, 5, 1, [chest]);

        world.Fire(Enter(player));

        Assert.Single(witness.Runs);
    }

    [Fact]
    public void HasItemsFollowsTheChestAsItEmptiesAndFills()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Coins, 5);
        var atLeastOne = HasItemsStack(world, 1, 5, [chest]);

        world.Fire(Enter(player));
        Assert.Single(atLeastOne.Runs);

        world.Store.Chests[chest.Id].Coins = 0;
        world.Fire(Enter(player));
        Assert.Single(atLeastOne.Runs);

        world.Store.Chests[chest.Id].Coins = 9;
        world.Fire(Enter(player));
        Assert.Equal(2, atLeastOne.Runs.Count);
    }

    private static (WiredVariableModule Module, WiredVariableReference Reference, WiredVariableHolder Holder) UserVariable(G1World world, G1Player player, int value)
    {
        var module = world.UseVariables(new WiredVariableDefinition(10, G1World.RoomId, 1, "amount", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true));
        var context = new WiredRuntimeContext(world.Room, new(WiredEventKind.Enter) { Actor = player.User }, new(() => [], () => [player.User]), new G1Operations());
        var frame = WiredVariableRuntimeFrames.Create(context);
        var reference = new WiredVariableReference(WiredVariableTarget.User, "custom:10");
        var holder = WiredVariableRuntimeFrames.UserHolder(player.User);
        Assert.True(module.Mutate(reference, holder, WiredVariableMutation.Give, value, frame));

        return (module, reference, holder);
    }

    [Fact]
    public void HasItemsReadsTheAmountFromAVariableOfTheTriggeringUserInARealStack()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var other = world.AddPlayer(2);
        var chest = world.AddChest(WiredChestKind.Coins, 5);
        UserVariable(world, player, 5);
        var equal = HasItemsStack(world, 0, 1, [chest], 6, option: 1, variable: "user:10", target: 0);
        var greater = HasItemsStack(world, 0, 2, [chest], 7, option: 1, variable: "user:10", target: 0);

        world.Fire(Enter(player));
        Assert.Single(equal.Runs);
        Assert.Empty(greater.Runs);

        // The other user holds no such variable, so the comparison has no amount to compare with.
        world.Fire(Enter(other));
        Assert.Single(equal.Runs);
        Assert.Empty(greater.Runs);
    }

    [Fact]
    public void HasItemTypeCountsOnlyThePickedTypesAndCountsEachTypeOnce()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chair = world.AddItem("chair", InteractionType.None, 4, 4, 44);
        var sameChair = world.AddItem("chair", InteractionType.None, 4, 5, 44);
        var lamp = world.AddItem("lamp", InteractionType.None, 5, 4, 55);
        var chest = world.AddChest(WiredChestKind.Furni, 0, 3, 3, G1World.Furni(1, 44), G1World.Furni(2, 44), G1World.Furni(3, 44), G1World.Furni(4, 55), G1World.Furni(5, 66));
        var otherChest = world.AddChest(WiredChestKind.Furni, 0, 3, 4, G1World.Furni(6, 44));
        var threeChairs = HasTypeStack(world, [chair, sameChair], 3, 1, [chest], 6);
        var fourChairsInBoth = HasTypeStack(world, [chair, sameChair], 4, 1, [chest, otherChest], 7);
        var chairsAndLamps = HasTypeStack(world, [chair, lamp], 4, 1, [chest], 8);
        var nothingPicked = HasTypeStack(world, [], 0, 1, [chest], 9);

        world.Fire(Enter(player));

        Assert.Single(threeChairs.Runs);
        Assert.Single(fourChairsInBoth.Runs);
        Assert.Single(chairsAndLamps.Runs);
        Assert.Single(nothingPicked.Runs);
    }

    [Fact]
    public void HasItemTypeSeparatesWallPostersByTheirPosterAndFloorFromWallOfTheSameSprite()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var poster = world.AddItem("poster", InteractionType.None, 4, 4, 1, wall: true);
        poster.ExtraData = new LegacyDataFormat { Data = "24" };
        var floorSprite = world.AddItem("floor_one", InteractionType.None, 4, 5, 1);
        InventoryItem Poster(uint id, string data) => new()
        {
            Id = id,
            Definition = new() { Id = 1, SpriteId = 1, Type = ItemType.Wall },
            ExtraData = new LegacyDataFormat { Data = data }
        };
        var chest = world.AddChest(WiredChestKind.Furni, 0, 3, 3, Poster(1, "24"), Poster(2, "24"), Poster(3, "30"), G1World.Furni(4, 1));
        var posters = HasTypeStack(world, [poster], 2, 1, [chest], 6);
        var floors = HasTypeStack(world, [floorSprite], 1, 1, [chest], 7);

        world.Fire(Enter(player));

        Assert.Single(posters.Runs);
        Assert.Single(floors.Runs);
    }

    [Fact]
    public void HasItemTypeCountsNoFurniInACoinChestAndPassesWithoutAChest()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chair = world.AddItem("chair", InteractionType.None, 4, 4, 44);
        var coins = world.AddChest(WiredChestKind.Coins, 50);
        var coinChest = HasTypeStack(world, [chair], 0, 1, [coins], 6);
        var missing = HasTypeStack(world, [chair], 0, 1, [], 7);

        world.Fire(Enter(player));

        // A coin chest holds no furni, so zero equals zero; with no chest at all the condition passes (observed on AIR
        // WIN63-202610151745-318700621, r97_chest_type_keep).
        Assert.Single(coinChest.Runs);
        Assert.Single(missing.Runs);
    }

    [Theory]
    [InlineData(0, 4, true)]
    [InlineData(0, 3, false)]
    [InlineData(1, 3, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(3, 2, false)]
    [InlineData(4, 3, false)]
    [InlineData(4, 2, true)]
    [InlineData(5, 3, true)]
    [InlineData(5, 4, false)]
    public void HasItemTypeComparesTheCountWithTheAmountByEveryOperator(int comparison, int value, bool passes)
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chair = world.AddItem("chair", InteractionType.None, 4, 4, 44);
        var chest = world.AddChest(WiredChestKind.Furni, 0, 3, 3, G1World.Furni(1, 44), G1World.Furni(2, 44), G1World.Furni(3, 44), G1World.Furni(4, 55));
        var witness = HasTypeStack(world, [chair], value, comparison, [chest], 6);

        world.Fire(Enter(player));

        // Three chairs in the chest.
        Assert.Equal(passes, witness.Runs.Count == 1);
    }

    private static readonly WiredVariableDefinition ContextCount = new(10, G1World.RoomId, 1, "chest_count", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, true);

    /// <summary>Stack (6,0): enter room, the scanner into context variable 10, a witness that reads that variable while the run is on.</summary>
    private static (G1Witness Witness, Func<long?> Scanned) ScannerStack(G1World world, int mode, Item[] samples, Item[] chests, int x = 6, double z = 0)
    {
        var module = world.UseVariables(ContextCount);
        world.AddBox("wf_trg_enter_room", null, x, 0);
        world.AddBox("wf_xtra_scan_chest_furni_by_type", _ => G1World.Chest("wf_xtra_scan_chest_furni_by_type", [mode], [], [100, 101], [], ["ctx:10"],
            [.. samples.Select(sample => sample.Id)], [.. chests.Select(chest => chest.Id)]), x, 0, z);
        var witness = world.AddWitness(x, 0);
        long? scanned = null;
        witness.Probe = context => scanned = module.Read(new(WiredVariableTarget.Context, "custom:10"), new(WiredVariableTarget.Context, 0, 0), context.VariableFrame!)?.Value;

        return (witness, () => scanned);
    }

    [Fact]
    public void TheScannerCountsThePickedTypesInThePickedChestsIntoAContextVariableInARealStack()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chair = world.AddItem("chair", InteractionType.None, 4, 4, 44);
        var lamp = world.AddItem("lamp", InteractionType.None, 5, 4, 55);
        var chest = world.AddChest(WiredChestKind.Furni, 0, 3, 3, G1World.Furni(1, 44), G1World.Furni(2, 44), G1World.Furni(3, 55), G1World.Furni(4, 66));
        var other = world.AddChest(WiredChestKind.Furni, 0, 3, 4, G1World.Furni(5, 44));
        var coins = world.AddChest(WiredChestKind.Coins, 99, 3, 5);
        var (witness, scanned) = ScannerStack(world, 0, [chair, lamp], [chest, other, coins]);

        world.Fire(Enter(player));

        Assert.Single(witness.Runs);
        Assert.Equal(4, scanned());
    }

    [Fact]
    public void TheScannerCountsZeroWhenNoTypeIsPickedOrNothingMatches()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chair = world.AddItem("chair", InteractionType.None, 4, 4, 44);
        var chest = world.AddChest(WiredChestKind.Furni, 0, 3, 3, G1World.Furni(1, 55));
        var (witness, scanned) = ScannerStack(world, 0, [chair], [chest]);
        var (_, none) = ScannerStack(world, 0, [], [chest], 7);

        world.Fire(Enter(player));

        Assert.Equal(0, scanned());
        Assert.Equal(0, none());
        Assert.Single(witness.Runs);
    }

    [Theory]
    [InlineData(3, 2, 0)]
    [InlineData(5, 2, 2)]
    [InlineData(5, 3, 3)]
    [InlineData(3, 1, 0)]
    [InlineData(4, 2, 1)]
    [InlineData(6, 3, 1)]
    public void TheScannerInPreviewModeCountsOnlyWhatTheChestShowsAboveItself(int previewMode, int amount, int expected)
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chair = world.AddItem("chair", InteractionType.None, 4, 4, 44);
        // Stock in deposit order: three chairs, then two lamps. Modes 5 and 6 preview from the front, 3 and 4 from the back, and 4 and 6 show each type once.
        var chest = world.AddChest(WiredChestKind.Furni, 0, 3, 3, G1World.Furni(1, 44), G1World.Furni(2, 44), G1World.Furni(3, 44), G1World.Furni(4, 55), G1World.Furni(5, 55));
        world.Store.Chests[chest.Id].Settings = world.Store.Chests[chest.Id].Settings with { PreviewMode = previewMode, PreviewAmount = amount };
        var (_, scanned) = ScannerStack(world, 1, [chair], [chest]);

        world.Fire(Enter(player));

        Assert.Equal(expected, scanned());
    }

    [Fact]
    public void TheScannerCountsAllItemsWhateverThePreviewIs()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chair = world.AddItem("chair", InteractionType.None, 4, 4, 44);
        var chest = world.AddChest(WiredChestKind.Furni, 0, 3, 3, G1World.Furni(1, 44), G1World.Furni(2, 44), G1World.Furni(3, 55));
        world.Store.Chests[chest.Id].Settings = world.Store.Chests[chest.Id].Settings with { PreviewMode = 0 };
        var (_, scanned) = ScannerStack(world, 0, [chair], [chest]);

        world.Fire(Enter(player));

        Assert.Equal(2, scanned());
    }

    [Fact]
    public void TheScannerStoresNothingWithoutAContextVariable()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chair = world.AddItem("chair", InteractionType.None, 4, 4, 44);
        var chest = world.AddChest(WiredChestKind.Furni, 0, 3, 3, G1World.Furni(1, 44));
        var module = world.UseVariables(ContextCount, new(11, G1World.RoomId, 1, "per_user", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true));
        world.AddBox("wf_trg_enter_room");
        var box = world.AddBox("wf_xtra_scan_chest_furni_by_type", _ => G1World.Chest("wf_xtra_scan_chest_furni_by_type", [0], [], [100, 101], [], ["user:11"],
            [chair.Id], [chest.Id]));
        var witness = world.AddWitness(0, 0);
        long? seen = -1;
        witness.Probe = context => seen = module.Read(new(WiredVariableTarget.Context, "custom:10"), new(WiredVariableTarget.Context, 0, 0), context.VariableFrame ?? WiredVariableRuntimeFrames.Create(context))?.Value;

        world.Fire(Enter(player));

        Assert.Single(witness.Runs);
        Assert.Null(seen);
        Assert.NotNull(box);
    }
}
