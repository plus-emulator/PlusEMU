using Xunit;

namespace Plus.Tests;

public partial class ModernWiredBehaviorTests
{
    // r92m r92m-sshot-extra (AIR WIN63-202610131318-860371703): a selected furni without its own entry went to A, C, B, B, C, C, B, B,
    // A over three spots and B, A, A, A over two; every spot was used each time.
    [Fact]
    public void ExtraSelectorFurniTakesARandomSavedSpotWhileEverySpotIsUsed()
    {
        var three = SnapshotRuntime(200, (1, 7), (2, 9), (3, 11));
        var two = SnapshotRuntime(200, (1, 7), (2, 9));
        var extraSpots = new HashSet<int>();

        for (var seed = 0; seed < 24; seed++) {
            var moved = RunSnapshot(three, new Random(seed), Item(1, 7, 2), Item(2, 9, 2), Item(3, 11, 2), Item(4, 8, 2));
            Assert.Equal([7, 9, 11], moved.Values.Distinct().Order());
            extraSpots.Add(moved[4]);

            var pair = RunSnapshot(two, new Random(seed), Item(1, 7, 2), Item(2, 9, 2), Item(4, 8, 2));
            Assert.Equal([7, 9], pair.Values.Distinct().Order());
        }

        Assert.Equal([7, 9, 11], extraSpots.Order());
    }
}
