using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public sealed class WiredNativeVariableModesTests
{
    [Fact]
    public void PickedBoxModeRoundTripsPicksAndInactiveCatalogueIdAndIgnoresUnpickedForeignOrRemovedDefinitions()
    {
        var config = WiredOctoberEditorParityTests.Compile("wf_trg_var_changed", n => n with
        {
            OwnedIntParams = [1, 1, 1, 1, 4, 1],
            VariableIds = ["n"],
            PrimaryItems = [new(10, false), new(11, false), new(99, false)]
        });
        var native = JsonSerializer.Deserialize<WiredNativeEditorConfiguration>(JsonSerializer.Serialize(config.Origin!.Native))!;
        Assert.Equal(new uint[] { 10, 11, 99 }, native.PrimaryItems.Select(item => item.ItemId));
        Assert.Equal(1, native.OwnedIntParams[5]);
        Assert.Equal("n", native.VariableIds[0]);
        Assert.True(WiredVariableChangedTrigger.TryNormalize(config, out _, out _));
        var directory = new Directory();
        var module = new WiredVariableModule(1, directory, new MemoryWiredVariableStore(), TimeProvider.System);
        var user = new WiredVariableHolder(WiredVariableTarget.User, 7, 1);
        var furni = new WiredVariableHolder(WiredVariableTarget.Furni, 8, 8);
        var frame = new WiredVariableFrame(1, [user, furni]);
        Assert.True(module.Mutate(new(user.Target, "custom:10"), user, WiredVariableMutation.Give, 4, frame, origin: 2));
        var change = Assert.Single(module.DrainChanges());
        Assert.True(WiredVariableChangedTrigger.MatchesPickedBoxes(config, change, module, 1));
        Assert.False(WiredVariableChangedTrigger.MatchesPickedBoxes(config, change with { Origin = 0 }, module, 1));
        Assert.False(WiredVariableChangedTrigger.MatchesPickedBoxes(config, change with { RoomId = 2 }, module, 1));
        Assert.False(WiredVariableChangedTrigger.MatchesPickedBoxes(config, change with { Key = change.Key with { DefinitionId = 12 } }, module, 1));
        Assert.False(WiredVariableChangedTrigger.MatchesPickedBoxes(config, change with { Key = change.Key with { DefinitionId = 99 } }, module, 1));
        Assert.True(module.Mutate(new(furni.Target, "custom:11"), furni, WiredVariableMutation.Give, 6, frame, origin: 2));
        Assert.True(WiredVariableChangedTrigger.MatchesPickedBoxes(config, Assert.Single(module.DrainChanges()), module, 1));
        Assert.True(module.Mutate(new(user.Target, "custom:10"), user, WiredVariableMutation.Set, 3, frame, origin: 2));
        Assert.False(WiredVariableChangedTrigger.MatchesPickedBoxes(config, Assert.Single(module.DrainChanges()), module, 1)); // increase-only
        Assert.True(module.Mutate(new(user.Target, "custom:10"), user, WiredVariableMutation.Remove, 0, frame, origin: 2));
        Assert.True(WiredVariableChangedTrigger.MatchesPickedBoxes(config, Assert.Single(module.DrainChanges()), module, 1));
        directory.Removed = true;
        Assert.False(WiredVariableChangedTrigger.MatchesPickedBoxes(config, change, module, 1));
        directory.Removed = false;
        directory.Owner = 6;
        Assert.False(WiredVariableChangedTrigger.MatchesPickedBoxes(config, change, module, 1));
    }

    // A Variable Changed trigger that picks an Echo box sees the changes of the variable the Echo mirrors.
    [Fact]
    public void PickedEchoBoxSeesTheChangesOfItsSource()
    {
        var config = WiredOctoberEditorParityTests.Compile("wf_trg_var_changed", n => n with
        {
            OwnedIntParams = [1, 1, 1, 1, 4, 1],
            VariableIds = ["n"],
            PrimaryItems = [new(31, false)]
        });
        var module = new WiredVariableModule(1, new Directory(), new MemoryWiredVariableStore(), TimeProvider.System);
        var user = new WiredVariableHolder(WiredVariableTarget.User, 7, 1);
        var frame = new WiredVariableFrame(1, [user]);
        Assert.True(module.Mutate(new(user.Target, "custom:30"), user, WiredVariableMutation.Give, 4, frame, origin: 2));
        Assert.True(WiredVariableChangedTrigger.MatchesPickedBoxes(config, Assert.Single(module.DrainChanges()), module, 1));
        Assert.True(module.Mutate(new(user.Target, "custom:31"), user, WiredVariableMutation.Set, 3, frame, origin: 2));
        Assert.False(WiredVariableChangedTrigger.MatchesPickedBoxes(config, Assert.Single(module.DrainChanges()), module, 1)); // increase-only
        Assert.True(module.Mutate(new(user.Target, "custom:31"), user, WiredVariableMutation.Remove, 0, frame, origin: 2));
        Assert.True(WiredVariableChangedTrigger.MatchesPickedBoxes(config, Assert.Single(module.DrainChanges()), module, 1));
    }

    [Theory]
    [InlineData(WiredVariableTarget.User, "wf_slc_users_with_var", 1, "user:")]
    [InlineData(WiredVariableTarget.Furni, "wf_slc_furni_with_var", 0, "furni:")]
    public void SameHolderReferenceReadsEachCandidateRatherThanTheFirstPickedSource(WiredVariableTarget target, string selector, int airTarget, string prefix)
    {
        var directory = new Directory { SelectorTarget = target };
        var module = new WiredVariableModule(1, directory, new MemoryWiredVariableStore(), TimeProvider.System);
        var first = new WiredVariableHolder(target, 100, 1);
        var second = new WiredVariableHolder(target, 200, 2);
        var third = new WiredVariableHolder(target, 300, 3);
        var frame = new WiredVariableFrame(1, [first, second, third]) { ResolveSource = (_, _, _) => [first] };

        foreach (var (holder, value) in new[] { (first, 10), (second, 20), (third, 30) }) {
            Assert.True(module.Mutate(new(target, "custom:20"), holder, WiredVariableMutation.Give, value, frame));

            if (holder != third) {
                Assert.True(module.Mutate(new(target, "custom:21"), holder, WiredVariableMutation.Give, value, frame));
            }
        }

        var config = WiredOctoberEditorParityTests.Compile(selector, n => n with
        {
            OwnedIntParams = [1, 2, 0, 0, airTarget, 1],
            VariableIds = [prefix + "20", prefix + "21"]
        });
        using var queries = new WiredVariableQueries(module, frame);
        Assert.True(queries.MatchSelector(selector, config, first));
        Assert.True(queries.MatchSelector(selector, config, second));
        Assert.False(queries.MatchSelector(selector, config, third));
        var legacy = WiredOctoberEditorParityTests.Compile(selector, n => n with
        {
            OwnedIntParams = [1, 2, 0, 0, airTarget],
            VariableIds = [prefix + "20", prefix + "21"]
        });
        Assert.Equal(9, legacy.IntParams.Length);
        Assert.True(queries.MatchSelector(selector, legacy, first));
        Assert.False(queries.MatchSelector(selector, legacy, second));
        Assert.False(WiredOctoberEditorParityTests.TryCompile(selector, n => n with { OwnedIntParams = [1, 2, 0, 0, airTarget == 0 ? 1 : 0, 1] }));
    }

    private sealed class Directory : IWiredVariableDirectory
    {
        public bool Removed; public uint Owner = 5; public WiredVariableTarget SelectorTarget;
        public uint? GetRoomOwner(uint id) => id == 1 ? Owner : 5;
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            10 when !Removed => new(10, 1, 5, "user", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            11 => new(11, 1, 5, "furni", WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, true),
            12 => new(12, 1, 5, "other", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            30 => new(30, 1, 5, "source", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            31 => new(31, 1, 5, "echo", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true, Link: new(1, new(WiredVariableTarget.User, "custom:30"), false)),
            20 or 21 => new(id, 1, 5, "selector", SelectorTarget, WiredVariableAvailability.RoomActive, true),
            99 => new(99, 2, 5, "foreign", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            _ => null
        };
    }
}
