using Xunit;

namespace Plus.Tests;

public partial class ModernWiredBehaviorTests
{
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
