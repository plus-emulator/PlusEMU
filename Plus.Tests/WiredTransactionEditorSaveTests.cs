using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Raw saves of the transaction boxes through the real save packet, read back the way the editor reopens them. Each case follows a save
// observed on AIR WIN63-202610201032-107964540.
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

    private static (string Answer, string? Key) Answer(WiredRawSave.Result result) => (result.Answer, result.Key);
}
