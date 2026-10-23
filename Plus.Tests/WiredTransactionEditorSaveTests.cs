using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Raw saves of the transaction boxes through the real save packet, read back the way the editor reopens them.
public sealed class WiredTransactionEditorSaveTests
{
    private const string VariableCount = "wiredfurni.error.invalid_variable_count";
    private const string InvalidFurniSource = "wiredfurni.error.invalid_furni_source";
    private const string RequireContracts = "wiredfurni.error.require_contracts";
    private const string RequireContractsOrTradeWired = "wiredfurni.error.require_contracts_or_trade_wired";

    // Cancel Transaction: the match criteria saves below were sent in this order to one box, each read back after its save.
    [Fact]
    public void CancelTransactionStoresAnyMatchCriteriaAsZeroOrOne()
    {
        using var save = new WiredRawSave("wf_act_cancel_transaction");
        (int[] Sent, int[] Stored)[] steps =
        [
            ([0], [0]), ([1], [1]), ([2], [1]), ([3], [1]), ([4], [1]), ([5], [1]), ([-1], [1]), ([100], [1]), ([int.MaxValue], [1]),
            ([1, 2], [1]), ([1, 2, 3], [1]), ([], [0])
        ];

        foreach (var (sent, stored) in steps) {
            var result = save.Save(ints: sent);

            Assert.True(result.Accepted, $"[{string.Join(',', sent)}]: {result}");
            Assert.Equal(stored, result.Ints);
        }
    }

    [Fact]
    public void CancelTransactionDropsTheText()
    {
        using var save = new WiredRawSave("wf_act_cancel_transaction");

        var result = save.Save(ints: [0], text: "abc");

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal("", result.Text);
    }

    [Fact]
    public void CancelTransactionRefusesAPickThatIsNoContract()
    {
        using var save = new WiredRawSave("wf_act_cancel_transaction");
        var first = save.Furni();
        var second = save.Furni();

        Assert.Equal(("error", RequireContracts), Answer(save.Save(furni: [100], picks: [first])));
        Assert.Equal(("error", RequireContracts), Answer(save.Save(furni: [100], picks: [first, second])));
        Assert.Equal(("error", RequireContracts), Answer(save.Save(furni: [100], picks: [first], secondary: [second])));
        Assert.True(save.Save(picks: []).Accepted);
    }

    [Fact]
    public void CancelTransactionAcceptsAContractPick()
    {
        using var save = new WiredRawSave("wf_act_cancel_transaction");

        var result = save.Save(furni: [100], picks: [save.Furni("wired_contract_trade", InteractionType.WiredContractTrade)]);

        Assert.True(result.Accepted, result.ToString());
    }

    [Theory]
    [InlineData("wf_act_cancel_transaction")]
    [InlineData("wf_trg_transaction_complete")]
    [InlineData("wf_trg_transaction_fail")]
    public void ATransactionBoxRefusesAVariableWithTheCountKey(string name)
    {
        using var save = new WiredRawSave(name);

        var result = save.Save(variables: ["user:5"]);

        Assert.Equal(("error", VariableCount), Answer(result));
    }

    [Fact]
    public void TransactionCompletedOffersThePicksAndTheSelector()
    {
        Assert.Equal([[100, 200]], WiredNativeEditorProjection.Metadata("wf_trg_transaction_complete").FurniAllowed.Select(slot => slot.ToArray()));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public void TransactionCompletedStoresAnOfferedFurniSource(int source)
    {
        using var save = new WiredRawSave("wf_trg_transaction_complete");

        var result = save.Save(furni: [source]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([source], result.Furni);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(201)]
    [InlineData(901)]
    public void TransactionCompletedRefusesAnotherFurniSource(int source)
    {
        using var save = new WiredRawSave("wf_trg_transaction_complete");

        Assert.Equal(("error", InvalidFurniSource), Answer(save.Save(furni: [source])));
    }

    [Fact]
    public void TransactionCompletedDropsIntsAndText()
    {
        using var save = new WiredRawSave("wf_trg_transaction_complete");

        var ints = save.Save(ints: [1, 2]);
        var text = save.Save(ints: [], text: "abc");

        Assert.True(ints.Accepted, ints.ToString());
        Assert.Empty(ints.Ints);
        Assert.True(text.Accepted, text.ToString());
        Assert.Equal("", text.Text);
    }

    [Fact]
    public void TransactionCompletedRefusesAPickThatIsNoContractOrTradeWired()
    {
        using var save = new WiredRawSave("wf_trg_transaction_complete");
        var first = save.Furni();
        var second = save.Furni();

        Assert.Equal(("error", RequireContractsOrTradeWired), Answer(save.Save(furni: [100], picks: [first])));
        Assert.Equal(("error", RequireContractsOrTradeWired), Answer(save.Save(furni: [100], picks: [first, second])));
        Assert.Equal(("error", RequireContractsOrTradeWired), Answer(save.Save(furni: [100], picks: [first], secondary: [second])));
    }

    // Initiate Transaction: the four saves were sent in this order; the editor then opened with the timeout of 29 seconds as 30.
    [Fact]
    public void InitiateTransactionStoresATimeoutBelowThirtySecondsAsThirty()
    {
        using var save = new WiredRawSave("wf_act_init_transaction");
        var chest = save.FurniChest();
        int[][] sent = [[0, 10, 0, -20, 0, 300], [0, 2, 0, -20, 1, 30], [0, 1, 0, -20, 1, 30], [0, 2, 0, -20, 1, 29]];

        foreach (var ints in sent) {
            var result = save.Save(ints: ints, furni: [100, 101, 200], users: [0, 200], picks: [chest]);

            Assert.True(result.Accepted, $"[{string.Join(',', ints)}]: {result}");
        }

        Assert.Equal([0, 2, 0, -20, 1, 30], save.Stored().Ints);
    }

    // Give Furni: the saves were sent in this order; the editor then opened with the amount 0 as 1. The chest pick is left out here, as it
    // does not change the stored ints.
    [Fact]
    public void GiveFurniStoresAnAmountOfZeroAsOne()
    {
        using var save = new WiredRawSave("wf_act_give_furni");

        var first = save.Save(ints: [0, 1, 0, -20, 0, 1], furni: [100, 200], users: [0, 200]);
        Assert.True(first.Accepted, first.ToString());
        var result = save.Save(ints: [0, 0, 0, -20, 0, 1], furni: [100, 200], users: [0, 200]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([0, 1, 0, -20, 0, 1], save.Stored().Ints);
    }

    // Initiate Transaction: each timeout was saved on a fresh box with the timeout switch on, then the editor was opened again.
    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 30)]
    [InlineData(15, 30)]
    [InlineData(29, 30)]
    [InlineData(30, 30)]
    [InlineData(31, 31)]
    [InlineData(3600, 3600)]
    [InlineData(3601, 3600)]
    [InlineData(int.MaxValue, 3600)]
    [InlineData(-1, 30)]
    public void InitiateTransactionClampsTheTimeoutBetweenThirtySecondsAndOneHour(int sent, int stored)
    {
        using var save = new WiredRawSave("wf_act_init_transaction");

        var result = save.Save(ints: [0, 10, 0, -20, 1, sent], furni: [100, 101, 200], users: [0, 200], picks: [save.FurniChest()]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([0, 10, 0, -20, 1, stored], save.Stored().Ints);
    }

    [Fact]
    public void InitiateTransactionClampsTheTimeoutWhenTheTimeoutIsOff()
    {
        using var save = new WiredRawSave("wf_act_init_transaction");

        var result = save.Save(ints: [0, 10, 0, -20, 0, 29], furni: [100, 101, 200], users: [0, 200], picks: [save.FurniChest()]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([0, 10, 0, -20, 0, 30], save.Stored().Ints);
    }

    [Fact]
    public void InitiateTransactionClampsATimeoutSavedOverAnotherOne()
    {
        using var save = new WiredRawSave("wf_act_init_transaction");
        var chest = save.FurniChest();

        Assert.True(save.Save(ints: [0, 10, 0, -20, 1, 100], furni: [100, 101, 200], users: [0, 200], picks: [chest]).Accepted);
        Assert.Equal([0, 10, 0, -20, 1, 100], save.Stored().Ints);
        Assert.True(save.Save(ints: [0, 10, 0, -20, 1, 29], furni: [100, 101, 200], users: [0, 200], picks: [chest]).Accepted);
        Assert.Equal([0, 10, 0, -20, 1, 30], save.Stored().Ints);
    }

    // The three boxes that pick transactions accept the three contracts and the Custom Contract add-on, and refuse the Initiate
    // Transaction box, other furni and any mix with one of them. A refused save keeps the previous picks.
    [Theory]
    [InlineData("wf_act_cancel_transaction", RequireContracts)]
    [InlineData("wf_trg_transaction_complete", RequireContractsOrTradeWired)]
    [InlineData("wf_trg_transaction_fail", RequireContractsOrTradeWired)]
    public void ATransactionBoxPicksContractsAndTheCustomContract(string name, string key)
    {
        using var save = new WiredRawSave(name);
        var colortile = save.Furni("wf_colortile", InteractionType.None);
        var trade = save.Furni("wired_contract_trade", InteractionType.WiredContractTrade);
        var payment = save.Furni("wired_contract_payment", InteractionType.WiredContractPayment);
        var reward = save.Furni("wired_contract_reward", InteractionType.WiredContractReward);
        var tradeWired = save.Furni("wf_act_init_transaction", InteractionType.WiredEffect);
        var custom = save.Furni("wf_xtra_custom_contract", InteractionType.WiredAddon);
        (uint[] Picks, bool Accepted)[] steps =
        [
            ([colortile], false), ([trade], true), ([payment], true), ([reward], true), ([tradeWired], false), ([custom], true),
            ([trade, tradeWired], false), ([trade, colortile], false)
        ];
        uint[] kept = [];

        foreach (var (picks, accepted) in steps) {
            var result = save.Save(furni: [100], picks: picks);
            var label = $"{name} [{string.Join(',', picks)}]";

            if (accepted) {
                Assert.True(result.Accepted, $"{label}: {result}");
                kept = picks;
            }
            else {
                Assert.Equal(("error", key), Answer(result));
            }

            Assert.Equal(kept, save.Picks());
        }
    }

    // Give Currency and Give Furni: each list of amounts was saved in this order on a fresh box, and the editor opened again after each.
    [Theory]
    [InlineData("wf_act_give_currency", 11, new[] { 0 }, new[] { 1 })]
    [InlineData("wf_act_give_currency", 11, new[] { 5, 0 }, new[] { 5, 1 })]
    [InlineData("wf_act_give_currency", 11, new[] { 5 }, new[] { 5 })]
    [InlineData("wf_act_give_currency", 11, new[] { -1 }, new[] { 1 })]
    [InlineData("wf_act_give_furni", 1, new[] { 0 }, new[] { 1 })]
    [InlineData("wf_act_give_furni", 1, new[] { 5, 0 }, new[] { 5, 1 })]
    [InlineData("wf_act_give_furni", 1, new[] { 5 }, new[] { 5 })]
    [InlineData("wf_act_give_furni", 1, new[] { -1 }, new[] { 1 })]
    public void AGiveBoxStoresAnAmountBelowOneAsOne(string name, int last, int[] sent, int[] stored)
    {
        using var save = new WiredRawSave(name);
        var chest = name == "wf_act_give_currency" ? save.CoinChest() : save.FurniChest();

        for (var index = 0; index < sent.Length; index++) {
            var result = save.Save(ints: [0, sent[index], 0, -20, 0, last], furni: [100, 200], users: [0, 200], picks: [chest]);

            Assert.True(result.Accepted, $"{sent[index]}: {result}");
            Assert.Equal([0, stored[index], 0, -20, 0, last], save.Stored().Ints);
        }
    }

    private static (string Answer, string? Key) Answer(WiredRawSave.Result result) => (result.Answer, result.Key);
}
