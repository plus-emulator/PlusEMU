using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Chests;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

// Cancel Transaction and the two transaction triggers.
public sealed partial class WiredTurboRemainderG1RuntimeTests
{
    private static WiredRuntimeEvent Leave(G1Player player) => new(WiredEventKind.Leave) { Actor = player.User };

    /// <summary>Stack (3,0): leave room, then cancel transaction for the picked contracts, or every transaction.</summary>
    private static void AddCancel(G1World world, bool every, params Item[] contracts)
    {
        world.AddBox("wf_trg_leave_room", null, 3, 0);
        world.AddBox("wf_act_cancel_transaction", _ => G1World.Chest("wf_act_cancel_transaction", [every ? 1 : 0], [], [100], [0], [], [.. contracts.Select(contract => contract.Id)], []), 3, 0);
    }

    [Fact]
    public void CancellingASpecifiedContractEndsItsTradeAfterTheGraceAndRunsTheFailStack()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [chest], [contract], failSelectsContracts: true);
        AddCancel(world, false, contract);
        var item = player.Give(G1World.Furni(201));
        world.Fire(Enter(player));
        world.Module.Offer(player.Client, [item.Id], true);
        world.Module.Confirm(player.Client, false);

        world.Fire(Leave(player));

        // The window stays during the grace so that an Initiate Transaction on the same stack can replace it without closing it.
        Assert.True(player.User.IsTrading);
        Assert.Empty(stacks.Failed.Runs);
        world.Module.Confirm(player.Client, true);
        Assert.Empty(world.Store.Requests);

        world.Advance(TimeSpan.FromMilliseconds(499));
        Assert.True(player.User.IsTrading);
        world.Advance(TimeSpan.FromMilliseconds(1));

        Assert.False(player.User.IsTrading);
        var failed = Assert.Single(stacks.Failed.Runs);
        Assert.Equal((int)WiredChestFailure.TradeCancelled, failed.Code);
        Assert.Equal(contract.Id, failed.SourceId);
        Assert.True(player.Owns(item.Id));
        Assert.Empty(world.Store.Chests[chest.Id].Items);
        Assert.Equal(1, player.Sent(ServerPacketHeader.WiredChestTradeCancelledComposer));
    }

    [Fact]
    public void AFailTriggerWithNoContractSelectedDoesNotFireWhenWiredCancelsTheTrade()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [chest], [contract]);
        AddCancel(world, false, contract);
        var item = player.Give(G1World.Furni(201));
        world.Fire(Enter(player));
        world.Module.Offer(player.Client, [item.Id], true);
        world.Fire(Leave(player));
        world.Advance(TimeSpan.FromMilliseconds(500));

        Assert.False(player.User.IsTrading);
        Assert.Empty(stacks.Failed.Runs);
    }

    [Fact]
    public void CancellingAnotherContractLeavesTheTradeAlone()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var other = world.AddContract(WiredContractKind.Payment, Pays(), 4, 1);
        var stacks = Initiate(world, player, [chest], [contract]);
        AddCancel(world, false, other);
        world.Fire(Enter(player));

        world.Fire(Leave(player));
        world.Advance(TimeSpan.FromSeconds(5));

        Assert.True(player.User.IsTrading);
        Assert.True(world.Module.HasPending);
        Assert.Empty(stacks.Failed.Runs);
    }

    [Fact]
    public void CancellingEveryTransactionEndsAnyContractTradeButNotAPlainChestDeposit()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [chest], [contract], failSelectsContracts: true);
        AddCancel(world, true);
        var item = player.Give(G1World.Furni(201));

        // A user's own deposit window is not a payment or trade of a contract.
        Assert.True(world.Module.Start(player.Client, chest.Id, [chest], null));
        world.Fire(Leave(player));
        world.Advance(TimeSpan.FromSeconds(5));
        Assert.True(player.User.IsTrading);
        Assert.Empty(stacks.Failed.Runs);
        world.Module.Cancel(player.Client);

        world.Fire(Enter(player));
        world.Module.Offer(player.Client, [item.Id], true);
        world.Fire(Leave(player));
        world.Advance(TimeSpan.FromMilliseconds(500));

        Assert.False(player.User.IsTrading);
        Assert.Equal((int)WiredChestFailure.TradeCancelled, Assert.Single(stacks.Failed.Runs).Code);
        Assert.True(player.Owns(item.Id));
    }

    [Fact]
    public void CancellingWithNothingToCancelStillLeavesTheStateAsItWas()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var other = world.AddPlayer(2);
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [chest], [contract]);
        AddCancel(world, true);
        world.Fire(Enter(other));

        // Cancelling for a user who has no transaction changes nothing for the user who has one.
        world.Fire(Leave(player));
        world.Advance(TimeSpan.FromSeconds(5));

        Assert.False(player.User.IsTrading);
        Assert.True(other.User.IsTrading);
        Assert.Empty(stacks.Failed.Runs);
    }

    [Fact]
    public void ACancelAndANewInitiateOnOneStackReplaceTheTradeWithoutClosingTheWindow()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [chest], [contract], failSelectsContracts: true);
        AddCancel(world, true);
        world.AddBox("wf_act_init_transaction", _ => G1World.Chest("wf_act_init_transaction", [0, 1, 0, 0, 0, 300], [3], [100, 101, 0], [0, 0], ["n"],
            [chest.Id], [contract.Id]), 3, 0, 1);
        // Effects of equal delay run in no fixed order; stacking them "in order" puts the cancellation first.
        world.AddBox("wf_xtra_exec_in_order", null, 3, 0);
        world.Fire(Enter(player));
        Assert.Equal(1, player.Sent(ServerPacketHeader.WiredChestTradeOpenComposer));

        // Cancel marks the trade, the Initiate beside it finds the cancellation pending and takes over.
        world.Fire(Leave(player));

        Assert.Equal(2, player.Sent(ServerPacketHeader.WiredChestTradeOpenComposer));
        Assert.True(player.User.IsTrading);
        Assert.Equal(0, player.Sent(ServerPacketHeader.WiredChestTradeCancelledComposer));
        var replaced = Assert.Single(stacks.Failed.Runs);
        Assert.Equal((int)WiredChestFailure.TradeCancelled, replaced.Code);
        world.Advance(TimeSpan.FromSeconds(5));
        Assert.True(player.User.IsTrading);
        Assert.Single(stacks.Failed.Runs);
    }

    [Fact]
    public void ACancelBoxWithNoUsersOrNothingPickedDoesNothing()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [chest], [contract]);
        AddCancel(world, false);
        world.Fire(Enter(player));

        world.Fire(Leave(player));
        world.Advance(TimeSpan.FromSeconds(5));

        Assert.True(player.User.IsTrading);
        Assert.Empty(stacks.Failed.Runs);
    }

    [Fact]
    public void TheTransactionTriggersFireOnlyForTheirOwnOutcomeAndForThePickedContracts()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Coins, 50);
        var rewarded = world.AddContract(WiredContractKind.Reward, Gives(20));
        var other = world.AddContract(WiredContractKind.Reward, Gives(10), 4, 1);
        var stacks = Initiate(world, player, [chest], [rewarded]);
        world.AddBox("wf_trg_transaction_complete", _ => G1World.Chest("wf_trg_transaction_complete", [], [], [100], [], [], [rewarded.Id], []), 5, 0);
        var forRewarded = world.AddWitness(5, 0);
        world.AddBox("wf_trg_transaction_complete", _ => G1World.Chest("wf_trg_transaction_complete", [], [], [100], [], [], [other.Id], []), 6, 0);
        var forOther = world.AddWitness(6, 0);
        world.AddBox("wf_trg_transaction_fail", _ => G1World.Chest("wf_trg_transaction_fail", [], [], [100], [], [], [rewarded.Id], []), 7, 0);
        var failForRewarded = world.AddWitness(7, 0);

        world.Fire(Enter(player));

        Assert.Single(stacks.Completed.Runs);
        Assert.Single(forRewarded.Runs);
        Assert.Empty(forOther.Runs);
        Assert.Empty(stacks.Failed.Runs);
        Assert.Empty(failForRewarded.Runs);
        Assert.Same(player.User, forRewarded.Runs[0].Actor);
    }

    [Fact]
    public void AFailTriggerPickedForAContractHearsOnlyThatContractsFailuresWithTheirReason()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Coins, 5);
        var big = world.AddContract(WiredContractKind.Reward, Gives(80));
        var small = world.AddContract(WiredContractKind.Reward, Gives(1), 4, 1);
        world.AddBox("wf_trg_enter_room");
        world.AddBox("wf_act_init_transaction", _ => G1World.Chest("wf_act_init_transaction", [0, 1, 0, 0, 0, 300], [3], [100, 101, 0], [0, 0], ["n"],
            [chest.Id], [big.Id]));
        world.AddBox("wf_trg_transaction_fail", _ => G1World.Chest("wf_trg_transaction_fail", [], [], [100], [], [], [big.Id], []), 1, 1);
        var forBig = world.AddWitness(1, 1);
        world.AddBox("wf_trg_transaction_fail", _ => G1World.Chest("wf_trg_transaction_fail", [], [], [100], [], [], [small.Id], []), 2, 1);
        var forSmall = world.AddWitness(2, 1);
        world.AddBox("wf_trg_transaction_complete", _ => G1World.Chest("wf_trg_transaction_complete", [], [], [100], [], [], [], []), 3, 1);
        var complete = world.AddWitness(3, 1);

        world.Fire(Enter(player));

        var failed = Assert.Single(forBig.Runs);
        Assert.Empty(forSmall.Runs);
        Assert.Empty(complete.Runs);
        var context = new WiredRuntimeContext(world.Room, failed, new(() => [], () => [player.User]), new G1Operations());
        var holder = new WiredVariableHolder(WiredVariableTarget.Context, 0, 0);
        Assert.Equal((int)WiredChestFailure.FundsGone, WiredChestVariables.Read(world.Module, context, holder, "@event.transaction_failed.reason"));
        Assert.Null(WiredChestVariables.Read(world.Module, context, holder, "@event.transaction_complete.multiplier"));
    }

    [Fact]
    public void TheCompleteEventReadsAsTheFiguresOfTheTransactionAndNeverAsAFailureReason()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var furni = world.AddChest(WiredChestKind.Furni);
        var coins = world.AddChest(WiredChestKind.Coins, 100);
        var contract = world.AddContract(WiredContractKind.Trade, Trades(2, 5));
        var stacks = Initiate(world, player, [furni, coins], [contract], [1, 2, 0, 0, 0, 300]);
        var offered = Enumerable.Range(201, 4).Select(id => player.Give(G1World.Furni((uint)id)).Id).ToArray();
        world.Fire(Enter(player));
        world.Module.Offer(player.Client, offered, true);
        world.Module.Confirm(player.Client, false);
        world.Module.Confirm(player.Client, true);
        world.Flush();

        var completed = Assert.Single(stacks.Completed.Runs);
        var context = new WiredRuntimeContext(world.Room, completed, new(() => [], () => [player.User]), new G1Operations());
        var holder = new WiredVariableHolder(WiredVariableTarget.Context, 0, 0);
        long? Read(string name) => WiredChestVariables.Read(world.Module, context, holder, name);
        Assert.Equal(2, Read("@event.transaction_complete.multiplier"));
        Assert.Equal(4, Read("@event.transaction_complete.deposit.furni_count"));
        Assert.Equal(0, Read("@event.transaction_complete.deposit.coins_count"));
        Assert.Equal(0, Read("@event.transaction_complete.withdrawal.furni_count"));
        Assert.Equal(10, Read("@event.transaction_complete.withdrawal.coins_count"));
        Assert.Null(Read("@event.transaction_failed.reason"));
    }

    [Fact]
    public void ACompleteTriggerStillMatchesItsContractAfterTheContractFurniLeftTheRoom()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [chest], [contract]);
        world.AddBox("wf_trg_transaction_complete", _ => G1World.Chest("wf_trg_transaction_complete", [], [], [100], [], [], [contract.Id], []), 5, 0);
        var picked = world.AddWitness(5, 0);
        var item = player.Give(G1World.Furni(201));
        world.Fire(Enter(player));
        world.Module.Offer(player.Client, [item.Id], true);
        world.Module.Confirm(player.Client, false);
        world.Floor.TryRemove(contract.Id, out _);

        world.Module.Confirm(player.Client, true);
        world.Flush();

        Assert.False(player.Owns(item.Id));
        Assert.Single(picked.Runs);
    }

    internal sealed class G1Operations : IWiredRuntimeOperations
    {
        public bool CallStacks(WiredRuntimeContext context, IEnumerable<Item> targets, bool negative = false) => throw new NotSupportedException();
        public bool SendSignal(WiredRuntimeContext context, IEnumerable<Item> receivers, WiredSelection selection, bool negative = false) => throw new NotSupportedException();
        public void ResetTimers(IEnumerable<Item> targets) => throw new NotSupportedException();
    }

    [Fact]
    public void AFailStackThatStartsTheSameFailingTransactionAgainDoesNotRunAwayWithinOneCycle()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        // No contract is picked, so every initiation fails; the fail trigger beside it initiates again.
        world.AddBox("wf_trg_enter_room");
        world.AddBox("wf_act_init_transaction", _ => G1World.Chest("wf_act_init_transaction", [0, 1, 0, 0, 0, 300], [3], [100, 101, 0], [0, 0], ["n"], [chest.Id], []));
        world.AddBox("wf_trg_transaction_fail", _ => G1World.Chest("wf_trg_transaction_fail", [], [], [100], [], [], [], []), 1, 1);
        world.AddBox("wf_act_init_transaction", _ => G1World.Chest("wf_act_init_transaction", [0, 1, 0, 0, 0, 300], [3], [100, 101, 0], [0, 0], ["n"], [chest.Id], []), 1, 1);

        world.Fire(Enter(player));

        Assert.InRange(world.Published.Count, 2, 200);
        Assert.All(world.Published, evt => Assert.Equal((int)WiredChestFailure.Misconfig, evt.Code));
        Assert.False(player.User.IsTrading);
    }
}
