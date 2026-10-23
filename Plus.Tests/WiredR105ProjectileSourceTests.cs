using Xunit;
using static Plus.Tests.WiredR102EditorSave;

namespace Plus.Tests;

// Projectile user sources as Habbo stores them. A save that names fewer user sources than the three slots keeps the frame default
// for each one it leaves out.
public sealed class WiredR105ProjectileSourceTests
{
    private static readonly int[] Frame = [0, 0, 0, 0, 500, -20, 1, 1, 0, 4, 0, 0, 1, 1, 0, 0, 1, -20, 50];

    [Fact]
    public void OneUserSourceFillsTheOtherSlotsFromTheFrame()
    {
        var saved = Save("wf_xtra_rotate_to_dir", Frame, users: [0]);

        Assert.True(saved.Accepted, Show(saved));
        Assert.Equal([0, 0, 0], saved.Users);
    }

    [Fact]
    public void NoUserSourcesKeepTheFrameDefaults()
    {
        var saved = Save("wf_xtra_rotate_to_dir", Frame, users: []);

        Assert.True(saved.Accepted, Show(saved));
        Assert.Equal([0, 0, 0], saved.Users);
    }

    [Fact]
    public void AMissingSlotDoesNotHideAnUnofferedSource()
    {
        var saved = Save("wf_xtra_rotate_to_dir", Frame, users: [100]);

        Assert.False(saved.Accepted);
        Assert.Equal("wiredfurni.error.invalid_user_source", saved.Key);
    }

    [Fact]
    public void FourUserSourcesAreStillRefused() => Assert.False(Save("wf_xtra_rotate_to_dir", Frame, users: [0, 0, 0, 0]).Accepted);
}
