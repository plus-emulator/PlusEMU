using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

// What the user selectors pick in a real firing. The owner (7, "Alice") stands at (0, 0) and Bob (8) at (2, 0).
public partial class ModernWiredRuntimeTests
{
    private static int[] PickedUsers(string name, string row, Action<SelectorStack>? setup = null)
    {
        using var s = new SelectorStack();
        setup?.Invoke(s);
        s.Selector(name, row);

        return s.Users(s.Fire());
    }

    // Team numbers: 0 any team, 1 Red, 2 Green, 3 Blue, 4 Yellow; 5 and 6 act as 4 and -1 acts as 0. "Any team" needs a membership.
    [Theory]
    [InlineData(0, 3, new[] { 7 })]
    [InlineData(3, 3, new[] { 7 })]
    [InlineData(1, 3, new int[0])]
    [InlineData(4, 4, new[] { 7 })]
    [InlineData(5, 4, new[] { 7 })]
    [InlineData(6, 4, new[] { 7 })]
    [InlineData(5, 3, new int[0])]
    [InlineData(100, 4, new[] { 7 })]
    [InlineData(-1, 2, new[] { 7 })]
    [InlineData(-1, 0, new int[0])]
    [InlineData(0, 0, new int[0])]
    public void UsersByTeamSelectsTheMembersOfTheNamedTeam(int sent, int ownerTeam, int[] expected) =>
        Assert.Equal(expected, PickedUsers("wf_slc_users_team", Row(3, $"[{sent}]", false, false), s => s.F.User.Team = (Team)ownerTeam));

    [Theory]
    [InlineData(0, 0, new[] { 7, 8 })]
    [InlineData(0, 3, new[] { 8 })]
    [InlineData(3, 3, new[] { 8 })]
    [InlineData(1, 3, new[] { 7, 8 })]
    public void UsersByTeamWithInvertSelectsEveryoneOutsideTheTeam(int sent, int ownerTeam, int[] expected) =>
        Assert.Equal(expected, PickedUsers("wf_slc_users_team", Row(3, $"[{sent}]", false, true), s => s.F.User.Team = (Team)ownerTeam));

    [Fact]
    public void UsersByTeamAsAFilterAloneSelectsNobody() =>
        Assert.Empty(PickedUsers("wf_slc_users_team", Row(3, "[3]", true, false), s => s.F.User.Team = Team.Blue));

    // The area covers x to x + width - 1 and y to y + height - 1. Each value is clamped to 0..64 when saved.
    [Theory]
    [InlineData("0,0,1,1", new[] { 7 })]
    [InlineData("0,0,2,1", new[] { 7 })]
    [InlineData("1,0,1,1", new int[0])]
    [InlineData("2,0,1,1", new[] { 8 })]
    [InlineData("0,0,3,1", new[] { 7, 8 })]
    [InlineData("0,0,0,1", new int[0])]
    [InlineData("0,0,1,0", new int[0])]
    [InlineData("-1,-1,3,3", new[] { 7, 8 })]
    [InlineData("0,0,65,65", new[] { 7, 8 })]
    [InlineData("65,65,1,1", new int[0])]
    public void UsersInAreaSelectsTheUsersInsideTheInclusiveBounds(string area, int[] expected) =>
        Assert.Equal(expected, PickedUsers("wf_slc_users_area", Row(13, $"[{area}]", false, false)));

    [Fact]
    public void UsersInAreaWithInvertSelectsTheUsersOutsideAndAFilterAloneSelectsNobody()
    {
        Assert.Equal([8], PickedUsers("wf_slc_users_area", Row(13, "[0,0,1,1]", false, true)));
        Assert.Empty(PickedUsers("wf_slc_users_area", Row(13, "[0,0,1,1]", true, false)));
    }

    // The window is built round the trigger user. The centre bit (1) holds the user. A root offset moves the window against its sign.
    private static string Window(int reference, int rootX, int rootY, long firstWord, bool allWords = false, string users = "[0]")
    {
        var words = Enumerable.Repeat(0L, 14).ToArray();
        words[0] = firstWord;

        if (allWords) {
            words = [.. Enumerable.Repeat(-1L, 13), 33554431];
        }

        return Row(12, $"[{reference},{rootX},{rootY},{string.Join(",", words)}]", false, false, furni: "[100]", users: users);
    }

    [Theory]
    [InlineData(1, 0, 0, 1L, new[] { 7 })]
    [InlineData(1, 0, 0, 2L, new int[0])]
    [InlineData(1, 0, 0, 33554430L, new[] { 8 })]
    [InlineData(1, 0, 0, 511L, new[] { 7 })]
    [InlineData(1, -1, 0, 1L, new int[0])]
    [InlineData(1, -1, 0, 511L, new[] { 7, 8 })]
    [InlineData(1, 1, 0, 2L, new[] { 7 })]
    [InlineData(1, 0, 0, 0L, new int[0])]
    [InlineData(1, 64, 64, 1L, new int[0])]
    [InlineData(1, 200, 200, 1L, new int[0])]
    [InlineData(0, 0, 0, 1L, new int[0])]
    public void UsersByNeighborhoodSelectsTheUsersInTheWindowOfTheTriggerUser(int reference, int rootX, int rootY, long word, int[] expected) =>
        Assert.Equal(expected, PickedUsers("wf_slc_users_neighborhood", Window(reference, rootX, rootY, word)));

    [Fact]
    public void UsersByNeighborhoodWithEveryBitSetSelectsEveryone() =>
        Assert.Equal([7, 8], PickedUsers("wf_slc_users_neighborhood", Window(1, 0, 0, -1, allWords: true)));

    [Fact]
    public void UsersByNeighborhoodAnchoredOnAnEmptySignalSelectsNobody() =>
        Assert.Empty(PickedUsers("wf_slc_users_neighborhood", Window(1, 0, 0, 511, users: "[201]")));

    [Fact]
    public void UsersByNeighborhoodWithInvertSelectsTheUsersOutsideTheWindow() =>
        Assert.Equal([8], PickedUsers("wf_slc_users_neighborhood", Window(1, 0, 0, 1).Replace("\"Inverse\":false", "\"Inverse\":true")));

    // A user without the variable is never selected, not even by "not equal". The value 0 counts as present.
    private static int[] WithScore(string ints, int? ownerValue, bool invert = false) => PickedUsers("wf_slc_users_with_var",
        Row(18, $"[{ints}]", false, invert, furni: "[100]", users: "[0]", variables: """["user:11","n"]"""),
        s =>
        {
            if (ownerValue is { } value) {
                s.Give(s.F.User, value);
            }
        });

    [Theory]
    [InlineData("2,0,0,0,-10,0", 8, new[] { 7 })]
    [InlineData("2,0,0,0,-10,0", null, new int[0])]
    [InlineData("2,0,0,0,-10,0", 0, new[] { 7 })]
    [InlineData("2,1,0,3,-10,0", 8, new[] { 7 })]
    [InlineData("2,1,0,8,-10,0", 8, new int[0])]
    [InlineData("1,1,0,8,-10,0", 8, new[] { 7 })]
    [InlineData("0,1,0,8,-10,0", 8, new int[0])]
    [InlineData("0,1,0,9,-10,0", 8, new[] { 7 })]
    [InlineData("5,1,0,8,-10,0", 8, new[] { 7 })]
    [InlineData("3,1,0,8,-10,0", 8, new[] { 7 })]
    [InlineData("4,1,0,8,-10,0", 8, new int[0])]
    [InlineData("4,1,0,99,-10,0", 8, new[] { 7 })]
    [InlineData("4,1,0,99,-10,0", null, new int[0])]
    [InlineData("1,1,0,0,-10,0", 0, new[] { 7 })]
    [InlineData("0,1,0,0,-10,0", -5, new[] { 7 })]
    [InlineData("1,1,-1,-5,-10,0", -5, new[] { 7 })]
    [InlineData("1,1,0,99,-10,0", null, new int[0])]
    public void UsersWithVariableSelectsOnlyTheHoldersWhoseValuePasses(string ints, int? ownerValue, int[] expected) =>
        Assert.Equal(expected, WithScore(ints, ownerValue));

    [Theory]
    [InlineData("2,0,0,0,-10,0", 8, new[] { 8 })]
    [InlineData("2,0,0,0,-10,0", null, new[] { 7, 8 })]
    [InlineData("1,1,0,8,-10,0", 8, new[] { 8 })]
    [InlineData("1,1,0,99,-10,0", 8, new[] { 7, 8 })]
    public void UsersWithVariableWithInvertSelectsEveryoneWhoDoesNotPass(string ints, int? ownerValue, int[] expected) =>
        Assert.Equal(expected, WithScore(ints, ownerValue, invert: true));

    // A raw save with one variable, then the form it was stored in fired in a room: only the user whose value passes is selected.
    [Theory]
    [InlineData(8, new[] { 7 })]
    [InlineData(9, new int[0])]
    public void UsersWithVariableSavedWithOneVariableSelectsTheHoldersWhoseValuePasses(int ownerValue, int[] expected)
    {
        using var save = new WiredRawSave("wf_slc_users_with_var");
        var stored = save.Save(ints: [1, 1, 0, 8, -10, 0], variables: ["user:11"]);
        Assert.True(stored.Accepted, stored.ToString());

        var row = Row(18, $"[{string.Join(',', stored.Ints)}]", false, false, furni: $"[{string.Join(',', stored.Furni)}]",
            users: $"[{string.Join(',', stored.Users)}]", variables: $"[{string.Join(',', stored.Variables!.Select(id => $"\"{id}\""))}]");

        Assert.Equal(expected, PickedUsers("wf_slc_users_with_var", row, s => s.Give(s.F.User, ownerValue)));
    }

    private static string JsonEscaped(string text) => text.Replace("\t", "\\t").Replace("\n", "\\n");

    // Names match whole and without regard to case. Only a tab separates them.
    [Theory]
    [InlineData("alice", new[] { 7 })]
    [InlineData("ALICE", new[] { 7 })]
    [InlineData("Alice\tNobody", new[] { 7 })]
    [InlineData("Nobody\tAlice", new[] { 7 })]
    [InlineData("Alice\tAlice", new[] { 7 })]
    [InlineData("Nobody,Alice", new int[0])]
    [InlineData("Nobody\nAlice", new int[0])]
    [InlineData("Nobody Alice", new int[0])]
    [InlineData(" Alice", new int[0])]
    [InlineData("Alice ", new int[0])]
    [InlineData("Alic", new int[0])]
    [InlineData("lice", new int[0])]
    [InlineData("Nobody", new int[0])]
    [InlineData("", new int[0])]
    public void UsersByNameSelectsTheWholeNamesSplitOnTabs(string names, int[] expected) =>
        Assert.Equal(expected, PickedUsers("wf_slc_users_byname", UsersByName(JsonEscaped(names))));

    [Fact]
    public void UsersByNameWithInvertSelectsTheOtherUsers()
    {
        Assert.Equal([8], PickedUsers("wf_slc_users_byname", UsersByName("alice", invert: true)));
        Assert.Equal([7, 8], PickedUsers("wf_slc_users_byname", UsersByName("Nobody", invert: true)));
        Assert.Empty(PickedUsers("wf_slc_users_byname", UsersByName("alice", filter: true)));
    }
}
