using Xunit;

namespace Plus.Tests;

// Users By Name saved through the real packet path and read back as the editor shows it.
public sealed class WiredUsersByNameEditorTests
{
    private static WiredRawSave.Result Saved(string name, string text)
    {
        using var save = new WiredRawSave(name);
        var result = save.Save(text: text);
        Assert.True(result.Accepted, $"{name} '{text}': {result}");

        return result;
    }

    private static void Expect(string name, string sent, string stored, int[]? ints = null)
    {
        using var save = new WiredRawSave(name);
        var result = save.Save(ints: ints, text: sent);
        Assert.True(result.Accepted, $"{name} '{sent}': {result}");
        Assert.Equal(stored, result.Text);
        Assert.Empty(result.Ints);
    }

    [Fact]
    public void UsersByNameStoresLowerCaseNamesAndCutsTrailingTabs()
    {
        const string name = "wf_slc_users_byname";

        Expect(name, "Objecto", "objecto");
        Expect(name, "OBJECTO", "objecto");
        Expect(name, "", "");
        Expect(name, " ", " ");
        Expect(name, "Robbie\tObjecto", "robbie\tobjecto");
        Expect(name, "\tObjecto", "\tobjecto");
        Expect(name, "Objecto\t", "objecto");
        Expect(name, "Objecto\t\t", "objecto");
        Expect(name, "Objecto\t\tRobbie", "objecto\t\trobbie");
        Expect(name, "Objecto\nRobbie", "objecto\nrobbie");
        Expect(name, "Objecto,Robbie", "objecto,robbie");
        Expect(name, "Objecto Robbie", "objecto robbie");
        Expect(name, " Objecto", " objecto");
        Expect(name, "Objecto ", "objecto ");
        Expect(name, "<b>x</b>", "<b>x</b>");
        Expect(name, "ünï", "ünï");
        Expect(name, "Objecto", "objecto", ints: [1, 2]);
    }

    [Fact]
    public void UsersByNameRefusesANameOver30CharactersAndMoreThan20NamesWithTheirLimits()
    {
        const string name = "wf_slc_users_byname";

        Assert.Equal(new string('a', 30), Saved(name, new string('a', 30)).Text);

        foreach (var length in new[] { 31, 32, 100 }) {
            using var save = new WiredRawSave(name);
            var result = save.Save(text: new string('a', length));
            Assert.Equal("wiredfurni.error.username_length", result.Key);
            Assert.Equal("30", result.ErrorParameters!["max_username_length"]);
        }

        string Names(int count) => string.Join('\t', Enumerable.Range(0, count).Select(index => $"n{index:00}"));

        Assert.Equal(Names(1), Saved(name, Names(1)).Text);
        Assert.Equal(Names(19), Saved(name, Names(19)).Text);
        Assert.Equal(Names(20), Saved(name, Names(20)).Text);

        foreach (var count in new[] { 21, 22, 50 }) {
            using var save = new WiredRawSave(name);
            var result = save.Save(text: Names(count));
            Assert.Equal("wiredfurni.error.usernames_count", result.Key);
            Assert.Equal("20", result.ErrorParameters!["max_usernames"]);
        }

        // A comma, a space and a line break do not separate names, so they count as one name of their own length.
        Assert.Equal("a,b", Saved(name, "a,b").Text);
    }
}
