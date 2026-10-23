using Xunit;

namespace Plus.Tests;

// Set Furni Visibility is either visible (0) or invisible (100).
public partial class ModernWiredRuntimeTests
{
    private const string VisibilityKey = "wiredfurni.error.set_furni_transparency.";

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(51)]
    [InlineData(75)]
    [InlineData(99)]
    [InlineData(101)]
    [InlineData(127)]
    [InlineData(255)]
    [InlineData(1000)]
    public async Task AVisibilityBetweenVisibleAndInvisibleIsRefused(int visibility)
    {
        var f = new MovementOracleSession(Visibility);

        await SaveEffect(f, FurniVisualSave([0, visibility, 0, 0, 0]));

        AssertSaved(f, false, VisibilityKey + "invalid_transparency");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task VisibleAndInvisibleAreAcceptedForNormalFurniToo(int visibility)
    {
        var f = new MovementOracleSession(Visibility);

        await SaveEffect(f, FurniVisualSave([0, visibility, 0, 0, 0], [8, 9]));

        AssertSaved(f, true, VisibilityKey + "invalid_transparency");
    }
}
