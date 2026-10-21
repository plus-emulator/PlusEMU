using Xunit;

namespace Plus.Tests;

// Raw saves of the boxes below, sent through the real save packet and read back the way the editor reopens them. Observed on AIR
// WIN63-202610201032-107964540.
public sealed class WiredR106EditorSaveTests
{
    private const string Invalid = "wiredfurni.error.invalid_user_source";

    private static WiredRawSave.Result Save(string name, int[]? ints = null, string text = "", int[]? furni = null, int[]? users = null,
        int quantifier = 0, uint[]? picks = null, uint[]? secondary = null)
    {
        using var save = new WiredRawSave(name);
        var result = save.Save(ints, text, furni, users, quantifier, picks: picks, secondary: secondary);

        return result;
    }

    // User Count In: each number is held to 0..125, the maximum is raised to the minimum, a missing number takes the frame value and
    // an extra number is dropped. The box has no text. A quantifier of 0, 1 or 2 is kept and any other value is stored as 0.
    [Theory]
    [InlineData(new[] { 126, 125 }, new[] { 125, 125 })]
    [InlineData(new[] { 1000, 125 }, new[] { 125, 125 })]
    [InlineData(new[] { int.MaxValue, 125 }, new[] { 125, 125 })]
    [InlineData(new[] { -1, 125 }, new[] { 0, 125 })]
    [InlineData(new[] { int.MinValue, 125 }, new[] { 0, 125 })]
    [InlineData(new[] { 1, 0 }, new[] { 1, 1 })]
    [InlineData(new[] { 1, 126 }, new[] { 1, 125 })]
    [InlineData(new[] { 1, int.MaxValue }, new[] { 1, 125 })]
    [InlineData(new[] { 1, -1 }, new[] { 1, 1 })]
    [InlineData(new[] { 1, int.MinValue }, new[] { 1, 1 })]
    [InlineData(new[] { 10, 5 }, new[] { 10, 10 })]
    [InlineData(new[] { 126, 5 }, new[] { 125, 125 })]
    [InlineData(new[] { 5, -1 }, new[] { 5, 5 })]
    [InlineData(new[] { 50, 1 }, new[] { 50, 50 })]
    [InlineData(new int[0], new[] { 1, 50 })]
    [InlineData(new[] { 5 }, new[] { 5, 50 })]
    [InlineData(new[] { 1, 2, 3 }, new[] { 1, 2 })]
    public void UserCountInStoresTheNumbersInTheirNormalForm(int[] sent, int[] stored)
    {
        var result = Save("wf_cnd_user_count_in", sent);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(stored, result.Ints);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(-1, 0)]
    [InlineData(5, 0)]
    public void UserCountInKeepsAQuantifierOfZeroOneOrTwo(int sent, int stored)
    {
        var result = Save("wf_cnd_user_count_in", [1, 2], quantifier: sent);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(stored, result.Quantifier ?? 0);
    }

    [Fact]
    public void UserCountInDropsATextItDoesNotRead()
    {
        var result = Save("wf_cnd_user_count_in", [1, 50], "abc");

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal("", result.Text);
        Assert.Equal([1, 50], result.Ints);
    }

    // Triggerer Matches and its negation: the user type 0 and every negative value are stored as 1, 1 to 4 are kept and every value from
    // 5 up is stored as 4.
    [Theory]
    [InlineData("wf_cnd_triggerer_match")]
    [InlineData("wf_cnd_not_triggerer_match")]
    public void TriggererMatchStoresTheUserTypeInItsNormalForm(string name)
    {
        foreach (var (sent, stored) in new[] { (0, 1), (1, 1), (2, 2), (3, 3), (4, 4), (5, 4), (6, 4), (8, 4), (16, 4), (100, 4), (int.MaxValue, 4),
            (-1, 1), (int.MinValue, 1) }) {
            var result = Save(name, [sent], "Robbie");

            Assert.True(result.Accepted, $"{name} {sent}: {result}");
            Assert.Equal([stored], result.Ints);
        }
    }

    [Theory]
    [InlineData("wf_cnd_triggerer_match")]
    [InlineData("wf_cnd_not_triggerer_match")]
    public void TriggererMatchTakesTheFrameTypeForMissingNumbersAndDropsExtraOnes(string name)
    {
        Assert.Equal([1], Save(name, [], "x").Ints);
        Assert.Equal([1], Save(name, [1, 2], "x").Ints);
    }

    [Theory]
    [InlineData("wf_cnd_triggerer_match")]
    [InlineData("wf_cnd_not_triggerer_match")]
    public void TriggererMatchKeepsANameOfAnyLengthAndSpacesTabsAndLineFeeds(string name)
    {
        var long100 = new string('a', 100);

        Assert.Equal(long100, Save(name, [1], long100).Text);
        Assert.Equal("a b", Save(name, [1], "a\tb").Text);
        Assert.Equal("a b", Save(name, [1], "a\nb").Text);
        Assert.Equal("Objecto ", Save(name, [1], "Objecto ").Text);
        Assert.Equal(" ", Save(name, [1], " ").Text);
        Assert.Equal("", Save(name, [1], "").Text);
    }

    [Theory]
    [InlineData("wf_cnd_triggerer_match")]
    [InlineData("wf_cnd_not_triggerer_match")]
    public void TriggererMatchKeepsAQuantifierOfZeroOneOrTwo(string name)
    {
        foreach (var (sent, stored) in new[] { (0, 0), (1, 1), (2, 2), (-1, 0), (5, 0) }) {
            var result = Save(name, [1], "a,b", quantifier: sent);

            Assert.True(result.Accepted, $"{name} {sent}: {result}");
            Assert.Equal(stored, result.Quantifier);
        }
    }

    // A save that names one user source, or none, keeps the frame value for the slots it leaves out.
    [Theory]
    [InlineData("wf_cnd_triggerer_match")]
    [InlineData("wf_cnd_not_triggerer_match")]
    public void TriggererMatchFillsTheUserSourceSlotsASaveLeavesOut(string name)
    {
        foreach (var (sent, stored) in new[] { (new[] { 0 }, new[] { 0, 101 }), (new[] { 200 }, new[] { 200, 101 }), (new[] { 201 }, new[] { 201, 101 }),
            (new int[0], new[] { 0, 101 }), (new[] { 0, 101 }, new[] { 0, 101 }), (new[] { 200, 201 }, new[] { 200, 201 }) }) {
            var result = Save(name, [1], "a,b", users: sent);

            Assert.True(result.Accepted, $"{name} [{string.Join(',', sent)}]: {result}");
            Assert.Equal(stored, result.Users);
        }
    }

    [Theory]
    [InlineData("wf_cnd_triggerer_match", 100)]
    [InlineData("wf_cnd_triggerer_match", 101)]
    [InlineData("wf_cnd_not_triggerer_match", 100)]
    [InlineData("wf_cnd_not_triggerer_match", 101)]
    public void TriggererMatchRefusesAFirstUserSourceItDoesNotOffer(string name, int source)
    {
        var result = Save(name, [1], "a,b", users: [source]);

        Assert.Equal("error", result.Answer);
        Assert.Equal(Invalid, result.Key);
    }

    // Teleport To: the fast flag is 1 for any value but 0, a missing flag is 0, extra numbers and a text are dropped, and an empty user
    // source list takes the frame value.
    [Theory]
    [InlineData(new[] { 0 }, 0)]
    [InlineData(new[] { 1 }, 1)]
    [InlineData(new[] { 2 }, 1)]
    [InlineData(new[] { -1 }, 1)]
    [InlineData(new[] { 100 }, 1)]
    [InlineData(new[] { int.MaxValue }, 1)]
    [InlineData(new int[0], 0)]
    [InlineData(new[] { 1, 2 }, 1)]
    public void TeleportToStoresTheFastFlagInItsNormalForm(int[] sent, int stored)
    {
        using var save = new WiredRawSave("wf_act_teleport_to");
        var result = save.Save(sent, furni: [100], users: [0], picks: [save.Furni()]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([stored], result.Ints);
    }

    [Fact]
    public void TeleportToDropsATextAndFillsAnEmptyUserSourceList()
    {
        using var save = new WiredRawSave("wf_act_teleport_to");
        var result = save.Save([0], "abc", furni: [100], users: [], picks: [save.Furni()]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal("", result.Text);
        Assert.Equal([0], result.Ints);
        Assert.Equal([0], result.Users);
    }

    // Kick User: the message is cut at 200 characters, a tab or a line feed becomes a space, numbers are dropped and an empty user source
    // list takes the frame value.
    [Theory]
    [InlineData(100, 100)]
    [InlineData(101, 101)]
    [InlineData(200, 200)]
    [InlineData(1000, 200)]
    public void KickUserKeepsAMessageOfUpTo200Characters(int sent, int stored)
    {
        var result = Save("wf_act_kick_user", [], new string('a', sent));

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(new string('a', stored), result.Text);
    }

    [Fact]
    public void KickUserStoresTabsAndLineFeedsAsSpacesAndDropsNumbers()
    {
        Assert.Equal("a b", Save("wf_act_kick_user", [], "a\tb").Text);
        Assert.Equal("a b", Save("wf_act_kick_user", [], "a\nb").Text);

        var result = Save("wf_act_kick_user", [1, 2], "x");

        Assert.True(result.Accepted, result.ToString());
        Assert.Empty(result.Ints);
    }

    [Fact]
    public void KickUserFillsAnEmptyUserSourceList()
    {
        var result = Save("wf_act_kick_user", [], "x", users: []);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([0], result.Users);
    }

    // Enter Room and Leave Room: a save with numbers is accepted and the numbers are dropped. Enter Room keeps the name filter text and
    // Leave Room stores none.
    [Theory]
    [InlineData("wf_trg_enter_room", "abc")]
    [InlineData("wf_trg_leave_room", "")]
    public void RoomTriggersAcceptNumbersAndStoreTheirText(string name, string stored)
    {
        var result = Save(name, [1, 2], "abc");

        Assert.True(result.Accepted, result.ToString());
        Assert.Empty(result.Ints);
        Assert.Equal(stored, result.Text);
    }

    // Transaction Failed: numbers and a text are dropped; the furniture source may be the picked furni (100) or the selector (200).
    [Fact]
    public void TransactionFailedAcceptsNumbersAndATextAndDropsThem()
    {
        var result = Save("wf_trg_transaction_fail", [1, 2], "abc", furni: [100]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Empty(result.Ints);
        Assert.Equal("", result.Text);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(200, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    [InlineData(201, false)]
    [InlineData(901, false)]
    public void TransactionFailedOffersThePickedFurniAndTheSelector(int source, bool accepted)
    {
        var result = Save("wf_trg_transaction_fail", furni: [source]);

        Assert.Equal(accepted, result.Accepted);
        Assert.Equal(accepted ? [source] : [100], result.Furni);

        if (!accepted) {
            Assert.Equal("wiredfurni.error.invalid_furni_source", result.Key);
        }
    }

    [Fact]
    public void TransactionFailedRefusesAUserSourceWithTheCountKey()
    {
        var result = Save("wf_trg_transaction_fail", furni: [100], users: [0]);

        Assert.Equal("wiredfurni.error.invalid_user_source_count", result.Key);
    }

    [Fact]
    public void TransactionFailedRefusesPicksThatAreNotContractsOrTradeWireds()
    {
        const string key = "wiredfurni.error.require_contracts_or_trade_wired";
        using var save = new WiredRawSave("wf_trg_transaction_fail");

        Assert.Equal(key, save.Save(furni: [100], picks: [save.Furni()]).Key);
        Assert.Equal(key, save.Save(furni: [100], picks: [save.Furni(), save.Furni()]).Key);
        Assert.Equal(key, save.Save(furni: [100], picks: [save.Furni()], secondary: [save.Furni()]).Key);
        Assert.True(save.Save(furni: [100], picks: []).Accepted);
    }

    [Fact]
    public void TransactionFailedAcceptsContractsAndTheCustomContractAsPicks()
    {
        using var save = new WiredRawSave("wf_trg_transaction_fail");

        Assert.True(save.Save(furni: [100], picks: [save.Furni("wired_contract_payment", Plus.HabboHotel.Items.InteractionType.WiredContractPayment)]).Accepted);
        Assert.True(save.Save(furni: [100], picks: [save.Furni("wired_contract_trade", Plus.HabboHotel.Items.InteractionType.WiredContractTrade)]).Accepted);
        Assert.True(save.Save(furni: [100], picks: [save.Furni("wired_contract_reward", Plus.HabboHotel.Items.InteractionType.WiredContractReward)]).Accepted);
        Assert.True(save.Save(furni: [100], picks: [save.Furni("wf_xtra_custom_contract", Plus.HabboHotel.Items.InteractionType.WiredAddon)]).Accepted);
        Assert.Equal("wiredfurni.error.require_contracts_or_trade_wired",
            save.Save(furni: [100], picks: [save.Furni("wf_act_init_transaction", Plus.HabboHotel.Items.InteractionType.None)]).Key);
    }

    // Filter Users: the amount is held to 1..1000, the operand kind to 0..1 and the reference target to the four offered values (other
    // values are stored as 0). The box has no text.
    [Theory]
    [InlineData(new[] { 0, 0, 1 }, new[] { 1, 0, 1 })]
    [InlineData(new[] { 1000, 0, 1 }, new[] { 1000, 0, 1 })]
    [InlineData(new[] { 1001, 0, 1 }, new[] { 1000, 0, 1 })]
    [InlineData(new[] { int.MaxValue, 0, 1 }, new[] { 1000, 0, 1 })]
    [InlineData(new[] { -1, 0, 1 }, new[] { 1, 0, 1 })]
    [InlineData(new[] { int.MinValue, 0, 1 }, new[] { 1, 0, 1 })]
    [InlineData(new[] { 10, 2, 1 }, new[] { 10, 1, 1 })]
    [InlineData(new[] { 10, 3, 1 }, new[] { 10, 1, 1 })]
    [InlineData(new[] { 10, 100, 1 }, new[] { 10, 1, 1 })]
    [InlineData(new[] { 10, -1, 1 }, new[] { 10, 0, 1 })]
    [InlineData(new[] { 10, 0, -10 }, new[] { 10, 0, -10 })]
    [InlineData(new[] { 10, 0, -20 }, new[] { 10, 0, -20 })]
    [InlineData(new[] { 10, 0, 0 }, new[] { 10, 0, 0 })]
    [InlineData(new[] { 10, 0, 2 }, new[] { 10, 0, 0 })]
    [InlineData(new[] { 10, 0, 99 }, new[] { 10, 0, 0 })]
    [InlineData(new[] { 10, 0, -5 }, new[] { 10, 0, 0 })]
    [InlineData(new[] { 10, 0, -1 }, new[] { 10, 0, 0 })]
    [InlineData(new int[0], new[] { 10, 0, 1 })]
    [InlineData(new[] { 5 }, new[] { 5, 0, 1 })]
    [InlineData(new[] { 5, 1 }, new[] { 5, 1, 1 })]
    [InlineData(new[] { 5, 1, -20, 9 }, new[] { 5, 1, -20 })]
    public void FilterUsersStoresTheNumbersInTheirNormalForm(int[] sent, int[] stored)
    {
        var result = Save("wf_xtra_filter_users", sent);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(stored, result.Ints);
    }

    [Fact]
    public void FilterUsersDropsATextItDoesNotRead()
    {
        var result = Save("wf_xtra_filter_users", [10, 0, 1], "abc");

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal("", result.Text);
    }
}
