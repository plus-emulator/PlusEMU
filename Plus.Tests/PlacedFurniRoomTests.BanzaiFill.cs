using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void ClosingABanzaiRingScoresOnlyTheTilesTheFillLocks()
    {
        var tiles = new Dictionary<(int X, int Y), Item>();
        uint id = 100;

        for (var y = 0; y < 4; y++) {
            for (var x = 0; x < 4; x++) {
                tiles[(x, y)] = InteractionItem(id++, x, y, InteractionType.Banzaifloor);
            }
        }

        var banzai = _room.GetBanzai();
        banzai.BanzaiStart();
        var redLocked = tiles[(1, 1)];
        redLocked.Team = Team.Red;
        redLocked.Value = 3;
        redLocked.LegacyDataString = "5";
        var player = Viewer(0, 0);
        player.Team = Team.Blue;

        (int, int)[] ring = [(0, 0), (1, 0), (2, 0), (3, 0), (3, 1), (3, 2), (2, 2), (1, 2), (0, 2), (0, 1)];

        foreach (var (x, y) in ring) {
            player.X = x;
            player.Y = y;

            for (var step = 0; step < 3; step++) {
                banzai.OnUserWalk(player);
            }
        }

        Assert.Equal(ring.Length + 1, _room.GetGameManager().Points[(int)Team.Blue]);
        Assert.Equal((Team.Blue, 3, "11"), (tiles[(2, 1)].Team, tiles[(2, 1)].Value, tiles[(2, 1)].LegacyDataString));
        Assert.Equal((Team.Red, 3, "5"), (redLocked.Team, redLocked.Value, redLocked.LegacyDataString));
        Assert.Equal(0, tiles[(0, 3)].Value);
    }
}
