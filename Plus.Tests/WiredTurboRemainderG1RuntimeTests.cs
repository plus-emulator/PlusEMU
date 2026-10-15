using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Chests;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The transaction cards of group g1, fired through the real wired engine and chest module: initiate and cancel transaction, the two
/// transaction triggers, the two chest conditions, the chest scanner and the custom contract add-on.
/// </summary>
public sealed partial class WiredTurboRemainderG1RuntimeTests
{
    private static WiredChestContract Pays(int amount = 1, int sprite = 44) => new() { Kind = WiredContractKind.Payment, PaymentMode = 1, Payment = [[new(amount, new(false, sprite))]] };
    private static WiredChestContract Gives(int coins) => new() { Kind = WiredContractKind.Reward, PaymentMode = 1, Reward = [new(coins)] };

    private static WiredRuntimeEvent Enter(G1Player player) => new(WiredEventKind.Enter) { Actor = player.User };

    /// <summary>Stack (0,0): enter room, initiate transaction on the chests and contracts. Stacks (1,0) and (1,1) witness the two outcomes.</summary>
    private sealed class Stacks
    {
        public required G1World World { get; init; }
        public required G1Player Player { get; init; }
        public required G1Witness Completed { get; init; }
        public required G1Witness Failed { get; init; }
    }

    private static Stacks Initiate(G1World world, G1Player player, Item[] chests, Item[] contracts, int[]? ints = null, Action<Stacks>? _ = null, bool failSelectsContracts = false)
    {
        world.AddBox("wf_trg_enter_room");
        world.AddBox("wf_act_init_transaction", _ => G1World.Chest("wf_act_init_transaction", ints ?? [0, 1, 0, 0, 0, 300], [3], [100, 101, 0], [0, 0], ["n"],
            [.. chests.Select(chest => chest.Id)], [.. contracts.Select(contract => contract.Id)]));
        world.AddBox("wf_trg_transaction_complete", _ => G1World.Chest("wf_trg_transaction_complete", [], [], [100], [], [], [], []), 1, 0);
        var completed = world.AddWitness(1, 0);
        world.AddBox("wf_trg_transaction_fail", _ => G1World.Chest("wf_trg_transaction_fail", [], [], [100], [], [], failSelectsContracts ? [.. contracts.Select(contract => contract.Id)] : [], []), 1, 1);
        var failed = world.AddWitness(1, 1);

        return new() { World = world, Player = player, Completed = completed, Failed = failed };
    }

    [Fact]
    public void APaymentContractOpensTheTradeThenTakesTheOfferedFurniAndRunsTheCompleteStack()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays(2));
        var stacks = Initiate(world, player, [chest], [contract]);
        var one = player.Give(G1World.Furni(201));
        var two = player.Give(G1World.Furni(202));
        var spare = player.Give(G1World.Furni(203));

        world.Fire(Enter(player));
        Assert.True(player.User.IsTrading);
        Assert.Equal(1, player.Sent(ServerPacketHeader.WiredChestTradeOpenComposer));
        Assert.Empty(stacks.Completed.Runs);
        Assert.Empty(stacks.Failed.Runs);
        Assert.Empty(world.Store.Requests);

        world.Module.Offer(player.Client, [one.Id, two.Id], true);
        world.Module.Confirm(player.Client, false);
        world.Module.Confirm(player.Client, true);
        world.Module.Confirm(player.Client, true);
        world.Flush();

        Assert.Single(world.Store.Requests);
        Assert.False(player.User.IsTrading);
        Assert.False(player.Owns(one.Id));
        Assert.False(player.Owns(two.Id));
        Assert.True(player.Owns(spare.Id));
        Assert.Equal([201u, 202u], world.Store.Chests[chest.Id].Items.Select(item => item.Id).Order().ToArray());
        var run = Assert.Single(stacks.Completed.Runs);
        Assert.Same(player.User, run.Actor);
        Assert.Equal(new WiredChestFigures(1, 2, 0, 0, 0), run.Transaction);
        Assert.Equal(contract.Id, run.SourceId);
        Assert.Empty(stacks.Failed.Runs);
    }

    private static WiredChestContract Trades(int pay, int coins) => new()
    {
        Kind = WiredContractKind.Trade,
        PaymentMode = 1,
        Payment = [[new(pay, new(false, 44))]],
        Reward = [new(coins)]
    };

    [Fact]
    public void ARewardContractPaysAtOnceWithoutOpeningATradeAndRunsTheCompleteStack()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Coins, 50);
        var contract = world.AddContract(WiredContractKind.Reward, Gives(20));
        var stacks = Initiate(world, player, [chest], [contract]);

        world.Fire(Enter(player));

        Assert.Equal(120, player.Credits);
        Assert.Equal(30, world.Store.Chests[chest.Id].Coins);
        Assert.False(player.User.IsTrading);
        Assert.Equal(0, player.Sent(ServerPacketHeader.WiredChestTradeOpenComposer));
        Assert.Equal(1, player.Sent(ServerPacketHeader.WiredChestRewardComposer));
        Assert.Equal(new WiredChestFigures(1, 0, 0, 0, 20), Assert.Single(stacks.Completed.Runs).Transaction);
        Assert.Empty(stacks.Failed.Runs);
    }

    [Fact]
    public void ARewardTheChestsCannotCoverFailsWithFundsGoneAndMovesNothing()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Coins, 50);
        var contract = world.AddContract(WiredContractKind.Reward, Gives(80));
        var stacks = Initiate(world, player, [chest], [contract]);

        world.Fire(Enter(player));

        Assert.Equal(100, player.Credits);
        Assert.Equal(50, world.Store.Chests[chest.Id].Coins);
        Assert.Equal((int)WiredChestFailure.FundsGone, Assert.Single(stacks.Failed.Runs).Code);
        Assert.Empty(stacks.Completed.Runs);
        Assert.Equal(1, player.Sent(ServerPacketHeader.WiredChestTradeCancelledComposer));
    }

    [Fact]
    public void RewardsAreMultipliedByTheTransactionMultiplierAndOnlyInTheMultiplierModes()
    {
        foreach (var (ints, expected) in new[] { (new[] { 0, 7, 0, 0, 0, 300 }, 120), (new[] { 1, 7, 0, 0, 0, 300 }, 240), (new[] { 2, 7, 0, 0, 0, 300 }, 240) }) {
            using var world = new G1World();
            var player = world.AddPlayer();
            var chest = world.AddChest(WiredChestKind.Coins, 1000);
            var contract = world.AddContract(WiredContractKind.Reward, Gives(20));
            var stacks = Initiate(world, player, [chest], [contract], ints);

            world.Fire(Enter(player));

            Assert.Equal(expected, player.Credits);
            Assert.Equal(ints[0] == 0 ? 1 : 7, Assert.Single(stacks.Completed.Runs).Transaction!.Multiplier);
        }
    }

    [Fact]
    public void ARewardIsNotHeldUpByTheUsersOpenTrade()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var furni = world.AddChest(WiredChestKind.Furni);
        var coins = world.AddChest(WiredChestKind.Coins, 50);
        var payment = world.AddContract(WiredContractKind.Payment, Pays());
        var reward = world.AddContract(WiredContractKind.Reward, Gives(20));
        var stacks = Initiate(world, player, [furni, coins], [payment]);
        world.Fire(Enter(player));
        Assert.True(player.User.IsTrading);

        // Turbo pays a reward whatever the user is doing; only payments and trades need the single trade window.
        Assert.True(world.Module.Start(player.Client, reward.Id, [coins], world.Store.LoadContract(reward)));
        world.Flush();

        Assert.Equal(120, player.Credits);
        Assert.True(player.User.IsTrading);
        Assert.Empty(stacks.Failed.Runs);
        Assert.Equal(reward.Id, Assert.Single(stacks.Completed.Runs).SourceId);
    }

    [Fact]
    public void ATradeRunsTheMultiplierAndTakesAndGivesExactlyThatManyTimes()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var furni = world.AddChest(WiredChestKind.Furni);
        var coins = world.AddChest(WiredChestKind.Coins, 100);
        var contract = world.AddContract(WiredContractKind.Trade, Trades(2, 5));
        var stacks = Initiate(world, player, [furni, coins], [contract], [1, 3, 0, 0, 0, 300]);
        var offered = Enumerable.Range(201, 7).Select(id => player.Give(G1World.Furni((uint)id)).Id).ToArray();

        world.Fire(Enter(player));
        // The multiplier needs five of the seven twice over: six furni exactly, so five are not enough to accept.
        world.Module.Offer(player.Client, offered.Take(5).ToArray(), true);
        world.Module.Confirm(player.Client, false);
        world.Module.Confirm(player.Client, true);
        Assert.Empty(world.Store.Requests);
        Assert.True(player.User.IsTrading);

        world.Module.Offer(player.Client, [offered[5]], true);
        world.Module.Confirm(player.Client, false);
        world.Module.Confirm(player.Client, true);
        world.Flush();

        Assert.Equal(115, player.Credits);
        Assert.Equal(85, world.Store.Chests[coins.Id].Coins);
        Assert.Equal(6, world.Store.Chests[furni.Id].Items.Count);
        Assert.True(player.Owns(offered[6]));
        Assert.Equal(new WiredChestFigures(3, 6, 0, 0, 15), Assert.Single(stacks.Completed.Runs).Transaction);
    }

    [Fact]
    public void AnAutoMultiplierTakesAsManyTimesAsTheOfferCoversUpToTheLimit()
    {
        foreach (var (limit, times) in new[] { (5, 3), (2, 2) }) {
            using var world = new G1World();
            var player = world.AddPlayer();
            var furni = world.AddChest(WiredChestKind.Furni);
            var coins = world.AddChest(WiredChestKind.Coins, 100);
            var contract = world.AddContract(WiredContractKind.Trade, Trades(2, 5));
            var stacks = Initiate(world, player, [furni, coins], [contract], [2, limit, 0, 0, 0, 300]);
            var offered = Enumerable.Range(201, 7).Select(id => player.Give(G1World.Furni((uint)id)).Id).ToArray();

            world.Fire(Enter(player));
            world.Module.Offer(player.Client, offered, true);
            world.Module.Confirm(player.Client, false);
            world.Module.Confirm(player.Client, true);
            world.Flush();

            Assert.Equal(100 + 5 * times, player.Credits);
            Assert.Equal(2 * times, world.Store.Chests[furni.Id].Items.Count);
            Assert.Equal(7 - 2 * times, offered.Count(player.Owns));
            Assert.Equal(new WiredChestFigures(times, 2 * times, 0, 0, 5 * times), Assert.Single(stacks.Completed.Runs).Transaction);
        }
    }

    [Fact]
    public void ATradeWhoseRewardIsGoneAtConfirmationKeepsThePaymentAndFailsWithFundsGone()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var furni = world.AddChest(WiredChestKind.Furni);
        var coins = world.AddChest(WiredChestKind.Coins, 5);
        var contract = world.AddContract(WiredContractKind.Trade, Trades(1, 5));
        var stacks = Initiate(world, player, [furni, coins], [contract]);
        var item = player.Give(G1World.Furni(201));
        world.Fire(Enter(player));
        world.Module.Offer(player.Client, [item.Id], true);
        world.Module.Confirm(player.Client, false);
        // Someone else empties the coin chest while the window is open.
        world.Store.Chests[coins.Id].Coins = 0;
        world.Module.Confirm(player.Client, true);
        world.Flush();

        Assert.True(player.Owns(item.Id));
        Assert.Empty(world.Store.Chests[furni.Id].Items);
        Assert.Equal(100, player.Credits);
        Assert.Equal((int)WiredChestFailure.FundsGone, Assert.Single(stacks.Failed.Runs).Code);
        Assert.False(player.User.IsTrading);
        Assert.Empty(stacks.Completed.Runs);
    }

    [Fact]
    public void ACommitThatFailsRollsEverythingBackAndReportsTheDatabaseError()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var furni = world.AddChest(WiredChestKind.Furni);
        var coins = world.AddChest(WiredChestKind.Coins, 50);
        var contract = world.AddContract(WiredContractKind.Trade, Trades(1, 5));
        var stacks = Initiate(world, player, [furni, coins], [contract]);
        var item = player.Give(G1World.Furni(201));
        world.Fire(Enter(player));
        world.Module.Offer(player.Client, [item.Id], true);
        world.Module.Confirm(player.Client, false);
        world.Store.FailNextCommit = true;
        world.Module.Confirm(player.Client, true);
        world.Flush();

        Assert.True(player.Owns(item.Id));
        Assert.Empty(world.Store.Chests[furni.Id].Items);
        Assert.Equal(100, player.Credits);
        Assert.Equal(50, world.Store.Chests[coins.Id].Coins);
        Assert.Equal((int)WiredChestFailure.DatabaseError, Assert.Single(stacks.Failed.Runs).Code);
        Assert.Equal(0, world.Store.Commits);

        // The item was released by the failed attempt: it is not reserved and can be offered in a new transaction.
        world.Fire(Enter(player));
        world.Module.Offer(player.Client, [item.Id], true);
        world.Module.Confirm(player.Client, false);
        world.Module.Confirm(player.Client, true);
        world.Flush();
        Assert.False(player.Owns(item.Id));
        Assert.Equal(105, player.Credits);
        Assert.Single(stacks.Completed.Runs);
    }

    [Fact]
    public void DoubleConfirmationAndRepeatedFiringNeverMoveAnythingTwice()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var furni = world.AddChest(WiredChestKind.Furni);
        var coins = world.AddChest(WiredChestKind.Coins, 50);
        var contract = world.AddContract(WiredContractKind.Trade, Trades(1, 5));
        var stacks = Initiate(world, player, [furni, coins], [contract]);
        var item = player.Give(G1World.Furni(201));

        world.Fire(Enter(player));
        world.Fire(Enter(player));
        Assert.Equal((int)WiredChestFailure.AlreadyTrading, Assert.Single(stacks.Failed.Runs).Code);
        Assert.Equal(1, player.Sent(ServerPacketHeader.WiredChestTradeOpenComposer));

        world.Module.Offer(player.Client, [item.Id], true);
        world.Module.Confirm(player.Client, false);
        world.Module.Confirm(player.Client, true);
        world.Module.Confirm(player.Client, true);
        world.Module.Confirm(player.Client, true);
        world.Flush();

        Assert.Single(world.Store.Requests);
        Assert.Equal(105, player.Credits);
        Assert.Single(world.Store.Chests[furni.Id].Items);
        Assert.Single(stacks.Completed.Runs);
    }

    [Fact]
    public void AnOrdinaryUserTradeBlocksTheContractAndTheOpenWindowIsUntouched()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [chest], [contract]);
        player.User.IsTrading = true;

        world.Fire(Enter(player));

        Assert.Equal((int)WiredChestFailure.AlreadyTrading, Assert.Single(stacks.Failed.Runs).Code);
        Assert.True(player.User.IsTrading);
        Assert.Equal(0, player.Sent(ServerPacketHeader.WiredChestTradeOpenComposer));
        Assert.False(world.Module.HasPending);
    }

    [Fact]
    public void TheTradeTimesOutAfterTheConfiguredSecondsAndPaysNothing()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [chest], [contract], [0, 1, 0, 0, 1, 30]);
        var item = player.Give(G1World.Furni(201));
        world.Fire(Enter(player));
        world.Module.Offer(player.Client, [item.Id], true);
        world.Module.Confirm(player.Client, false);

        world.Advance(TimeSpan.FromSeconds(29));
        Assert.True(player.User.IsTrading);
        Assert.Empty(stacks.Failed.Runs);

        world.Advance(TimeSpan.FromSeconds(1));
        Assert.False(player.User.IsTrading);
        Assert.Equal((int)WiredChestFailure.Timeout, Assert.Single(stacks.Failed.Runs).Code);
        Assert.True(player.Owns(item.Id));
        Assert.Empty(world.Store.Chests[chest.Id].Items);

        // A confirmation arriving after the deadline finds no trade.
        world.Module.Confirm(player.Client, true);
        Assert.Empty(world.Store.Requests);
    }

    [Fact]
    public void WithoutATimeoutTheTradeStaysOpen()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [chest], [contract], [0, 1, 0, 0, 0, 30]);
        world.Fire(Enter(player));

        world.Advance(TimeSpan.FromHours(2));

        Assert.True(player.User.IsTrading);
        Assert.Empty(stacks.Failed.Runs);
    }

    [Fact]
    public void NoContractOrSeveralContractsAreAMisconfigurationAndOpenNoTrade()
    {
        foreach (var contracts in new[] { 0, 2 }) {
            using var world = new G1World();
            var player = world.AddPlayer();
            var chest = world.AddChest(WiredChestKind.Furni);
            var picked = Enumerable.Range(0, contracts).Select(i => world.AddContract(WiredContractKind.Payment, Pays(), 3 + i, 1)).ToArray();
            var stacks = Initiate(world, player, [chest], picked);

            world.Fire(Enter(player));

            Assert.Equal((int)WiredChestFailure.Misconfig, Assert.Single(stacks.Failed.Runs).Code);
            Assert.False(player.User.IsTrading);
            Assert.False(world.Module.HasPending);
        }
    }

    [Fact]
    public void ChestsThatAreLockedOrGoneFailWithNoOrLockedChestsButAUsableOneIsEnough()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var usable = world.AddChest(WiredChestKind.Furni);
        var locked = world.AddChest(WiredChestKind.Furni, 0, 3, 3);
        var gone = world.AddChest(WiredChestKind.Furni, 0, 4, 4);
        world.Store.Chests[locked.Id].Settings = world.Store.Chests[locked.Id].Settings with { Locked = true };
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [usable, locked, gone], [contract]);
        var item = player.Give(G1World.Furni(201));
        world.Floor.TryRemove(gone.Id, out _);

        // The locked and the vanished chest are ignored and the payment goes to the usable one.
        world.Fire(Enter(player));
        Assert.True(player.User.IsTrading);
        world.Module.Offer(player.Client, [item.Id], true);
        world.Module.Confirm(player.Client, false);
        world.Module.Confirm(player.Client, true);
        world.Flush();
        Assert.Equal([usable.Id], world.Store.Requests.Single().ChestIds);
        Assert.Single(world.Store.Chests[usable.Id].Items);
        Assert.Empty(world.Store.Chests[locked.Id].Items);

        // With the one usable chest locked too, nothing is left to pay into.
        world.Store.Chests[usable.Id].Settings = world.Store.Chests[usable.Id].Settings with { Locked = true };
        var second = player.Give(G1World.Furni(202));
        world.Fire(Enter(player));
        Assert.False(player.User.IsTrading);
        Assert.Equal((int)WiredChestFailure.NoOrLockedChests, Assert.Single(stacks.Failed.Runs).Code);
        Assert.True(player.Owns(second.Id));
    }

    [Fact]
    public void AChestLockedWhileTheWindowIsOpenFailsTheConfirmationWithoutPaying()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [chest], [contract]);
        var item = player.Give(G1World.Furni(201));
        world.Fire(Enter(player));
        world.Module.Offer(player.Client, [item.Id], true);
        world.Module.Confirm(player.Client, false);
        world.Store.Chests[chest.Id].Settings = world.Store.Chests[chest.Id].Settings with { WiredEnabled = false };

        world.Module.Confirm(player.Client, true);
        world.Flush();

        Assert.True(player.Owns(item.Id));
        Assert.Empty(world.Store.Requests);
        Assert.Equal((int)WiredChestFailure.NoOrLockedChests, Assert.Single(stacks.Failed.Runs).Code);
        Assert.False(player.User.IsTrading);
    }

    [Fact]
    public void ABoxWithNoUserToActOnDoesNothing()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Coins, 50);
        var contract = world.AddContract(WiredContractKind.Reward, Gives(20));
        var stacks = Initiate(world, player, [chest], [contract]);
        var stranger = new HabboHotel.Rooms.RoomUser(77, (int)G1World.RoomId, 70, world.Room, null, TestChatEmotions.Unused, TestRewardProgress.Unused);

        world.Fire(new WiredRuntimeEvent(WiredEventKind.Enter) { Actor = stranger });

        Assert.Equal(100, player.Credits);
        Assert.Empty(world.Published);
        Assert.Empty(stacks.Completed.Runs);
        Assert.Empty(stacks.Failed.Runs);
    }

    [Fact]
    public void TwoUsersTransactIndependentlyOnTheSameBox()
    {
        using var world = new G1World();
        var first = world.AddPlayer(1);
        var second = world.AddPlayer(2);
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, first, [chest], [contract]);
        var a = first.Give(G1World.Furni(201));
        var b = second.Give(G1World.Furni(301));

        world.Fire(Enter(first));
        world.Fire(Enter(second));
        Assert.True(first.User.IsTrading);
        Assert.True(second.User.IsTrading);

        world.Module.Offer(second.Client, [a.Id], true);
        Assert.True(first.Owns(a.Id));
        world.Module.Offer(first.Client, [a.Id], true);
        world.Module.Offer(second.Client, [b.Id], true);

        foreach (var player in new[] { first, second }) {
            world.Module.Confirm(player.Client, false);
            world.Module.Confirm(player.Client, true);
        }

        world.Flush();
        Assert.False(first.Owns(a.Id));
        Assert.False(second.Owns(b.Id));
        Assert.Equal(2, world.Store.Chests[chest.Id].Items.Count);
        Assert.Equal([first.User, second.User], stacks.Completed.Runs.Select(run => run.Actor).ToArray());
    }

    [Fact]
    public void ALeavingUserEndsTheirTradeAsCancelledAndKeepsTheirFurni()
    {
        using var world = new G1World();
        var player = world.AddPlayer();
        var chest = world.AddChest(WiredChestKind.Furni);
        var contract = world.AddContract(WiredContractKind.Payment, Pays());
        var stacks = Initiate(world, player, [chest], [contract]);
        var item = player.Give(G1World.Furni(201));
        world.Fire(Enter(player));
        world.Module.Offer(player.Client, [item.Id], true);

        world.Module.Leave(player.User);
        world.Flush();

        Assert.Equal((int)WiredChestFailure.TradeCancelled, Assert.Single(stacks.Failed.Runs).Code);
        Assert.True(player.Owns(item.Id));
        Assert.Empty(world.Store.Requests);
    }
}
