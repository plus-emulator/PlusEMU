using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;
using static Plus.Tests.WiredSelectorTests;

namespace Plus.Tests;

// Official hhus WIN63-202610131318 batches r80-var-01/02/03: the saved native shapes and the behaviour the windows showed.
public sealed class WiredObservedR82VariableTests
{
    private const uint Marker = 7;
    private const uint FurniVariable = 10;
    private const uint UserVariable = 11;

    private static WiredConfiguration Compile(string name, int[] owned, string[] ids, int[]? furniSources = null, int[]? userSources = null, uint[]? picks = null) =>
        WiredOctoberEditorParityTests.Compile(name, n =>
        {
            var edited = n with { OwnedIntParams = [.. owned], VariableIds = [.. ids] };

            if (furniSources != null) {
                edited = edited with { FurniSourceTypes = [.. furniSources] };
            }

            if (userSources != null) {
                edited = edited with { UserSourceTypes = [.. userSources] };
            }

            return picks == null ? edited : edited with { PrimaryItems = [.. picks.Select(id => new WiredNativeItemReference(id, false))] };
        });

    // The saved native ints and ids of every r80 variable card are accepted by the editor.
    [Fact]
    public void EveryObservedVariableSaveIsAccepted()
    {
        Compile("wf_act_give_var", [0, 0, 5, 0, 0, -10, 0], ["furni:10", "n"], [100, 200], [0, 200], [Marker]);
        Compile("wf_act_change_var_val", [0, 0, 0, 0, 7, -10, 0], ["furni:10", "n"], [100, 200], [0, 200], [Marker]);
        Compile("wf_act_remove_var", [0], ["furni:10"], [100], [0], [Marker]);
        Compile("wf_trg_var_changed", [1, 1, 1, 7, -1, 0], ["furni:10"]);
        Compile("wf_slc_furni_with_var", [2, 0, 0, 0, -10, 0], ["furni:10", "n"], [100], null, [Marker]);
        Compile("wf_slc_users_with_var", [2, 0, 0, 0, -10, 0], ["user:11", "n"], [100], [0]);
        Compile("wf_cnd_var_age_match", [0, 0, 0, 0, 600, 1, 0, -10], ["furni:10", "n"], [100, 200], [0, 200], [Marker]);
        Compile("wf_cnd_var_age_match", [0, 2, 0, 0, 600, 1, 0, -10], ["furni:10", "n"], [100, 200], [0, 200], [Marker]);
        Compile("wf_act_give_var", [1, 0, 0, 0, 0, -10, 0], ["user:11", "n"], [0, 200], [0, 200]);
        Compile("wf_xtra_filter_users_by_var", [10, 0, 0, 1], ["user:11", "n"], [100], [0]);
        Compile("wf_xtra_filter_furni_by_var", [1, 0, 0, 1], ["furni:10", "n"], [100], [0]);
        Compile("wf_act_click_conf", [0, 0], [], null, [0]);
        Compile("wf_act_give_score", [1, 0], []);
        Compile("wf_act_give_score_tm", [1, 0, 1], []);
        Compile("wf_trg_score_achieved", [1, 0], []);
        Compile("wf_trg_score_achieved", [1, 1], []);
        Compile("wf_var_furni", [1, 1], []);
        Compile("wf_var_user", [0, 0], []);
    }

    private static (WiredVariableModule Module, WiredVariableFrame Frame, WiredVariableHolder Furni, WiredVariableHolder User) Vars(TimeProvider clock)
    {
        var furni = new WiredVariableHolder(WiredVariableTarget.Furni, (int)Marker, (int)Marker);
        var user = new WiredVariableHolder(WiredVariableTarget.User, 999, 4);
        var frame = new WiredVariableFrame(1, [furni, user]) { Trigger = [user] };
        frame.Selector.Add(furni);

        return (new WiredVariableModule(1, new Directory(), new MemoryWiredVariableStore(), clock), frame, furni, user);
    }

    // Official: give (create), change (assign 7) and remove each fired the Variable Changed stack saved [1,1,1,7,-1,0].
    [Fact]
    public void VariableChangedFiresForCreateChangeAndDelete()
    {
        var (module, frame, furni, _) = Vars(TimeProvider.System);
        var reference = new WiredVariableReference(WiredVariableTarget.Furni, "custom:10");
        var trigger = Compile("wf_trg_var_changed", [1, 1, 1, 7, -1, 0], ["furni:10"]) with { Text = "custom:10" };

        Assert.True(module.Mutate(reference, furni, WiredVariableMutation.Give, 5, frame, origin: 2));
        Assert.True(WiredVariableChangedTrigger.Matches(trigger, Assert.Single(module.DrainChanges())));
        Assert.True(module.Mutate(reference, furni, WiredVariableMutation.Set, 7, frame, origin: 2));
        Assert.True(WiredVariableChangedTrigger.Matches(trigger, Assert.Single(module.DrainChanges())));
        Assert.True(module.Mutate(reference, furni, WiredVariableMutation.Remove, 0, frame, origin: 2));
        Assert.True(WiredVariableChangedTrigger.Matches(trigger, Assert.Single(module.DrainChanges())));
    }

    // Official: right after the give, "younger than 600 s" passed and "older than 600 s" did not; before the give both failed.
    [Fact]
    public void VariableAgeMatchesAFreshlyGivenVariableOnlyAsYounger()
    {
        var (module, frame, furni, _) = Vars(TimeProvider.System);
        var executors = new WiredVariableExecutors(module, TimeProvider.System);
        var younger = Compile("wf_cnd_var_age_match", [0, 0, 0, 0, 600, 1, 0, -10], ["furni:10", "n"], [100, 200], [0, 200], [Marker]);
        var older = Compile("wf_cnd_var_age_match", [0, 2, 0, 0, 600, 1, 0, -10], ["furni:10", "n"], [100, 200], [0, 200], [Marker]);

        Assert.False(executors.Execute("wf_cnd_var_age_match", younger, frame));
        Assert.False(executors.Execute("wf_cnd_var_age_match", older, frame));
        Assert.True(module.Mutate(new(WiredVariableTarget.Furni, "custom:10"), furni, WiredVariableMutation.Give, 5, frame));
        Assert.True(executors.Execute("wf_cnd_var_age_match", younger, frame));
        Assert.False(executors.Execute("wf_cnd_var_age_match", older, frame));
    }

    // Official: a user variable without a value, given to the own user, was selected by With Variable (the teleport followed).
    [Fact]
    public void UserSelectorFindsAValuelessUserVariableAfterTheGive()
    {
        var (module, frame, _, user) = Vars(TimeProvider.System);
        var reference = new WiredVariableReference(WiredVariableTarget.User, "custom:11");
        var config = WiredVariableFixtures.WithVar("wf_slc_users_with_var", Config([0, 0, 0, 0, 0, 0, 0, 0, 0], text: "custom:11"));
        using var queries = WiredSelectorVariableBridge.Create(module, frame);
        var inputs = Inputs() with { FurniVariablePredicate = queries.FurniPredicate, UserVariablePredicate = queries.UserPredicate };

        Assert.Empty(WiredSelectorModule.SelectRaw("wf_slc_users_with_var", config, World(), inputs).Selection.UserIds);
        Assert.True(module.Mutate(reference, user, WiredVariableMutation.Give, 0, frame));
        using var after = WiredSelectorVariableBridge.Create(module, frame);
        var inputsAfter = Inputs() with { FurniVariablePredicate = after.FurniPredicate, UserVariablePredicate = after.UserPredicate };
        Assert.Equal([4], WiredSelectorModule.SelectRaw("wf_slc_users_with_var", config, World(), inputsAfter).Selection.UserIds);
    }

    // Official: Filter Users By Variable (default 10, highest first) on a valueless variable dropped the own user (no teleport followed).
    [Fact]
    public void FilterByVariableKeepsNoHolderOfAValuelessVariable()
    {
        var (module, frame, _, user) = Vars(TimeProvider.System);
        var reference = new WiredVariableReference(WiredVariableTarget.User, "custom:11");
        Assert.True(module.Mutate(reference, user, WiredVariableMutation.Give, 0, frame));

        Assert.Empty(WiredVariablePredicates.Filter(module, reference, [user], frame, 0, 10));
    }

    // Official initial editor frames (receipts initial_editor): source options, defaults, wall flag and owned defaults of every card.
    [Theory]
    [InlineData("wf_act_give_var", "0,100,101,200,201;0,100,101,200,201", "0,200,201;0,200,201", "0,200", "0,200", true, "1,0,0,0,0,-10,0")]
    [InlineData("wf_act_change_var_val", "0,100,101,200,201;0,100,101,200,201", "0,200,201;0,200,201", "0,200", "0,200", true, "1,0,0,0,0,-10,0")]
    [InlineData("wf_act_remove_var", "0,100,200,201", "0,200,201", "100", "0", true, "1")]
    [InlineData("wf_slc_furni_with_var", "0,100,201", "0,201", "100", "0", true, "2,0,0,0,-10,0")]
    [InlineData("wf_slc_users_with_var", "0,100,201", "0,201", "100", "0", true, "2,0,0,0,-10,0")]
    [InlineData("wf_cnd_var_age_match", "0,100,101,200,201;0,100,101,200,201", "0,200,201;0,200,201", "0,200", "0,200", true, "1,0,0,0,0,1,0,-10")]
    [InlineData("wf_xtra_filter_furni_by_var", null, null, "100", "0", false, "10,0,0,1")]
    [InlineData("wf_xtra_filter_users_by_var", null, null, "100", "0", false, "10,0,0,1")]
    [InlineData("wf_act_rel_mov", "0,100,200,201", "", "100", "", true, "0,0")]
    [InlineData("wf_trg_var_changed", "", "", "", "", false, "1,1,0,7,-1,0")]
    [InlineData("wf_act_click_conf", "", "0,200,201", "", "0", false, "0,0")]
    public void EditorFramesMatchTheOfficialInitialEditor(string name, string? furniAllowed, string? usersAllowed, string furniDefaults,
        string userDefaults, bool wall, string owned)
    {
        var metadata = WiredNativeEditorProjection.Metadata(name);
        string Groups(System.Collections.Immutable.ImmutableArray<System.Collections.Immutable.ImmutableArray<int>> groups) =>
            string.Join(';', groups.Select(group => string.Join(',', group)));

        if (furniAllowed != null) {
            Assert.Equal(furniAllowed, Groups(metadata.FurniAllowed));
            Assert.Equal(usersAllowed, Groups(metadata.UsersAllowed));
        }

        Assert.Equal(furniDefaults, string.Join(',', metadata.FurniDefaults));
        Assert.Equal(userDefaults, string.Join(',', metadata.UserDefaults));
        Assert.Equal(wall, metadata.AllowWall);
        Assert.Equal(owned, string.Join(',', metadata.OwnedDefaults));
    }

    // Older saves of Give Variable and Variable Age carry one selector per kind; they still compile and the second selector is not asked for.
    [Fact]
    public void SavesWithASingleSelectorPerKindStillCompileAndKeepTheirLayout()
    {
        var give = Compile("wf_act_give_var", [0, 0, 5, 1, 1, 0, 0], ["furni:10", "furni:10"], [100], [0]);
        Assert.Equal(8, give.IntParams.Length);
        var age = Compile("wf_cnd_var_age_match", [0, 0, 0, 0, 600, 1, 0, -10], ["furni:10", "n"], [100], [0]);
        Assert.Equal(10, age.IntParams.Length);
        // Two selectors, value mode: the reference's sources are not stored.
        Assert.Equal(8, Compile("wf_act_give_var", [0, 0, 5, 0, 0, -10, 0], ["furni:10", "n"], [100, 200], [0, 200]).IntParams.Length);
        Assert.Equal(10, Compile("wf_cnd_var_age_match", [0, 0, 0, 0, 600, 1, 0, -10], ["furni:10", "n"], [100, 200], [0, 200]).IntParams.Length);
    }

    // The reference variable of Give Variable is read through its own (second) source selector, not the destination's.
    [Fact]
    public void GiveVariableReadsItsReferenceThroughTheSecondSelector()
    {
        var (module, frame, furni, user) = Vars(TimeProvider.System);
        var executors = new WiredVariableExecutors(module, TimeProvider.System);
        Assert.True(module.Mutate(new(WiredVariableTarget.Furni, "custom:10"), furni, WiredVariableMutation.Give, 42, frame));
        // AIR targets: 1 user, 0 furni. Destination: the triggering user (0); reference: the selector pool (200).
        var give = Compile("wf_act_give_var", [1, 0, 0, 0, 1, 0, 0], ["user:12", "furni:10"], [0, 200], [0, 200]);
        Assert.Equal(10, give.IntParams.Length);

        Assert.True(executors.Execute("wf_act_give_var", give, frame));
        Assert.Equal(42, module.Read(new(WiredVariableTarget.User, "custom:12"), user, frame)!.Value);
        // Pointing the reference at the triggering furniture finds nothing to read: the reference selector decides.
        var none = Compile("wf_act_give_var", [1, 0, 0, 0, 1, 0, 0], ["user:12", "furni:10"], [0, 0], [0, 0]);
        Assert.True(module.Mutate(new(WiredVariableTarget.User, "custom:12"), user, WiredVariableMutation.Remove, 0, frame));
        Assert.False(executors.Execute("wf_act_give_var", none, frame));
    }

    private sealed class Directory : IWiredVariableDirectory
    {
        public WiredVariableDefinition? Find(uint itemId) => itemId switch
        {
            FurniVariable => new(itemId, 1, 5, "r80vmarker", WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, true),
            UserVariable => new(itemId, 1, 5, "r80vuser", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, false),
            12 => new(itemId, 1, 5, "r80vcopy", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            _ => null
        };
        public uint? GetRoomOwner(uint roomId) => 5;
    }
}
