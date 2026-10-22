using System.Drawing;
using Plus.HabboHotel.Rooms.Games.Banzai;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

// Map legend: '.' free arena tile, 'B' or 'R' arena tile locked by Blue or Red, '*' free arena tile Blue encloses, '#' no arena tile.
public sealed class BanzaiEnclosureTests
{
    [Fact]
    public void ASquareRingEnclosesItsInside() => AssertBlueEncloses(
        ".......",
        ".BBBBB.",
        ".B***B.",
        ".B***B.",
        ".BBBBB.",
        ".......");

    [Fact]
    public void ARingNeedsNoDiagonalCorners() => AssertBlueEncloses(
        "......",
        "..BB..",
        ".B**B.",
        ".B**B.",
        "..BB..",
        "......");

    [Fact]
    public void AnLShapedRingEnclosesTheWholeL() => AssertBlueEncloses(
        ".........",
        ".BBBB....",
        ".B**B....",
        ".B**BBBB.",
        ".B*****B.",
        ".BBBBBBB.",
        ".........");

    [Fact]
    public void AnOpenUShapeEnclosesNothing() => AssertBlueEncloses(
        ".....",
        ".B.B.",
        ".B.B.",
        ".BBB.",
        ".....");

    [Fact]
    public void AnotherTeamsLockedTilesAreNoWall() => AssertBlueEncloses(
        ".....",
        ".BBB.",
        ".B.R.",
        ".BBB.",
        ".....");

    [Fact]
    public void AnotherTeamsLockedTileInsideTheRingStaysOut() => AssertBlueEncloses(
        "......",
        ".BBBB.",
        ".B*RB.",
        ".BBBB.",
        "......");

    [Fact]
    public void ARingOnTheArenaEdgeEnclosesItsInside() => AssertBlueEncloses(
        "BBB",
        "B*B",
        "BBB");

    [Fact]
    public void AnAreaTouchingTheArenaEdgeIsNotEnclosed() => AssertBlueEncloses(
        "#####",
        "#BBB#",
        "#B.B#",
        "#B.##",
        "#BBB#");

    [Fact]
    public void AnAreaTouchingTheMapEdgeIsNotEnclosed() => AssertBlueEncloses(
        ".B..",
        ".B..",
        ".B..");

    private static void AssertBlueEncloses(params string[] map)
    {
        var arena = new HashSet<Point>();
        var lockedBy = new Dictionary<Point, Team>();
        var expected = new List<Point>();

        for (var y = 0; y < map.Length; y++) {
            for (var x = 0; x < map[y].Length; x++) {
                var point = new Point(x, y);

                if (map[y][x] == '#') {
                    continue;
                }

                arena.Add(point);

                switch (map[y][x]) {
                    case 'B':
                        lockedBy[point] = Team.Blue;
                        break;
                    case 'R':
                        lockedBy[point] = Team.Red;
                        break;
                    case '*':
                        expected.Add(point);
                        break;
                }
            }
        }

        var enclosed = BanzaiEnclosure.EnclosedTiles(arena, lockedBy, Team.Blue);

        Assert.Equal(Sorted(expected), Sorted(enclosed));
    }

    private static List<Point> Sorted(IEnumerable<Point> points) => [.. points.OrderBy(point => point.Y).ThenBy(point => point.X)];
}
