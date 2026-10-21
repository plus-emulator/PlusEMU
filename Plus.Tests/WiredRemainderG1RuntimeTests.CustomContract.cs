using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Chests;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

// The custom contract add-on, with Initiate Transaction beside it on one stack.
public sealed partial class WiredRemainderG1RuntimeTests
{
    /// <summary>
    /// Stack (6,0): enter room, the custom contract, initiate transaction on the chests with no picked contract. The payment side is the first
    /// five ints, the reward side the last five: enabled, furni (1) or credits (0), variable option, amount, variable target.
    /// </summary>
    private static (IWiredConfiguredItem Init, IWiredConfiguredItem Custom) CustomStack(G1World world, int[] sides, Item[] chests, Item[] paymentTypes, Item[] rewardTypes,
        string paymentVariable = "n", string rewardVariable = "n", int[]? init = null, double customZ = 0)
    {
        world.AddBox("wf_trg_enter_room", null, 6, 0);
        var custom = world.AddBox("wf_xtra_custom_contract", _ => G1World.Chest("wf_xtra_custom_contract", sides, [4, 9], [100, 101, 0, 0], [0, 0], [paymentVariable, rewardVariable],
            [.. paymentTypes.Select(item => item.Id)], [.. rewardTypes.Select(item => item.Id)]), 6, 0, customZ);
        var initiate = world.AddBox("wf_act_init_transaction", _ => G1World.Chest("wf_act_init_transaction", init ?? [0, 1, 0, 0, 0, 300], [3], [100, 101, 0], [0, 0], ["n"],
            [.. chests.Select(chest => chest.Id)], []), 6, 0, 5);

        return (initiate, custom);
    }

    private static G1Witness WatchOutcomes(G1World world, out G1Witness failed)
    {
        world.AddBox("wf_trg_transaction_complete", _ => G1World.Chest("wf_trg_transaction_complete", [], [], [100], [], [], [], []), 8, 0);
        var completed = world.AddWitness(8, 0);
        world.AddBox("wf_trg_transaction_fail", _ => G1World.Chest("wf_trg_transaction_fail", [], [], [100], [], [], [], []), 8, 1);
        failed = world.AddWitness(8, 1);

        return completed;
    }

    [Fact]
    public void ACustomRewardPaysTheEnabledAmountAndTheTransactionBelongsToTheInitiateBox()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var coins = world.AddChest(WiredChestKind.Coins, 50);
        var (initiate, _) = CustomStack(world, [0, 0, 0, 1, 0, 1, 0, 0, 7, 0], [coins], [], []);
        var completed = WatchOutcomes(world, out var failed);

        world.Fire(Enter(player));

        Assert.Equal(107, player.Credits);
        Assert.Equal(43, world.Store.Chests[coins.Id].Coins);
        var run = Assert.Single(completed.Runs);
        Assert.Equal(initiate.Item.Id, run.SourceId);
        Assert.Equal(new WiredChestFigures(1, 0, 0, 0, 7), run.Transaction);
        Assert.Empty(failed.Runs);
        Assert.False(player.User.IsTrading);
    }

    [Fact]
    public void ACustomPaymentOnlyContractTakesCreditFurniAndKeepsTheVoucherWhole()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var coins = world.AddChest(WiredChestKind.Coins, 0);
        CustomStack(world, [1, 0, 0, 3, 0, 0, 0, 0, 1, 0], [coins], [], []);
        var completed = WatchOutcomes(world, out var failed);
        var voucher = player.Give(G1World.Voucher(301, 5));
        var furni = player.Give(G1World.Furni(302));
        world.Fire(Enter(player));
        Assert.True(player.User.IsTrading);

        // Only credit furni fit a credits payment.
        world.Module.Offer(player.Client, [furni.Id], true);
        world.Module.Offer(player.Client, [voucher.Id], true);
        world.Module.Confirm(player.Client, false);
        world.Module.Confirm(player.Client, true);
        world.Flush();

        Assert.False(player.Owns(voucher.Id));
        Assert.True(player.Owns(furni.Id));
        Assert.Equal(5, world.Store.Chests[coins.Id].Coins);
        Assert.Equal(100, player.Credits);
        Assert.Equal(new WiredChestFigures(1, 0, 5, 0, 0), Assert.Single(completed.Runs).Transaction);
        Assert.Empty(failed.Runs);
    }

    [Fact]
    public void ACustomTradeOfPickedFurniTypesForCreditsRunsThroughTheWholeWindow()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chair = world.AddItem("chair", InteractionType.None, 4, 4, 44);
        var furni = world.AddChest(WiredChestKind.Furni);
        var coins = world.AddChest(WiredChestKind.Coins, 100, 3, 4);
        CustomStack(world, [1, 1, 0, 2, 0, 1, 0, 0, 4, 0], [furni, coins], [chair], []);
        var completed = WatchOutcomes(world, out var failed);
        var a = player.Give(G1World.Furni(201));
        var b = player.Give(G1World.Furni(202));
        var wrong = player.Give(G1World.Furni(203, 55));
        world.Fire(Enter(player));

        world.Module.Offer(player.Client, [wrong.Id], true);
        Assert.Empty(world.Store.Requests);
        world.Module.Offer(player.Client, [a.Id, b.Id], true);
        world.Module.Confirm(player.Client, false);
        world.Module.Confirm(player.Client, true);
        world.Flush();

        Assert.Equal(104, player.Credits);
        Assert.Equal(2, world.Store.Chests[furni.Id].Items.Count);
        Assert.True(player.Owns(wrong.Id));
        Assert.Equal(new WiredChestFigures(1, 2, 0, 0, 4), Assert.Single(completed.Runs).Transaction);
        Assert.Empty(failed.Runs);
    }

    [Fact]
    public void ACustomRewardOfAPickedFurniTypeGivesThoseFurniOutOfTheChest()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var lamp = world.AddItem("lamp", InteractionType.None, 4, 4, 55);
        var furni = world.AddChest(WiredChestKind.Furni, 0, 2, 2, G1World.Furni(1, 44), G1World.Furni(2, 55), G1World.Furni(3, 55), G1World.Furni(4, 55));
        CustomStack(world, [0, 0, 0, 1, 0, 1, 1, 0, 2, 0], [furni], [], [lamp]);
        var completed = WatchOutcomes(world, out _);

        world.Fire(Enter(player));

        Assert.Equal(2, player.Habbo.Inventory!.Furniture.GetItems.Count(item => item.Definition.SpriteId == 55));
        Assert.Equal(2, world.Store.Chests[furni.Id].Items.Count);
        Assert.Contains(world.Store.Chests[furni.Id].Items, item => item.Definition.SpriteId == 44);
        Assert.Equal(new WiredChestFigures(1, 0, 0, 2, 0), Assert.Single(completed.Runs).Transaction);
    }

    [Fact]
    public void ACustomContractWithBothSidesOffOrAFuniSideWithoutAFuniIsAMisconfiguration()
    {
        foreach (var sides in new[] { new[] { 0, 0, 0, 1, 0, 0, 0, 0, 1, 0 }, new[] { 0, 0, 0, 1, 0, 1, 1, 0, 2, 0 } }) {
            using var world = new G1World();
            var player = world.AddPlayer();
            var coins = world.AddChest(WiredChestKind.Coins, 50);
            var (initiate, _) = CustomStack(world, sides, [coins], [], []);
            var completed = WatchOutcomes(world, out var failed);

            world.Fire(Enter(player));

            var run = Assert.Single(failed.Runs);
            Assert.Equal((int)WiredChestFailure.Misconfig, run.Code);
            Assert.Equal(initiate.Item.Id, run.SourceId);
            Assert.Equal(100, player.Credits);
            Assert.Empty(completed.Runs);
            Assert.False(player.User.IsTrading);
        }
    }

    [Fact]
    public void ASideWhoseAmountVariableIsEmptyOrOutOfRangeFallsOutAndTheOtherSideStands()
    {
        foreach (var amount in new[] { 0, 100001 }) {
            using var world = new G1World();
            var player = world.AddPlayer();
            var coins = world.AddChest(WiredChestKind.Coins, 50);
            var (module, reference, holder) = UserVariable(world, player, amount);
            // Payment amount from the user's variable, reward a fixed 7: the payment drops out and the contract becomes a reward.
            CustomStack(world, [1, 0, 1, 1, 0, 1, 0, 0, 7, 0], [coins], [], [], paymentVariable: "user:10");
            var completed = WatchOutcomes(world, out var failed);

            world.Fire(Enter(player));

            Assert.Equal(107, player.Credits);
            Assert.Single(completed.Runs);
            Assert.Empty(failed.Runs);
            Assert.NotNull(module);
            Assert.NotNull(reference);
            Assert.NotNull(holder);
        }
    }

    [Fact]
    public void ACustomContractWinsOverAPickedContractAndTheSourceIsTheInitiateBox()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var coins = world.AddChest(WiredChestKind.Coins, 500);
        var picked = world.AddContract(WiredContractKind.Reward, Gives(99));
        world.AddBox("wf_trg_enter_room", null, 6, 0);
        world.AddBox("wf_xtra_custom_contract", _ => G1World.Chest("wf_xtra_custom_contract", [0, 0, 0, 1, 0, 1, 0, 0, 7, 0], [4, 9], [100, 101, 0, 0], [0, 0], ["n", "n"], [], []), 6, 0);
        var initiate = world.AddBox("wf_act_init_transaction", _ => G1World.Chest("wf_act_init_transaction", [0, 1, 0, 0, 0, 300], [3], [100, 101, 0], [0, 0], ["n"],
            [coins.Id], [picked.Id]), 6, 0, 5);
        world.AddBox("wf_trg_transaction_complete", _ => G1World.Chest("wf_trg_transaction_complete", [], [], [100], [], [], [picked.Id], []), 8, 0);
        var forContract = world.AddWitness(8, 0);
        world.AddBox("wf_trg_transaction_complete", _ => G1World.Chest("wf_trg_transaction_complete", [], [], [100], [], [], [initiate.Item.Id], []), 8, 1);
        var forBox = world.AddWitness(8, 1);

        world.Fire(Enter(player));

        Assert.Equal(107, player.Credits);
        Assert.Empty(forContract.Runs);
        Assert.Single(forBox.Runs);
    }

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 0.0)]
    public void TheContractIsBuiltWhenTheTransactionStartsSoTheScannerMayRunBeforeOrAfterIt(double scannerZ, double customZ)
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chair = world.AddItem("chair", InteractionType.None, 4, 4, 44);
        var stock = world.AddChest(WiredChestKind.Furni, 0, 2, 2, G1World.Furni(1, 44), G1World.Furni(2, 44), G1World.Furni(3, 44));
        var coins = world.AddChest(WiredChestKind.Coins, 50, 3, 4);
        var module = world.UseVariables(ContextCount);
        world.AddBox("wf_trg_enter_room", null, 6, 0);
        world.AddBox("wf_xtra_scan_chest_furni_by_type", _ => G1World.Chest("wf_xtra_scan_chest_furni_by_type", [0], [], [101, 100], [], ["ctx:10"], [stock.Id], [chair.Id]), 6, 0, scannerZ);
        // The reward is as many credits as the scanner counted chairs.
        world.AddBox("wf_xtra_custom_contract", _ => G1World.Chest("wf_xtra_custom_contract", [0, 0, 0, 1, 0, 1, 0, 1, 1, 2], [4, 9], [100, 101, 0, 0], [0, 0], ["n", "ctx:10"], [], []), 6, 0, customZ);
        world.AddBox("wf_act_init_transaction", _ => G1World.Chest("wf_act_init_transaction", [0, 1, 0, 0, 0, 300], [3], [100, 101, 0], [0, 0], ["n"], [coins.Id], []), 6, 0, 5);
        var completed = WatchOutcomes(world, out var failed);

        world.Fire(Enter(player));

        Assert.Equal(103, player.Credits);
        Assert.Equal(47, world.Store.Chests[coins.Id].Coins);
        Assert.Single(completed.Runs);
        Assert.Empty(failed.Runs);
        Assert.NotNull(module);
    }

    [Fact]
    public void TheMultiplierCanComeFromAVariableOfTheUserAndAMissingOneIsAMisconfiguration()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var other = world.AddPlayer(2);
        var coins = world.AddChest(WiredChestKind.Coins, 500);
        var contract = world.AddContract(WiredContractKind.Reward, Gives(10));
        UserVariable(world, player, 4);
        world.AddBox("wf_trg_enter_room");
        world.AddBox("wf_act_init_transaction", _ => G1World.Chest("wf_act_init_transaction", [1, 1, 1, 0, 0, 300], [3], [100, 101, 0], [0, 0], ["user:10"],
            [coins.Id], [contract.Id]));
        var completed = WatchOutcomes(world, out var failed);

        world.Fire(Enter(player));
        Assert.Equal(140, player.Credits);
        Assert.Equal(4, Assert.Single(completed.Runs).Transaction!.Multiplier);

        // The other user has no such variable: nothing is paid and the box reports the misconfiguration.
        world.Fire(Enter(other));
        Assert.Equal(100, other.Credits);
        Assert.Equal((int)WiredChestFailure.Misconfig, Assert.Single(failed.Runs).Code);
        Assert.Same(other.User, failed.Runs[0].Actor);
    }

    [Fact]
    public void AMultiplierVariableBeyondTheLimitsIsClampedToTheLimits()
    {
        foreach (var (value, expected) in new[] { (9999, 500), (-3, 1), (0, 1) }) {
            using var world = new G1World();
            var player = world.AddPlayer();
            var coins = world.AddChest(WiredChestKind.Coins, 100000);
            var contract = world.AddContract(WiredContractKind.Reward, Gives(1));
            UserVariable(world, player, value);
            world.AddBox("wf_trg_enter_room");
            world.AddBox("wf_act_init_transaction", _ => G1World.Chest("wf_act_init_transaction", [1, 1, 1, 0, 0, 300], [3], [100, 101, 0], [0, 0], ["user:10"],
                [coins.Id], [contract.Id]));
            var completed = WatchOutcomes(world, out _);

            world.Fire(Enter(player));

            Assert.Equal(expected, Assert.Single(completed.Runs).Transaction!.Multiplier);
            Assert.Equal(100 + expected, player.Credits);
        }
    }
}
