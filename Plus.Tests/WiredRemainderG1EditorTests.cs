using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The editor frames of the transaction cards. The two chest conditions and the scanner were observed on hhus (WIN63-202610131318, r62 batch 04,
/// run 20261013T200247); the other five are read from the October AIR client (wired_setup/actiontypes/chests, addons/chests, triggerconfs) and
/// follow the sibling frames for their sources.
/// </summary>
public sealed class WiredRemainderG1EditorTests
{
    public static TheoryData<string, int[], int[], int[], int[][], int[][], bool> ObservedFrames => new()
    {
        { "wf_cnd_chest_has_items", [0, 0, -20, 2], [100, 200], [200], [[0, 100, 200, 201], [0, 100, 200, 201]], [[0, 200, 201]], true },
        { "wf_cnd_chest_has_item_type", [0, 0, -20, 2], [100, 101, 0], [0], [[0, 100, 101, 200, 201], [0, 100, 101, 200, 201], [0, 100, 101, 200, 201]], [[0, 200, 201]], true },
        { "wf_xtra_scan_chest_furni_by_type", [0], [0, 100], [], [[0, 100, 101, 200, 201], [0, 100, 101, 200, 201]], [], true }
    };

    [Theory]
    [MemberData(nameof(ObservedFrames))]
    public void TheInitialEditorMatchesTheObservedFrame(string name, int[] ints, int[] furni, int[] users, int[][] furniAllowed, int[][] usersAllowed, bool wall)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        var metadata = WiredNativeEditorProjection.Metadata(name);
        Assert.Equal(ints, native.OwnedIntParams.ToArray());
        Assert.Equal(furni, native.FurniSourceTypes.ToArray());
        Assert.Equal(users, native.UserSourceTypes.ToArray());
        Assert.Equal("", native.Text);
        Assert.Single(native.VariableIds);
        Assert.Equal(furniAllowed, metadata.FurniAllowed.Select(slot => slot.ToArray()).ToArray());
        Assert.Equal(usersAllowed, metadata.UsersAllowed.Select(slot => slot.ToArray()).ToArray());
        Assert.Equal(wall, metadata.AllowWall);
        // Observed on AIR WIN63-202610201032-107964540: the two chest conditions open with the quantifier selector, the scanner does not.
        Assert.Equal(name.StartsWith("wf_cnd", StringComparison.Ordinal) ? 1 : 0, metadata.QuantifierType);
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out _));
    }

    [Theory]
    [InlineData("wf_act_init_transaction", 47)]
    [InlineData("wf_act_cancel_transaction", 48)]
    [InlineData("wf_cnd_chest_has_items", 45)]
    [InlineData("wf_cnd_chest_has_item_type", 46)]
    [InlineData("wf_trg_transaction_complete", 25)]
    [InlineData("wf_trg_transaction_fail", 26)]
    [InlineData("wf_xtra_scan_chest_furni_by_type", 18)]
    [InlineData("wf_xtra_custom_contract", 20)]
    public void TheEditorCodesAreTheOnesTheClientOpens(string name, int code)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        Assert.Equal(code, descriptor.EditorCode);
    }

    public static TheoryData<string, int[], int[], int[], int[][], int[][]> AirFrames => new()
    {
        // Mode, multiplier, option, target, timeout flag, timeout seconds; chests, contracts and the merged reference; the users and the merged reference.
        { "wf_act_init_transaction", [0, 10, 0, -20, 0, 300], [100, 101, 200], [0, 200], [[0, 100, 101, 200, 201], [0, 100, 101, 200, 201], [0, 100, 101, 200, 201]], [[0, 200, 201], [0, 200, 201]] },
        // Cancel for the specified contract (0) or every transaction (1); the contracts and the users.
        { "wf_act_cancel_transaction", [0], [100], [0], [[0, 100, 200, 201]], [[0, 200, 201]] },
        // Payment then reward: enabled, furni, option, amount, target; payment and reward furni and their merged references, then their users.
        { "wf_xtra_custom_contract", [0, 0, 0, 1, -20, 0, 0, 0, 1, -20], [0, 0, 200, 200], [200, 200], [[0, 100, 101, 200, 201], [0, 100, 101, 200, 201], [0, 100, 101, 200, 201], [0, 100, 101, 200, 201]], [[0, 200, 201], [0, 200, 201]] },
        // The triggers only pick the contracts they listen for.
        { "wf_trg_transaction_complete", [], [100], [], [[100]], [] },
        { "wf_trg_transaction_fail", [], [100], [], [[100]], [] }
    };

    [Theory]
    [MemberData(nameof(AirFrames))]
    public void TheInitialEditorFollowsTheAirClientAndTheSiblingFrames(string name, int[] ints, int[] furni, int[] users, int[][] furniAllowed, int[][] usersAllowed)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        var metadata = WiredNativeEditorProjection.Metadata(name);
        Assert.Equal(ints, native.OwnedIntParams.ToArray());
        Assert.Equal(furni, native.FurniSourceTypes.ToArray());
        Assert.Equal(users, native.UserSourceTypes.ToArray());
        Assert.Equal("", native.Text);
        Assert.Equal(furniAllowed, metadata.FurniAllowed.Select(slot => slot.ToArray()).ToArray());
        Assert.Equal(usersAllowed, metadata.UsersAllowed.Select(slot => slot.ToArray()).ToArray());
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out _));
    }

    [Fact]
    public void TheNumberOfVariablePickersFollowsTheAirForms()
    {
        // ValueOrVariableSection: one in the condition and in Initiate Transaction, two in the custom contract, the scanner's context variable.
        foreach (var (name, variables) in new[] { ("wf_act_init_transaction", 1), ("wf_act_cancel_transaction", 0), ("wf_cnd_chest_has_items", 1), ("wf_cnd_chest_has_item_type", 1),
            ("wf_xtra_scan_chest_furni_by_type", 1), ("wf_xtra_custom_contract", 2), ("wf_trg_transaction_complete", 0), ("wf_trg_transaction_fail", 0) }) {
            Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
            Assert.Equal(variables, WiredNativeEditorProjection.DefaultNative(descriptor).VariableIds.Length);
        }
    }

    [Theory]
    [InlineData("wf_act_init_transaction", new[] { 0, 1, 0, 0, 0, 300 }, true)]
    [InlineData("wf_act_init_transaction", new[] { 2, 500, 0, 0, 1, 3600 }, true)]
    [InlineData("wf_act_init_transaction", new[] { 1, 1, 0, 0, 1, 30 }, true)]
    [InlineData("wf_act_init_transaction", new[] { 3, 1, 0, 0, 0, 300 }, false)]
    [InlineData("wf_act_init_transaction", new[] { -1, 1, 0, 0, 0, 300 }, false)]
    [InlineData("wf_act_init_transaction", new[] { 0, 0, 0, 0, 0, 300 }, false)]
    [InlineData("wf_act_init_transaction", new[] { 0, 501, 0, 0, 0, 300 }, false)]
    [InlineData("wf_act_init_transaction", new[] { 0, 1, 2, 0, 0, 300 }, false)]
    [InlineData("wf_act_init_transaction", new[] { 0, 1, 0, 4, 0, 300 }, false)]
    [InlineData("wf_act_init_transaction", new[] { 0, 1, 0, 0, 2, 300 }, false)]
    [InlineData("wf_act_init_transaction", new[] { 0, 1, 0, 0, 1, 29 }, true)]
    [InlineData("wf_act_init_transaction", new[] { 0, 1, 0, 0, 1, 3601 }, false)]
    [InlineData("wf_act_cancel_transaction", new[] { 0 }, true)]
    [InlineData("wf_act_cancel_transaction", new[] { 1 }, true)]
    [InlineData("wf_act_cancel_transaction", new[] { 2 }, false)]
    // Observed on AIR WIN63-202610201032-107964540: the chest conditions clamp or reset the amount, option and comparison instead of refusing.
    [InlineData("wf_cnd_chest_has_items", new[] { 0, 0, 2, 0 }, true)]
    [InlineData("wf_cnd_chest_has_items", new[] { 1000000, 1, 2, 5 }, true)]
    [InlineData("wf_cnd_chest_has_items", new[] { -1, 0, 2, 0 }, true)]
    [InlineData("wf_cnd_chest_has_items", new[] { 1000001, 0, 2, 0 }, true)]
    [InlineData("wf_cnd_chest_has_items", new[] { 0, 0, 2, 6 }, true)]
    [InlineData("wf_cnd_chest_has_items", new[] { 0, 0, 2, -1 }, true)]
    [InlineData("wf_cnd_chest_has_item_type", new[] { 7, 0, 2, 3 }, true)]
    [InlineData("wf_cnd_chest_has_item_type", new[] { 1000001, 0, 2, 3 }, true)]
    [InlineData("wf_xtra_scan_chest_furni_by_type", new[] { 0 }, true)]
    [InlineData("wf_xtra_scan_chest_furni_by_type", new[] { 1 }, true)]
    [InlineData("wf_xtra_scan_chest_furni_by_type", new[] { 2 }, true)]
    [InlineData("wf_xtra_custom_contract", new[] { 1, 1, 0, 100000, 2, 1, 0, 1, 1, 2 }, true)]
    [InlineData("wf_xtra_custom_contract", new[] { 0, 0, 0, 1, 2, 0, 0, 0, 1, 2 }, true)]
    [InlineData("wf_xtra_custom_contract", new[] { 1, 2, 0, 5, 2, 0, 0, 0, 1, 2 }, false)]
    [InlineData("wf_xtra_custom_contract", new[] { 1, 0, 0, 0, 2, 0, 0, 0, 1, 2 }, false)]
    [InlineData("wf_xtra_custom_contract", new[] { 1, 0, 0, 100001, 2, 0, 0, 0, 1, 2 }, false)]
    [InlineData("wf_xtra_custom_contract", new[] { 0, 0, 0, 1, 2, 1, 0, 0, 100001, 2 }, false)]
    [InlineData("wf_xtra_custom_contract", new[] { 2, 0, 0, 1, 2, 0, 0, 0, 1, 2 }, false)]
    public void TheSavedNumbersStayInsideTheLimitsOfTheAirForms(string name, int[] scalars, bool saved)
    {
        using var world = new G1World();
        // The AIR target codes are what the editor sends; the scalars here use the executor's codes, converted like the editor does.
        var targets = name == "wf_act_init_transaction" ? new[] { 3 } : name == "wf_cnd_chest_has_items" || name == "wf_cnd_chest_has_item_type" ? [2]
            : name == "wf_xtra_custom_contract" ? [4, 9] : [];
        var air = (int[])scalars.Clone();

        foreach (var slot in targets) {
            air[slot] = scalars[slot] is >= 0 and <= 3 ? WiredNativeAuxiliaryEditor.AirTarget(scalars[slot]) : scalars[slot];
        }

        Assert.Equal(saved, world.TrySave(name, native => native with { OwnedIntParams = [.. air] }));
    }

    // Observed on AIR WIN63-202610201032-107964540: a raw save of 29 seconds is accepted and read back as 30.
    [Theory]
    [InlineData(29, 30)]
    [InlineData(0, 30)]
    [InlineData(30, 30)]
    [InlineData(300, 300)]
    public void ATimeoutBelowThirtySecondsIsSavedAsThirty(int seconds, int stored)
    {
        using var world = new G1World();

        Assert.True(world.TrySave("wf_act_init_transaction", native => native with { OwnedIntParams = [0, 2, 0, WiredNativeAuxiliaryEditor.AirTarget(0), 1, seconds] }, out var runtime));
        Assert.Equal([0, 2, 0, 0, 1, stored], runtime.IntParams.ToArray());
    }

    [Theory]
    [InlineData("wf_act_init_transaction", 0, 11, false)]
    [InlineData("wf_act_init_transaction", 0, 201, true)]
    [InlineData("wf_act_init_transaction", 1, 101, true)]
    [InlineData("wf_act_cancel_transaction", 0, 101, false)]
    [InlineData("wf_act_cancel_transaction", 0, 0, true)]
    [InlineData("wf_cnd_chest_has_items", 0, 101, false)]
    [InlineData("wf_cnd_chest_has_items", 1, 101, false)]
    [InlineData("wf_cnd_chest_has_items", 1, 200, true)]
    [InlineData("wf_cnd_chest_has_item_type", 1, 101, true)]
    [InlineData("wf_cnd_chest_has_item_type", 2, 101, true)]
    [InlineData("wf_xtra_scan_chest_furni_by_type", 0, 101, true)]
    [InlineData("wf_xtra_scan_chest_furni_by_type", 1, 0, true)]
    [InlineData("wf_xtra_custom_contract", 3, 101, true)]
    [InlineData("wf_trg_transaction_complete", 0, 200, false)]
    [InlineData("wf_trg_transaction_fail", 0, 0, false)]
    public void EachFurniSlotAcceptsOnlyTheSourcesOfItsCard(string name, int slot, int source, bool saved)
    {
        using var world = new G1World();

        Assert.Equal(saved, world.TrySave(name, native =>
        {
            var sources = native.FurniSourceTypes.ToArray();
            sources[slot] = source;

            return native with { FurniSourceTypes = [.. sources] };
        }));
    }

    [Theory]
    [InlineData("wf_act_init_transaction", 0, 11, false)]
    [InlineData("wf_act_init_transaction", 1, 200, true)]
    [InlineData("wf_act_cancel_transaction", 0, 201, true)]
    [InlineData("wf_act_cancel_transaction", 0, 11, false)]
    [InlineData("wf_cnd_chest_has_items", 0, 11, false)]
    [InlineData("wf_cnd_chest_has_items", 0, 0, true)]
    [InlineData("wf_cnd_chest_has_item_type", 0, 100, false)]
    [InlineData("wf_xtra_custom_contract", 1, 201, true)]
    [InlineData("wf_xtra_custom_contract", 0, 11, false)]
    public void EachUserSlotAcceptsTheTriggeringSelectorAndSignalUsersAndNeverTheClickedUser(string name, int slot, int source, bool saved)
    {
        using var world = new G1World();

        Assert.Equal(saved, world.TrySave(name, native =>
        {
            var sources = native.UserSourceTypes.ToArray();
            sources[slot] = source;

            return native with { UserSourceTypes = [.. sources] };
        }));
    }

    [Fact]
    public void TheCardsThatTakeNoSettingsRefuseExtraValues()
    {
        using var world = new G1World();

        Assert.True(world.TrySave("wf_trg_transaction_complete", native => native));
        Assert.False(world.TrySave("wf_trg_transaction_complete", native => native with { OwnedIntParams = [1] }));
        Assert.False(world.TrySave("wf_act_cancel_transaction", native => native with { OwnedIntParams = [0, 1] }));
        // Observed on AIR WIN63-202610201032-107964540: Scan Chest takes the frame default for a missing mode.
        Assert.True(world.TrySave("wf_xtra_scan_chest_furni_by_type", native => native with { OwnedIntParams = [] }));
    }

    [Fact]
    public void ASavedConfigurationReopensAsItWasSaved()
    {
        using var world = new G1World();
        var box = world.AddBox("wf_act_init_transaction", native => native with
        {
            OwnedIntParams = [2, 40, 0, -20, 1, 90],
            FurniSourceTypes = [100, 101, 0],
            UserSourceTypes = [200, 0]
        });

        Assert.True(WiredNativeEditorProjection.TryProject(box.Item, box.Descriptor, box.Configuration, out var reopened));
        Assert.Equal([2, 40, 0, -20, 1, 90], reopened.OwnedIntParams.ToArray());
        Assert.Equal([100, 101, 0], reopened.FurniSourceTypes.ToArray());
        Assert.Equal([200, 0], reopened.UserSourceTypes.ToArray());
        Assert.Equal([2, 40, 0, 2, 1, 90], box.Configuration.IntParams.ToArray());
    }
}
