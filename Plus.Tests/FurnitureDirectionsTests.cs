using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public class FurnitureDirectionsTests
{
    private static ItemDefinition Definition(params int[] directions) => new() { ItemName = "test", Directions = directions };

    [Theory]
    [InlineData("wf_colortile", new[] { 0 })]
    [InlineData("wf_antenna2", new[] { 0, 2 })]
    [InlineData("throne", new[] { 0, 2, 4, 6 })]
    [InlineData("hween_c15_road", new[] { 0, 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData("bc_block_0_recolorable", new[] { 0, 2, 4, 6 })]
    public void TheGeneratedTableHoldsEachFurnisAssetDirections(string classname, int[] expected)
    {
        Assert.Equal(expected, FurnitureDirections.For(classname));
    }

    [Fact]
    public void ColourVariantsShareTheirBaseAssetAndUnknownFurniHaveNoDirections()
    {
        Assert.Equal(FurnitureDirections.For("throne"), FurnitureDirections.For("throne*3"));
        Assert.Empty(FurnitureDirections.For("no_such_furni"));
        Assert.Empty(FurnitureDirections.For(null));
    }

    [Fact]
    public void ParseReadsWiredUnitsAndSkipsCommentsAndBadValues()
    {
        var table = FurnitureDirections.Parse(new StringReader("# header\nchair\t4,0,2,6\nodd\t9,1\nnone\t\n"));

        Assert.Equal(new[] { 0, 2, 4, 6 }, table["chair"]);
        Assert.Equal(new[] { 1 }, table["odd"]);
        Assert.False(table.ContainsKey("none"));
    }

    // Observed on AIR WIN63-202610151745-318700621: flying west (6), wf_antenna2 (0, 2) turned to 2 and wf_colortile (0) kept 0.
    [Theory]
    [InlineData(new[] { 0 }, 6, null)]
    [InlineData(new[] { 0 }, 4, 0)]
    [InlineData(new[] { 0 }, 0, 0)]
    [InlineData(new[] { 0, 2 }, 6, 2)]
    [InlineData(new[] { 0, 2 }, 4, 0)]
    [InlineData(new[] { 0, 2 }, 2, 2)]
    [InlineData(new[] { 0, 2 }, 7, null)]
    [InlineData(new[] { 0, 2, 4, 6 }, 6, 6)]
    [InlineData(new[] { 0, 2, 4, 6 }, 7, null)]
    [InlineData(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 7, 7)]
    [InlineData(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 6, 6)]
    public void AHeadingSnapsToADirectionTheFurniHas(int[] directions, int heading, int? expected)
    {
        Assert.Equal(expected, FurnitureDirections.Heading(Definition(directions), heading));
    }

    [Theory]
    [InlineData(false, 6, 6)]
    [InlineData(false, 7, null)]
    [InlineData(true, 7, 7)]
    public void FurniWithoutAssetDirectionsKeepTheEvenOrExtraRotRule(bool extraRot, int heading, int? expected)
    {
        Assert.Equal(expected, FurnitureDirections.Heading(new ItemDefinition { ExtraRot = extraRot }, heading));
    }

    // Observed on AIR WIN63-202610131318-860371703: Move/Rotate clockwise leaves a wf_colortile as it is.
    [Theory]
    [InlineData(new[] { 0 }, 0, true, 0)]
    [InlineData(new[] { 0 }, 0, false, 0)]
    [InlineData(new[] { 0, 2 }, 0, true, 2)]
    [InlineData(new[] { 0, 2 }, 2, true, 0)]
    [InlineData(new[] { 0, 2 }, 0, false, 2)]
    [InlineData(new[] { 0, 2, 4, 6 }, 6, true, 0)]
    [InlineData(new[] { 0, 2, 4, 6 }, 0, false, 6)]
    [InlineData(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 1, true, 3)]
    [InlineData(new int[0], 6, true, 0)]
    public void ATurnSkipsDirectionsTheFurniLacks(int[] directions, int rotation, bool clockwise, int expected)
    {
        Assert.Equal(expected, FurnitureDirections.Turn(Definition(directions), rotation, clockwise));
    }

    [Fact]
    public void ADiagonalFromTheAssetIsAllowedWithoutExtraRot()
    {
        Assert.True(FurnitureDirections.Allows(Definition(0, 1, 2, 3, 4, 5, 6, 7), 7));
        Assert.False(FurnitureDirections.Allows(Definition(0, 2), 7));
        Assert.True(FurnitureDirections.Allows(Definition(0), 2));
        Assert.False(FurnitureDirections.Allows(Definition(0, 1), 8));
    }

    [Theory]
    [InlineData(new[] { 0 })]
    [InlineData(new[] { 0, 2 })]
    public void MoveRotateClockwiseOnAFurniWithOneAxisStaysOrFlips(int[] directions)
    {
        var item = new Item { Id = 1, Definition = Definition(directions) };
        var rotation = -1;

        new WiredMovementActions().Execute("wf_act_move_rotate", new() { IntParams = [-1, 2, 100, 0] }, [item], [], [],
            (_, _, _, value, _) => { rotation = value; return true; }, (_, _, _, _, _) => false, (_, _) => { });

        Assert.Equal(directions.Length == 1 ? 0 : 2, rotation);
    }

    // AIR WIN63-202610151745-318700621 room/class_1854.as:1971-1983 rounds a diagonal up to the next quarter turn.
    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    [InlineData(5, 6)]
    [InlineData(7, 0)]
    public void ADiagonalFurniCoversTheFootprintOfTheQuarterTurnItRoundsTo(int diagonal, int quarter)
    {
        var tiles = Gamemap.GetAffectedTiles(3, 1, 5, 5, diagonal).Values.Select(tile => (tile.X, tile.Y)).ToArray();

        Assert.Equal(Gamemap.GetAffectedTiles(3, 1, 5, 5, quarter).Values.Select(tile => (tile.X, tile.Y)), tiles);
        Assert.Equal(2, tiles.Length);
    }
}
