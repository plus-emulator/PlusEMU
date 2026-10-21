using Xunit;

namespace Plus.Tests;

// Selector saves as Habbo stores them: ints that are out of range are clamped or reset, ints that are missing take the frame
// default, extra ints are dropped, and a text the selector does not read is dropped. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredSaveLeniencySelectorTests
{
    private static WiredRawSave.Result Saved(string name, int[] ints, string text = "")
    {
        using var save = new WiredRawSave(name);
        var result = save.Save(ints: ints, text: text);
        Assert.True(result.Accepted, $"{name} [{string.Join(',', ints)}] '{text}': {result}");

        return result;
    }

    private static void Expect(string name, int[] sent, int[] stored, string text = "")
    {
        var result = Saved(name, sent, text);
        Assert.True(stored.SequenceEqual(result.Ints), $"{name} [{string.Join(',', sent)}] stored [{string.Join(',', result.Ints)}], expected [{string.Join(',', stored)}]");
        Assert.Equal("", result.Text);
    }

    [Fact]
    public void FurniAltitudeClampsTheAltitudeToZeroTo8000AndKeepsComparisonsZeroToFour()
    {
        const string name = "wf_slc_furni_altitude";

        foreach (var (sent, stored) in new[] { (0, 0), (50, 50), (8000, 8000), (8001, 8000), (100000, 8000), (int.MaxValue, 8000), (-1, 0), (int.MinValue, 0) }) {
            Expect(name, [sent, 0], [stored, 0]);
        }

        foreach (var comparison in new[] { 0, 1, 2, 3, 4 }) {
            Expect(name, [100, comparison], [100, comparison]);
        }

        foreach (var comparison in new[] { -1, 5, 100, int.MaxValue }) {
            Expect(name, [100, comparison], [100, 0]);
        }

        Expect(name, [], [0, 0]);
        Expect(name, [100], [100, 0]);
        Expect(name, [100, 1, 9], [100, 1]);
        Expect(name, [100, 1], [100, 1], "abc");
    }

    [Fact]
    public void FurniAreaClampsEachValueToZeroTo64AndFitsTheFourInts()
    {
        const string name = "wf_slc_furni_area";

        foreach (var (sent, stored) in new[] { (-1, 0), (int.MinValue, 0), (0, 0), (63, 63), (64, 64), (65, 64), (100, 64), (int.MaxValue, 64) }) {
            Expect(name, [sent, sent, sent, sent], [stored, stored, stored, stored]);
        }

        Expect(name, [5, 5, 3], [5, 5, 3, 0]);
        Expect(name, [], [0, 0, 0, 0]);
        Expect(name, [1, 2, 3, 4, 5], [1, 2, 3, 4]);
        Expect(name, [6, 10, 3, 1], [6, 10, 3, 1], "abc");
    }

    [Fact]
    public void FurniByTypeKeepsTheStateFlagOnlyForOne()
    {
        const string name = "wf_slc_furni_bytype";

        Expect(name, [1], [1]);
        Expect(name, [0], [0]);

        foreach (var sent in new[] { 2, -1, 100, int.MaxValue }) {
            Expect(name, [sent], [0]);
        }

        Expect(name, [], [0]);
        Expect(name, [1, 1], [1]);
        Expect(name, [1], [1], "abc");
    }

    [Fact]
    public void RemoteSelectorMapsTheTypeToZeroOrOneAndResetsANegativeCount()
    {
        const string name = "wf_slc_remote";

        Expect(name, [0, 0], [0, 0]);
        Expect(name, [1, 0], [1, 0]);

        foreach (var type in new[] { 2, 100 }) {
            Expect(name, [type, 0], [1, 0]);
        }

        Expect(name, [-1, 0], [0, 0]);
        Expect(name, [0, 100], [0, 100]);
        Expect(name, [0, int.MaxValue], [0, int.MaxValue]);
        Expect(name, [0, -1], [0, 0]);
        Expect(name, [0, int.MinValue], [0, 0]);
        Expect(name, [], [0, 0]);
        Expect(name, [1], [1, 0]);
        Expect(name, [1, 2, 3], [1, 2]);
        Expect(name, [0, 0], [0, 0], "abc");
    }

    [Fact]
    public void UsersByTypeKeepsOneToFourAndMapsTheRest()
    {
        const string name = "wf_slc_users_bytype";

        foreach (var type in new[] { 1, 2, 3, 4 }) {
            Expect(name, [type], [type]);
        }

        foreach (var type in new[] { 0, -1 }) {
            Expect(name, [type], [1]);
        }

        foreach (var type in new[] { 5, 6, 7, 8, 16, 100, int.MaxValue }) {
            Expect(name, [type], [4]);
        }

        Expect(name, [], [1]);
        Expect(name, [1, 4], [1]);
        Expect(name, [1], [1], "abc");
    }

    [Fact]
    public void UsersByActionKeepsTheStoredActionsAndResetsTheRestToZero()
    {
        const string name = "wf_slc_users_byaction";

        foreach (var action in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 67 }) {
            Expect(name, [action], [action]);
        }

        foreach (var action in new[] { 12, 13, 20, 66, 68, 100, 1000, -1, int.MaxValue }) {
            Expect(name, [action], [0]);
        }

        Expect(name, [], [0]);
        Expect(name, [0, 1], [0]);
    }

    [Theory]
    [InlineData(10, "abc")]
    [InlineData(10, "18")]
    [InlineData(10, "-1")]
    [InlineData(10, " 5")]
    [InlineData(11, "dance 5")]
    [InlineData(11, "abc")]
    [InlineData(0, "abc")]
    [InlineData(67, "abc")]
    public void UsersByActionKeepsAnyFilterTextAsSent(int action, string filter)
    {
        using var save = new WiredRawSave("wf_slc_users_byaction");
        var result = save.Save(ints: [action], text: filter);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(filter, result.Text);
        Assert.Equal([action], result.Ints);
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(200, 200)]
    [InlineData(201, 200)]
    [InlineData(1000, 200)]
    public void UsersByActionKeepsTheFirst200CharactersOfAFilterText(int sent, int stored)
    {
        using var save = new WiredRawSave("wf_slc_users_byaction");
        var result = save.Save(ints: [10], text: new string('1', sent));

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(new string('1', stored), result.Text);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("0", "0")]
    [InlineData("1", "1")]
    [InlineData("-1", "-1")]
    [InlineData("123456789", "123456789")]
    [InlineData("99999999999999", "99999999999999")]
    [InlineData("2147483648", "2147483648")]
    [InlineData("+5", "5")]
    [InlineData("007", "7")]
    [InlineData("abc", "")]
    [InlineData(" 5 ", "")]
    [InlineData("1,2", "")]
    [InlineData("1.5", "")]
    [InlineData("0x10", "")]
    public void UsersInGroupStoresTheGroupIdAsANumberOrClearsIt(string sent, string stored)
    {
        using var save = new WiredRawSave("wf_slc_users_group");
        var result = save.Save(ints: [], text: sent);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(stored, result.Text);
        Assert.Empty(result.Ints);
    }

    [Fact]
    public void UsersInGroupDropsIntsAndAHundredDigitIdIsCleared()
    {
        using var save = new WiredRawSave("wf_slc_users_group");

        var result = save.Save(ints: [1, 2], text: new string('9', 100));

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal("", result.Text);
        Assert.Empty(result.Ints);
    }

    [Fact]
    public void UsersWithHandItemStoresAnyHandItemAndFitsTheInts()
    {
        const string name = "wf_slc_users_handitem";

        foreach (var item in new[] { int.MinValue, -100, -1, 0, 1, 2, 27, 1129, 100000, int.MaxValue }) {
            Expect(name, [item], [item]);
        }

        Expect(name, [], [2]);
        Expect(name, [2, 3], [2]);
        Expect(name, [2], [2], "abc");
    }

    [Fact]
    public void FurniNeighborhoodKeepsEighteenIntsWithTheReferenceAndTheRootClamped()
    {
        const string name = "wf_slc_furni_neighborhood";
        int[] Mask(int reference, int x, int y, int word = 33554431) => [reference, x, y, word, .. new int[13]];
        int[] Stored(int reference, int x, int y, int word = 33554431) => [.. Mask(reference, x, y, word), 0];

        Expect(name, Mask(0, 0, 0), Stored(0, 0, 0));
        Expect(name, Mask(1, 0, 0), Stored(1, 0, 0));

        foreach (var reference in new[] { 2, -1 }) {
            Expect(name, Mask(reference, 0, 0), Stored(0, 0, 0));
        }

        foreach (var (sent, stored) in new[] { (-65, -64), (-64, -64), (-1, -1), (1, 1), (64, 64), (65, 64), (1000, 64), (int.MaxValue, 64), (int.MinValue, -64) }) {
            Expect(name, Mask(0, sent, 0), Stored(0, stored, 0));
            Expect(name, Mask(0, 0, sent), Stored(0, 0, stored));
        }

        foreach (var word in new[] { 0, 1, -1, int.MaxValue, int.MinValue, 33554432, 16777216 }) {
            Expect(name, Mask(0, 0, 0, word), Stored(0, 0, 0, word));
        }

        Expect(name, [], [1, 0, 0, 33554431, .. new int[14]]);
        Expect(name, [1], [1, 0, 0, 33554431, .. new int[14]]);
        Expect(name, [1, 2, 3], [1, 2, 3, 33554431, .. new int[14]]);
        Expect(name, [1, 2, 3, 5, 5, 5, 5, 5, 5, 5], [1, 2, 3, 5, 5, 5, 5, 5, 5, 5, .. new int[8]]);
        Expect(name, [1, 2, 3, .. Enumerable.Repeat(5, 15)], [1, 2, 3, .. Enumerable.Repeat(5, 14), 0]);
        Expect(name, [0, 0, 0, .. Enumerable.Repeat(-1, 14)], [0, 0, 0, .. Enumerable.Repeat(-1, 13), 33554431, 0]);
        Expect(name, [.. new int[16], -1], [.. new int[16], 33554431, 0]);
        Expect(name, [.. new int[16], 256], [.. new int[16], 256, 0]);
        Expect(name, Mask(1, 0, 0), Stored(1, 0, 0), "abc");
    }
}
