using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public class FurnitureDirectionsTests
{
    private static ItemDefinition Definition(params int[] directions) => new() { ItemName = "test", Directions = directions };

    private static ItemDefinition Definition(int defaultDirection, int[] directions) =>
        new() { ItemName = "test", Directions = directions, DefaultDirection = defaultDirection };

    [Theory]
    [InlineData("bc_block_0_recolorable", 1, 2)]
    [InlineData("bc_block_0_recolorable", 3, 6)]
    [InlineData("bc_block_0_recolorable", 4, null)]
    [InlineData("ktchn_fridge", 0, 2)]
    [InlineData("ktchn_fridge", 1, 4)]
    [InlineData("ktchn_fridge", 2, null)]
    public void RotationIsAPositionInTheFurnisDirections(string name, int value, int? stored)
    {
        // @rotation writes and reads the position in the furni's list of directions.
        var definition = new ItemDefinition { ItemName = name, Directions = FurnitureDirections.For(name) };

        Assert.Equal(stored, FurnitureDirections.RotationAt(definition, value));

        if (stored is { } rotation) {
            Assert.Equal(value, FurnitureDirections.RotationIndex(definition, rotation));
        }
    }

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

    // A heading the furni lacks lands it on its defaultdir.
    [Theory]
    [InlineData(new[] { 0, 2 }, 2, 4, 2)]
    [InlineData(new[] { 0, 2 }, 2, 3, 2)]
    [InlineData(new[] { 0, 2 }, 2, 5, 2)]
    [InlineData(new[] { 0, 2 }, 2, 7, 2)]
    [InlineData(new[] { 0, 2 }, 2, 1, 2)]
    [InlineData(new[] { 0, 2 }, 2, 6, 2)]
    [InlineData(new[] { 2, 6 }, 2, 0, 2)]
    [InlineData(new[] { 2, 6 }, 2, 4, 2)]
    [InlineData(new[] { 0 }, 0, 6, 0)]
    [InlineData(new[] { 0, 2 }, 2, 0, 0)]
    [InlineData(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 0, 7, 7)]
    [InlineData(new[] { 0, 2 }, 4, 6, null)]
    public void AHeadingTheFurniLacksLandsOnItsDefaultDirection(int[] directions, int defaultDirection, int heading, int? expected)
    {
        Assert.Equal(expected, FurnitureDirections.Heading(Definition(defaultDirection, directions), heading));
    }

    [Theory]
    [InlineData(false, 6, 6)]
    [InlineData(false, 7, null)]
    [InlineData(true, 7, 7)]
    public void FurniWithoutAssetDirectionsKeepTheEvenOrExtraRotRule(bool extraRot, int heading, int? expected)
    {
        Assert.Equal(expected, FurnitureDirections.Heading(new ItemDefinition { ExtraRot = extraRot }, heading));
    }

    // With Move/Rotate, each turn is the next direction in 45 degree order.
    [Theory]
    [InlineData(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 0, true, new[] { 1, 2, 3 })]
    [InlineData(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 0, false, new[] { 7, 6, 5 })]
    [InlineData(new[] { 2, 4 }, 2, true, new[] { 4, 2, 4 })]
    [InlineData(new[] { 2, 4 }, 2, false, new[] { 4, 2, 4 })]
    [InlineData(new[] { 2, 6 }, 2, true, new[] { 6, 2, 6 })]
    [InlineData(new[] { 0 }, 0, true, new[] { 0, 0, 0 })]
    [InlineData(new int[0], 6, true, new[] { 0, 2, 4 })]
    public void ATurnStepsToTheNextDirectionTheFurniHas(int[] directions, int start, bool clockwise, int[] expected)
    {
        var definition = Definition(0, directions);
        var rotation = start;
        var turns = new List<int>();

        for (var turn = 0; turn < expected.Length; turn++) {
            rotation = FurnitureDirections.Turn(definition, rotation, clockwise);
            turns.Add(rotation);
        }

        Assert.Equal(expected, turns);
    }

    // On wf_antenna2 (0, 2): from 0, 1 stores 2 and 4 is refused; from 2, 4 is refused, 0 stores 0, then 6 and 4 are refused. bc_stairs_recolorable takes 3.
    [Theory]
    [InlineData(new[] { 0, 2 }, 1, 2)]
    [InlineData(new[] { 0, 2 }, 4, null)]
    [InlineData(new[] { 0, 2 }, 0, 0)]
    [InlineData(new[] { 0, 2 }, 6, null)]
    [InlineData(new[] { 0, 2 }, 2, 2)]
    [InlineData(new[] { 0, 2 }, 7, 0)]
    [InlineData(new[] { 0, 2 }, 3, 2)]
    [InlineData(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 3, 3)]
    [InlineData(new[] { 2, 6 }, 3, 2)]
    [InlineData(new[] { 2, 6 }, 5, 6)]
    [InlineData(new[] { 0, 2 }, 8, null)]
    [InlineData(new int[0], 3, null)]
    [InlineData(new int[0], 4, 4)]
    public void AWriteTakesTheNearestDirectionWithin45DegreesClockwiseFirst(int[] directions, int value, int? expected)
    {
        Assert.Equal(expected, FurnitureDirections.Write(Definition(2, directions), value));
    }

    // Assigns 4, 0, 6 and 4 in turn to wf_antenna2 (directions 0 and 2), starting at 2.
    [Fact]
    public void TheObservedAssignSequenceStoresOnlyTheZero()
    {
        var definition = Definition(2, [0, 2]);
        var rotation = 2;
        var updates = new List<int>();

        foreach (var value in new[] { 4, 0, 6, 4 }) {
            if (FurnitureDirections.Write(definition, value) is { } stored && stored != rotation) {
                rotation = stored;
                updates.Add(stored);
            }
        }

        Assert.Equal(new[] { 0 }, updates);
        Assert.Equal(0, rotation);
    }

    // A client move of wf_antenna2 (0, 2, defaultdir 2) to direction 4 stores 2.
    [Theory]
    [InlineData(new[] { 0, 2 }, 2, 4, 2)]
    [InlineData(new[] { 0, 2 }, 2, 0, 0)]
    [InlineData(new[] { 0, 2 }, 6, 4, 0)]
    [InlineData(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 0, 5, 5)]
    [InlineData(new int[0], 0, 4, 4)]
    [InlineData(new int[0], 0, 3, 0)]
    public void AUserMoveStoresADirectionTheFurniHas(int[] directions, int defaultDirection, int rotation, int expected)
    {
        Assert.Equal(expected, FurnitureDirections.Place(Definition(defaultDirection, directions), rotation));
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

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(0, 4, 0)]
    [InlineData(2, 4, 2)]
    [InlineData(2, 6, 2)]
    [InlineData(2, 0, 0)]
    public void MatchToSnapshotStoresOnlyADirectionTheFurniHas(int start, int snapshotRotation, int expected)
    {
        var item = new Item { Id = 1, Definition = Definition(2, [0, 2]), Rotation = start };
        item.Definition.Type = ItemType.Floor;
        var rotation = -1;

        new WiredMovementActions().Execute("wf_act_match_to_sshot",
            new() { IntParams = [0, 1, 0, 0, 100], SelectedItems = [1], Snapshots = [new(1, 0, 0, 0, 0, snapshotRotation, "0")] }, [item], [], [],
            (_, _, _, value, _) => { rotation = value; return true; }, (_, _, _, _, _) => false, (_, _) => { });

        Assert.Equal(expected, rotation);
    }

    // The client rounds a diagonal up to the next quarter turn.
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
