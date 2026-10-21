using Xunit;

namespace Plus.Tests;

// The user selectors and the Filter Users add-on, saved through the real packet path and read back as the editor shows them.
// Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredUserSelectorEditorTests
{
    private static WiredRawSave.Result Saved(string name, int[]? ints = null, string text = "", int[]? furni = null, int[]? users = null,
        string[]? variables = null)
    {
        using var save = new WiredRawSave(name);
        var result = save.Save(ints: ints, text: text, furni: furni, users: users, variables: variables);
        Assert.True(result.Accepted, $"{name} [{string.Join(',', ints ?? [])}] '{text}': {result}");

        return result;
    }

    private static void Expect(string name, int[] sent, int[] stored, string? storedText = "", string text = "")
    {
        var result = Saved(name, sent, text);
        Assert.True(stored.SequenceEqual(result.Ints), $"{name} [{string.Join(',', sent)}] stored [{string.Join(',', result.Ints)}], expected [{string.Join(',', stored)}]");
        Assert.Equal(storedText, result.Text);
    }

    [Fact]
    public void UsersByTeamKeepsZeroToFourAndReadsEveryOtherNumberAsTheNearestEnd()
    {
        const string name = "wf_slc_users_team";

        foreach (var team in new[] { 0, 1, 2, 3, 4 }) {
            Expect(name, [team], [team]);
        }

        foreach (var team in new[] { 5, 6, 9, 100, int.MaxValue }) {
            Expect(name, [team], [4]);
        }

        foreach (var team in new[] { -1, int.MinValue }) {
            Expect(name, [team], [0]);
        }

        Expect(name, [], [0]);
        Expect(name, [2, 3], [2]);
        Expect(name, [2], [2], "", "abc");
    }

    [Fact]
    public void UsersByNeighborhoodStoresTheReferenceTheRootAndTheMask()
    {
        const string name = "wf_slc_users_neighborhood";
        int[] Words(int reference, int x, int y, params int[] words) => [reference, x, y, .. words, .. new int[15 - words.Length]];

        foreach (var (sent, stored) in new[] { (0, 0), (1, 1), (2, 0), (-1, 0) }) {
            Expect(name, Words(sent, 0, 0, 1), Words(stored, 0, 0, 1)[..17].Concat([0]).ToArray());
        }

        foreach (var (sent, stored) in new[] { (-65, -64), (-64, -64), (-1, -1), (1, 1), (64, 64), (65, 64), (int.MaxValue, 64), (int.MinValue, -64) }) {
            Expect(name, Words(1, sent, sent, 1), [1, stored, stored, 1, .. new int[13], 0]);
        }

        // The last mask word keeps its 25 tile bits; the other words are kept as sent.
        Expect(name, [1, 0, 0, int.MinValue, 33554432, .. Enumerable.Repeat(-1, 12)], [1, 0, 0, int.MinValue, 33554432, .. Enumerable.Repeat(-1, 11), 33554431, 0]);
        Expect(name, [1, 2, 3], [1, 2, 3, 33554431, .. new int[13], 0]);
        Expect(name, [], [1, 0, 0, 33554431, .. new int[13], 0]);
    }

    [Fact]
    public void UsersByNeighborhoodTakesTheFurniSourcesAndUserSourcesTheEditorOffers()
    {
        const string name = "wf_slc_users_neighborhood";

        foreach (var source in new[] { 0, 100, 201 }) {
            Assert.Equal([source], Saved(name, furni: [source]).Furni);
        }

        foreach (var source in new[] { 0, 201, 11, 10 }) {
            Assert.Equal([source], Saved(name, users: [source]).Users);
        }

        foreach (var source in new[] { 101, 200, 901 }) {
            using var save = new WiredRawSave(name);
            Assert.Equal("wiredfurni.error.invalid_furni_source", save.Save(furni: [source]).Key);
        }

        foreach (var source in new[] { 100, 200 }) {
            using var save = new WiredRawSave(name);
            Assert.Equal("wiredfurni.error.invalid_user_source", save.Save(users: [source]).Key);
        }
    }

    [Fact]
    public void UsersFromSignalStoresNoIntsAndNoSources()
    {
        const string name = "wf_slc_users_signal";

        Expect(name, [1, 2], []);
        Expect(name, [], [], "", "abc");

        foreach (var source in new[] { 0, 100, 201 }) {
            using var save = new WiredRawSave(name);
            Assert.Equal("wiredfurni.error.invalid_furni_source_count", save.Save(furni: [source]).Key);
        }

        foreach (var source in new[] { 0, 201, 10 }) {
            using var save = new WiredRawSave(name);
            Assert.Equal("wiredfurni.error.invalid_user_source_count", save.Save(users: [source]).Key);
        }

    }

    [Fact]
    public void UsersWithVariableStoresTheComparisonTheValueFilterAndTheReference()
    {
        const string name = "wf_slc_users_with_var";

        foreach (var comparison in new[] { 0, 1, 2, 3, 4, 5 }) {
            Expect(name, [comparison, 0, 0, 0, -10, 0], [comparison, 0, 0, 0, -10, 0]);
        }

        foreach (var comparison in new[] { 6, 7, 100, int.MaxValue, int.MinValue, -1 }) {
            Expect(name, [comparison, 0, 0, 0, -10, 0], [0, 0, 0, 0, -10, 0]);
        }

        foreach (var (sent, stored) in new[] { (-1, 0), (0, 0), (1, 1), (2, 2), (3, 2), (7, 2), (100, 2), (int.MaxValue, 2), (int.MinValue, 0) }) {
            Expect(name, [2, sent, 0, 0, -10, 0], [2, stored, 0, 0, -10, 0]);
        }

        // The reference value is stored as sent, in both ints.
        foreach (var value in new[] { -1, 0, 1, 2, 7, 100, int.MaxValue, int.MinValue }) {
            Expect(name, [2, 0, value, 0, -10, 0], [2, 0, value, 0, -10, 0]);
            Expect(name, [2, 0, 0, value, -10, 0], [2, 0, 0, value, -10, 0]);
        }

        foreach (var (sent, stored) in new[] { (-30, 0), (-20, -20), (-10, -10), (-1, 0), (0, 0), (1, 1), (2, 0), (5, 0) }) {
            Expect(name, [2, 0, 0, 0, sent, 0], [2, 0, 0, 0, stored, 0]);
        }

        foreach (var (sent, stored) in new[] { (-1, 1), (0, 0), (1, 1), (2, 1), (100, 1), (int.MaxValue, 1), (int.MinValue, 1) }) {
            Expect(name, [2, 0, 0, 0, -10, sent], [2, 0, 0, 0, -10, stored]);
        }

        Expect(name, [], [2, 0, 0, 0, -10, 0]);
        Expect(name, [1], [1, 0, 0, 0, -10, 0]);
        Expect(name, [1, 1, 1, 1, 1, 1, 1, 1], [1, 1, 1, 1, 1, 1]);
    }

    // A save with one variable keeps it in the first slot and leaves the second one empty; its ints are stored as with two variables.
    [Fact]
    public void UsersWithVariableStoresASaveWithOneVariable()
    {
        const string name = "wf_slc_users_with_var";

        foreach (var (sent, stored) in new (int[], int[])[]
        {
            ([0, 0, 0, 0, -10, 0], [0, 0, 0, 0, -10, 0]),
            ([5, 0, 0, 0, -10, 0], [5, 0, 0, 0, -10, 0]),
            ([6, 0, 0, 0, -10, 0], [0, 0, 0, 0, -10, 0]),
            ([int.MinValue, 0, 0, 0, -10, 0], [0, 0, 0, 0, -10, 0]),
            ([2, -1, 0, 0, -10, 0], [2, 0, 0, 0, -10, 0]),
            ([2, 1, 0, 0, -10, 0], [2, 1, 0, 0, -10, 0]),
            ([2, 3, 0, 0, -10, 0], [2, 2, 0, 0, -10, 0]),
            ([2, int.MaxValue, 0, 0, -10, 0], [2, 2, 0, 0, -10, 0]),
            ([2, 0, -1, 0, -10, 0], [2, 0, -1, 0, -10, 0]),
            ([2, 0, int.MinValue, 0, -10, 0], [2, 0, int.MinValue, 0, -10, 0]),
            ([2, 0, 0, int.MaxValue, -10, 0], [2, 0, 0, int.MaxValue, -10, 0]),
            ([2, 0, 0, 0, -30, 0], [2, 0, 0, 0, 0, 0]),
            ([2, 0, 0, 0, -20, 0], [2, 0, 0, 0, -20, 0]),
            ([2, 0, 0, 0, 1, 0], [2, 0, 0, 0, 1, 0]),
            ([2, 0, 0, 0, 5, 0], [2, 0, 0, 0, 0, 0]),
            ([2, 0, 0, 0, -10, -1], [2, 0, 0, 0, -10, 1]),
            ([2, 0, 0, 0, -10, 1], [2, 0, 0, 0, -10, 1]),
            ([2, 0, 0, 0, -10, 100], [2, 0, 0, 0, -10, 1]),
            ([], [2, 0, 0, 0, -10, 0]),
            ([1], [1, 0, 0, 0, -10, 0]),
            ([1, 1, 1, 1, 1, 1, 1, 1], [1, 1, 1, 1, 1, 1]),
            ([2, 1, 5, 0, 0, 0], [2, 1, 5, 0, 0, 0]),
            ([2, 2, 0, 0, -20, 0], [2, 2, 0, 0, -20, 0])
        }) {
            using var save = new WiredRawSave(name);
            var result = save.Save(ints: sent, variables: ["user:11"]);

            Assert.True(result.Accepted, $"[{string.Join(',', sent)}]: {result}");
            Assert.Equal(stored, result.Ints);
            Assert.Equal(["user:11", "0"], result.Variables);
        }
    }

    [Fact]
    public void UsersWithVariableStoresAnEmptyVariableAsTwoEmptySlots()
    {
        foreach (var variable in new[] { "0", "" }) {
            using var save = new WiredRawSave("wf_slc_users_with_var");
            var result = save.Save(ints: [2, 1, 5, 0, 0, 0], variables: [variable]);

            Assert.True(result.Accepted, $"'{variable}': {result}");
            Assert.Equal([2, 1, 5, 0, 0, 0], result.Ints);
            Assert.Equal(["0", "0"], result.Variables);
        }
    }

    [Fact]
    public void UsersWithVariableKeepsTheUserSourceOfASaveWithOneVariable()
    {
        foreach (var source in new[] { 0, 201, 11, 10 }) {
            using var save = new WiredRawSave("wf_slc_users_with_var");
            var result = save.Save(users: [source], variables: ["user:11"]);

            Assert.True(result.Accepted, $"{source}: {result}");
            Assert.Equal([source], result.Users);
        }

        foreach (var source in new[] { 100, 200 }) {
            using var save = new WiredRawSave("wf_slc_users_with_var");
            Assert.Equal("wiredfurni.error.invalid_user_source", save.Save(users: [source], variables: ["user:11"]).Key);
        }
    }

    [Fact]
    public void StoredRowsOfTheseBoxesKeepLoading()
    {
        foreach (var (name, ints) in new (string, int[])[]
        {
            ("wf_slc_users_team", [3]),
            ("wf_slc_users_signal", []),
            ("wf_slc_users_neighborhood", [1, 0, 0, 33554431, .. new int[13], 0]),
            ("wf_slc_users_with_var", [2, 1, 5, 7, 0, 0]),
            ("wf_slc_users_with_var", [3, 2, 0, 0, -20, 0]),
            ("wf_xtra_filter_users", [10, 0, 1])
        }) {
            Assert.True(Plus.HabboHotel.Items.Wired.Configuration.WiredBoxRegistry.TryGet(name, out var descriptor));
            var native = Plus.HabboHotel.Items.Wired.Configuration.WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [.. ints] };

            Assert.True(Plus.HabboHotel.Items.Wired.Configuration.WiredNativeEditorProjection.TryCompile(1, descriptor, native, out var runtime, storedRow: true), name);
            Assert.True(ints.SequenceEqual(runtime.Origin!.Native.OwnedIntParams), name);
        }
    }

    [Fact]
    public void AStoredUsersWithVariableRowWithTwoVariablesKeepsLoading()
    {
        Assert.True(Plus.HabboHotel.Items.Wired.Configuration.WiredBoxRegistry.TryGet("wf_slc_users_with_var", out var descriptor));
        var native = Plus.HabboHotel.Items.Wired.Configuration.WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [1, 1, 0, 8, -10, 0],
            VariableIds = ["user:11", "n"]
        };

        Assert.True(Plus.HabboHotel.Items.Wired.Configuration.WiredNativeEditorProjection.TryCompile(1, descriptor, native, out var runtime, storedRow: true));
        Assert.Equal([1, 1, 0, 8, -10, 0], runtime.Origin!.Native.OwnedIntParams.ToArray());
        Assert.Equal(["user:11", "n"], runtime.Origin.Native.VariableIds.ToArray());
    }

    [Fact]
    public void FilterUsersKeepsAnAmountOfOneTo1000AndFitsTheOtherInts()
    {
        const string name = "wf_xtra_filter_users";

        foreach (var (sent, stored) in new[] { (0, 1), (1, 1), (2, 2), (10, 10), (1000, 1000), (1001, 1000), (100000, 1000), (-1, 1), (int.MaxValue, 1000), (int.MinValue, 1) }) {
            Expect(name, [sent, 0, 1], [stored, 0, 1]);
        }

        foreach (var (sent, stored) in new[] { (0, 0), (1, 1), (2, 1), (3, 1), (100, 1), (-1, 0) }) {
            Expect(name, [10, sent, 1], [10, stored, 1]);
        }

        foreach (var (sent, stored) in new[] { (0, 0), (1, 1), (-10, -10), (-20, -20), (2, 0), (3, 0), (-5, 0), (99, 0), (-1, 0) }) {
            Expect(name, [10, 0, sent], [10, 0, stored]);
        }

        Expect(name, [], [10, 0, 1]);
        Expect(name, [5], [5, 0, 1]);
        Expect(name, [5, 1], [5, 1, 1]);
        Expect(name, [5, 1, -20, 9], [5, 1, -20]);
    }
}
