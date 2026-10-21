using Xunit;
using static Plus.Tests.WiredR102EditorSave;

namespace Plus.Tests;

// Projectile (wf_xtra_rotate_to_dir) editor saves, read back the way Habbo stores them. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredR102ProjectileEditorTests
{
    private static readonly int[] Frame = [0, 0, 0, 0, 500, -20, 1, 1, 0, 4, 0, 0, 1, 1, 0, 0, 1, -20, 50];
    private static readonly int[] Defaults = [1, 1, 0, 0, 100, -20, 1, 1, 0, 4, 0, 0, 1, 1, 0, 0, 1, -20, 50];

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, 1)]
    [InlineData(0, 2, 1)]
    [InlineData(1, 0, 0)]
    [InlineData(1, 3, 3)]
    [InlineData(1, 4, 0)]
    [InlineData(1, -1, 0)]
    public void RotateProjectileReadsTwoAsOneAndTheDirectionSystemStoresZeroToThree(int index, int sent, int stored) =>
        Assert.Equal(stored, Saved(With(index, sent)).Ints[index]);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 1)]
    [InlineData(100000, 100000)]
    [InlineData(100001, 100000)]
    public void TimePerTileStoresOneToOneHundredThousand(int sent, int stored) => Assert.Equal(stored, Saved(With(4, sent)).Ints[4]);

    [Theory]
    [InlineData(5, 0, 0)]
    [InlineData(5, 1, 1)]
    [InlineData(5, -10, -10)]
    [InlineData(5, -20, -20)]
    [InlineData(5, 5, 0)]
    [InlineData(17, 0, 0)]
    [InlineData(17, 1, 1)]
    [InlineData(17, -10, -10)]
    [InlineData(17, -20, -20)]
    [InlineData(17, 5, 0)]
    public void TheTimeAndDistanceTargetsStoreAnUnknownTargetAsZero(int index, int sent, int stored) => Assert.Equal(stored, Saved(With(index, sent)).Ints[index]);

    [Theory]
    [InlineData(9, 0, 0)]
    [InlineData(9, 100000, 100000)]
    [InlineData(9, 100001, 100000)]
    [InlineData(9, -1, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(10, 7, 7)]
    [InlineData(10, 8, 7)]
    [InlineData(10, -1, 0)]
    [InlineData(14, 0, 0)]
    [InlineData(14, 2, 2)]
    [InlineData(14, 3, 2)]
    [InlineData(14, -1, 0)]
    [InlineData(16, -64, -64)]
    [InlineData(16, 64, 64)]
    [InlineData(16, 65, 64)]
    [InlineData(16, -65, -64)]
    [InlineData(18, -1000, -1000)]
    [InlineData(18, 1000, 1000)]
    [InlineData(18, 1001, 1000)]
    [InlineData(18, -1001, -1000)]
    public void SpeedOffsetDistanceModeDistanceAndCurveClampToTheirRange(int index, int sent, int stored) => Assert.Equal(stored, Saved(With(index, sent)).Ints[index]);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(255)]
    [InlineData(-1)]
    public void InternalVariableFlagsAreStoredAsSent(int flags) => Assert.Equal(flags, Saved(With(11, flags)).Ints[11]);

    [Fact]
    public void ProjectileFillsMissingIntsFromTheFrameAndDropsAnExtraOne()
    {
        Assert.Equal(Defaults, Saved([]).Ints);
        Assert.Equal(Defaults, Saved([1, 1]).Ints);
        Assert.Equal(Frame, Saved([.. Frame, 7]).Ints);
    }

    [Fact]
    public void ProjectileRefusesAUserSourceItDoesNotOfferWithTheUserSourceKey()
    {
        var refused = Save("wf_xtra_rotate_to_dir", Frame, users: [100, 0, 0]);
        Assert.False(refused.Accepted);
        Assert.Equal("wiredfurni.error.invalid_user_source", refused.Key);

        foreach (var users in new[] { new[] { 0, 0, 0 }, [200, 200, 200], [201, 0, 0] }) {
            Assert.Equal(users, Saved(Frame, users: users).Users);
        }

        foreach (var furni in new[] { new[] { 901, 0, 0 }, [0, 100, 200], [100, 100, 100] }) {
            Assert.Equal(furni, Saved(Frame, furni: furni).Furni);
        }
    }

    private static int[] With(int index, int value)
    {
        var ints = (int[])Frame.Clone();
        ints[index] = value;

        return ints;
    }

    private static Result Saved(int[] ints, int[]? furni = null, int[]? users = null)
    {
        var result = Save("wf_xtra_rotate_to_dir", ints, furni, users);
        Assert.True(result.Accepted, Show(result));

        return result;
    }
}
