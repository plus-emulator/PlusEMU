using Xunit;

namespace Plus.Tests;

// Users In Area saved through the real packet path.
public sealed class WiredUsersAreaEditorTests
{
    private static void Expect(int[] sent, int[] stored, string text = "")
    {
        using var save = new WiredRawSave("wf_slc_users_area");
        var result = save.Save(ints: sent, text: text);
        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(stored, result.Ints);
        Assert.Equal("", result.Text);
    }

    [Fact]
    public void AMissingIntTakesTheFrameDefaultAndAnExtraIntIsDropped()
    {
        Expect([5, 5, 3], [5, 5, 3, 0]);
        Expect([], [0, 0, 0, 0]);
        Expect([1, 2, 3, 4, 5], [1, 2, 3, 4]);
        Expect([6, 10, 3, 1], [6, 10, 3, 1], text: "abc");
        Expect([-1, -1, 2, 2], [0, 0, 2, 2]);
        Expect([int.MaxValue, 65, int.MinValue, 64], [64, 64, 0, 64]);
    }
}
