using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The WIN63-202610121709 variable editors (GiveVariable, ChangeVariable, VariableValue, VariableAge, WithVariable,
/// VariableUpdate readIntParamsFromForm / readVariableIdsFromForm) and what their new fields do at run time.
/// </summary>
public sealed class WiredOctoberVariableBoxTests
{
    [Fact]
    public void FreshBoxesOpenOnTheOctoberDefaults()
    {
        Assert.Equal([1, 0, 0, 0, 0, -10, 0], Defaults("wf_act_give_var"));
        Assert.Equal([1, 0, 0, 0, 0, -10, 0], Defaults("wf_act_change_var_val"));
        Assert.Equal([1, 0, 0, 0, 0, 1, 0, -10], Defaults("wf_cnd_var_age_match"));
        Assert.Equal(["n", "n"], WiredNativeEditorProjection.Metadata("wf_act_give_var").VariableDefaults.ToArray());
        Assert.Equal(["n", "n"], WiredNativeEditorProjection.Metadata("wf_cnd_var_age_match").VariableDefaults.ToArray());
    }

    [Fact]
    public void OctoberSavesCompileAndOlderSavesStillDo()
    {
        // Give: [target, value high, value low, override, from variable, reference target, same holder], ids [variable, reference].
        var give = WiredOctoberEditorParityTests.Compile("wf_act_give_var", n => n with { OwnedIntParams = [0, 0, 5, 1, 1, 0, 1], VariableIds = ["furni:10", "furni:11"] });
        Assert.Equal([1, 1, 5, 0, 0, 1, 1, 1], give.IntParams.ToArray());
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_act_give_var", n => n with { OwnedIntParams = [0, 0, 5, 1], VariableIds = ["furni:10"] }));
        // Value mode: the reference slot is WiredVariable.NONE ("n").
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_act_give_var", n => n with { OwnedIntParams = [0, 0, 5, 0, 0, -10, 0], VariableIds = ["furni:10", "n"] }));
        // Change and Variable Value: a seventh field, same holder.
        Assert.Equal(10, WiredOctoberEditorParityTests.Compile("wf_act_change_var_val", n => n with { OwnedIntParams = [0, 1, 1, 0, 0, 0, 1], VariableIds = ["furni:10", "furni:11"] }).IntParams.Length);
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_cnd_var_val_match", n => n with { OwnedIntParams = [0, 1, 1, 0, 0, 0, 1], VariableIds = ["furni:10", "furni:11"], Quantifier = 0 }));
        // Age: [target, comparison, clock, duration high, duration low, unit, from variable, reference target].
        Assert.Equal(10, WiredOctoberEditorParityTests.Compile("wf_cnd_var_age_match", n => n with { OwnedIntParams = [0, 0, 0, 0, 0, 1, 1, -10], VariableIds = ["furni:10", "room:12"], Quantifier = 0 }).IntParams.Length);
        // With Variable and Variable Changed gain a sixth field; the same-holder selector and box watch mode are not implemented.
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_trg_var_changed", n => n with { OwnedIntParams = [1, 1, 0, 7, -1, 0] }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_trg_var_changed", n => n with { OwnedIntParams = [1, 1, 0, 7, -1, 1] }));
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_slc_furni_with_var", n => n with { OwnedIntParams = [2, 0, 0, 0, -10, 0] }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_slc_furni_with_var", n => n with { OwnedIntParams = [2, 2, 0, 0, 0, 1] }));
    }

    [Fact]
    public void SameHolderOnlyAppliesToAVariableOfTheDestinationsOwnKind()
    {
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_change_var_val", n => n with { OwnedIntParams = [0, 1, 1, 0, 0, 1, 1], VariableIds = ["furni:10", "user:11"] }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_give_var", n => n with { OwnedIntParams = [-10, 0, 0, 0, 1, -10, 1], VariableIds = ["room:10", "room:11"] }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReferenceVariablesFeedGiveChangeAndMatch(bool batch)
    {
        var (module, executor, holders, frame) = World(batch);
        Set(module, frame, holders[0], 11, 3);
        Set(module, frame, holders[1], 11, 7);

        // Give from a variable, same holder: each furni gets its own value.
        Assert.True(executor.Execute("wf_act_give_var", Config([1, 0, 0, 0, 0, 1, 1, 1], "furni:10", "furni:11"), frame));
        Assert.Equal(new long[] { 3, 7 }, Read(module, frame, holders, 10));

        // Give from a variable on its own source: the first value read.
        Assert.True(executor.Execute("wf_act_give_var", Config([1, 0, 0, 0, 0, 1, 1, 0], "furni:12", "furni:11"), frame));
        Assert.Equal(new long[] { 3, 3 }, Read(module, frame, holders, 12));

        // Change by a variable, same holder: 10 += 11 on each furni.
        Assert.True(executor.Execute("wf_act_change_var_val", Config([1, 1, 1, 0, 1, 0, 0, 0, 0, 1], "furni:10", "furni:11"), frame));

        if (batch) {
            Assert.True(frame.VariableChanges!.Flush());
        }

        Assert.Equal(new long[] { 6, 14 }, Read(module, frame, holders, 10));

        // Match on the same holder: 10 == 2 * 11 holds on both furni (comparison code 2 is ==).
        Set(module, frame, holders[0], 12, 6);
        Set(module, frame, holders[1], 12, 14);
        Assert.True(executor.Execute("wf_cnd_var_val_match", Config([1, 2, 1, 0, 1, 0, 0, 0, 0, 1, 0], "furni:10", "furni:12"), frame));
        Assert.False(executor.Execute("wf_cnd_var_val_match", Config([1, 2, 1, 0, 1, 0, 0, 0, 0, 1, 0], "furni:10", "furni:11"), frame));
    }

    [Fact]
    public void VariableAgeCanReadItsDurationFromAVariable()
    {
        var (module, executor, holders, frame) = World(false);
        Set(module, frame, holders[0], 10, 1);
        Set(module, frame, holders[1], 10, 1);
        Set(module, frame, holders[0], 11, 3600);

        // Created less than 3600 seconds ago, the duration read from variable 11.
        Assert.True(executor.Execute("wf_cnd_var_age_match", Config([1, 0, 0, 0, 1, 0, 0, 1, 1, 0], "furni:10", "furni:11"), frame));
        // More than that: false.
        Assert.False(executor.Execute("wf_cnd_var_age_match", Config([1, 0, 2, 0, 1, 0, 0, 1, 1, 0], "furni:10", "furni:11"), frame));
        // No holder of the duration variable: false.
        Assert.False(executor.Execute("wf_cnd_var_age_match", Config([1, 0, 0, 0, 1, 0, 0, 1, 1, 0], "furni:10", "furni:12"), frame));
    }

    [Theory]
    [InlineData("-120", WiredVariableTarget.Furni, "internal:@position.x")]
    [InlineData("-230", WiredVariableTarget.User, "internal:@position.x")]
    [InlineData("-300", WiredVariableTarget.Global, "internal:@furni_count")]
    [InlineData("-420", WiredVariableTarget.Context, "internal:@event.signal.antenna_id")]
    [InlineData("~131", WiredVariableTarget.Furni, "internal:~chest.locked")]
    public void HabboBuiltInIdsNameOurBuiltInTokens(string id, WiredVariableTarget target, string token)
    {
        // hhus WiredAllVariablesDiffs, WIN63-202610121709: 162 built-in and smart variables with fixed ids.
        Assert.Equal(162, WiredHabboVariableIds.Count);
        Assert.True(WiredVariableDescription.TryParseCatalogId(id, out var parsed, out var parsedToken));
        Assert.Equal((target, token), (parsed, parsedToken));
        Assert.Equal(id, WiredHabboVariableIds.IdOf(target, token));
        Assert.False(WiredVariableDescription.TryParseCatalogId("-99999", out _, out _));
    }

    private static int[] Defaults(string name) => [.. WiredNativeEditorProjection.Metadata(name).OwnedDefaults];

    private static WiredConfiguration Config(int[] ints, params string[] ids) => new() { IntParams = [.. ints], VariableIds = [.. ids] };

    private static void Set(WiredVariableModule module, WiredVariableFrame frame, WiredVariableHolder holder, uint id, long value) =>
        Assert.True(module.Mutate(new(holder.Target, "custom:" + id), holder, WiredVariableMutation.Replace, value, frame));

    private static long[] Read(WiredVariableModule module, WiredVariableFrame frame, WiredVariableHolder[] holders, uint id) =>
        [.. holders.Select(holder => module.Read(new(holder.Target, "custom:" + id), holder, frame)!.Value)];

    private static (WiredVariableModule, WiredVariableExecutors, WiredVariableHolder[], WiredVariableFrame) World(bool batch)
    {
        var holders = new[] { new WiredVariableHolder(WiredVariableTarget.Furni, 1, 1), new WiredVariableHolder(WiredVariableTarget.Furni, 2, 2) };
        var frame = new WiredVariableFrame(1, holders) { Trigger = holders, VariableChanges = batch ? new() : null };
        var module = new WiredVariableModule(1, new Directory(), new MemoryWiredVariableStore(), TimeProvider.System);

        return (module, new WiredVariableExecutors(module, TimeProvider.System), holders, frame);
    }

    private sealed class Directory : IWiredVariableDirectory
    {
        public WiredVariableDefinition? Find(uint id) => id is 10 or 11 or 12
            ? new(id, 1, 5, "test" + id, WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, true) : null;
        public uint? GetRoomOwner(uint roomId) => 5;
    }
}
