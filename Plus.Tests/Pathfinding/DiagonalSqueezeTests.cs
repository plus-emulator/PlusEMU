using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests.Pathfinding;

// A room's :disablediagonal setting lets its actors cut between corner-touching blocked tiles.
public class DiagonalSqueezeTests
{
    private const SquareState O = SquareState.Open;
    private const SquareState X = SquareState.Blocked;

    private static ActorProfile Squeezing => new() { DiagonalSqueeze = true };

    [Theory]
    [InlineData(CornerRule.Official)]
    [InlineData(CornerRule.Strict)]
    [InlineData(CornerRule.None)]
    public void SqueezePassesBetweenCornerFurnitureWhateverTheGlobalRule(CornerRule corner)
    {
        var (grid, settings) = CornerFurniture(corner);
        var rules = new MovementRules(grid, settings);
        var from = grid.Position(grid.Tile(2, 1));
        var to = grid.Position(grid.Tile(1, 2));

        Assert.Equal(corner == CornerRule.None ? StepReason.Ok : StepReason.CornerBlocked,
            rules.CanStep(new(), from, to, StepPurpose.Transit, OccupancyView.Planning).Reason);
        Assert.True(rules.CanStep(Squeezing, from, to, StepPurpose.Transit, OccupancyView.Planning).Ok);
        Assert.True(rules.CanStep(Squeezing, from, to, StepPurpose.Goal, OccupancyView.Planning).Ok);
        // The furniture itself stays closed.
        Assert.Equal(StepReason.NotStandable,
            rules.CanStep(Squeezing, from, grid.Position(grid.Tile(1, 1)), StepPurpose.Transit, OccupancyView.Planning).Reason);
    }

    [Fact]
    public void SearchSqueezesOnlyWhenAllowedAndOtherwiseRoutesAround()
    {
        var (grid, settings) = CornerFurniture(CornerRule.Official);
        var search = new PathSearch(grid, settings);
        var start = grid.Position(grid.Tile(2, 1));
        using var lease = PathWorkspacePool.Rent(grid.SlotCapacity, grid.ActiveNodeCount);
        var route = new Route();

        Assert.Equal(PathOutcome.Found, search.Find(new(Squeezing, start, 1, 2), lease.Workspace, route));
        Assert.Equal([grid.Tile(1, 2)], route.Steps.ToArray().Select(step => step.Tile));

        var official = new ActorProfile();
        Assert.Equal(PathOutcome.Found, search.Find(new(official, start, 1, 2), lease.Workspace, route));
        Assert.Equal(3, route.Count);
        var rules = new MovementRules(grid, settings);
        var from = start;

        foreach (var step in route.Steps.ToArray()) {
            var next = grid.Position(step.Tile);
            Assert.True(rules.CanStep(official, from, next, StepPurpose.Transit, OccupancyView.Planning).Ok
                || rules.CanStep(official, from, next, StepPurpose.Goal, OccupancyView.Planning).Ok);
            from = next;
        }
    }

    [Theory]
    [InlineData(CornerRule.Official, StepReason.CornerVoid)]
    [InlineData(CornerRule.Strict, StepReason.CornerBlocked)]
    [InlineData(CornerRule.None, StepReason.Ok)]
    public void SqueezeNeverCutsAVoidCornerTheGlobalRuleRefuses(CornerRule corner, StepReason expected)
    {
        var settings = new PathfindingSettings { CornerRule = corner };

        // Room sections that touch only at a corner:
        // 0 x x
        // x 0 0
        // x 0 0
        var (sections, _, _) = NavTest.Create(3, 3, settings, states: [O, X, X, X, O, O, X, O, O]);
        AssertSameAsGlobalRule(sections, settings, (0, 0), (1, 1), expected);
        var workspace = new PathWorkspace(9, 9);
        var route = new Route();
        Assert.Equal(corner == CornerRule.None ? PathOutcome.Found : PathOutcome.Unreachable,
            new PathSearch(sections, settings).Find(new(Squeezing, sections.Position(0), 2, 2), workspace, route));

        // One flank void, the other furniture: still the void rule.
        var (mixed, inputs, compiler) = NavTest.Create(3, 3, settings, states: [O, X, O, O, O, O, O, O, O]);
        inputs.Publish(NavTest.Record(1, 1, [mixed.Tile(0, 1)], walkable: false));
        compiler.ApplyNow();
        AssertSameAsGlobalRule(mixed, settings, (0, 0), (1, 1), expected);

        // A doorway on the room's edge is left straight, not diagonally past the void beside it:
        // x 0 0
        // D 0 0
        // x 0 0
        var (doorway, _, _) = NavTest.Create(3, 3, settings, states: [X, O, O, O, O, O, X, O, O], door: 3);
        AssertSameAsGlobalRule(doorway, settings, (0, 1), (1, 0), expected);
        Assert.Equal(PathOutcome.Found, new PathSearch(doorway, settings).Find(new(Squeezing, doorway.Position(3), 1, 0), workspace, route));
        Assert.Equal(corner == CornerRule.None ? new[] { doorway.Tile(1, 0) } : new[] { doorway.Tile(1, 1), doorway.Tile(1, 0) },
            route.Steps.ToArray().Select(step => step.Tile).ToArray());
    }

    [Fact]
    public void SqueezeNeverLeavesTheLayout()
    {
        // 0 x x
        // x 0 0
        // x 0 0
        var settings = new PathfindingSettings { CornerRule = CornerRule.None };
        var (grid, _, _) = NavTest.Create(3, 3, settings, states: [O, X, X, X, O, O, X, O, O]);
        var rules = new MovementRules(grid, settings);
        var corner = grid.Position(grid.Tile(0, 0));
        var inside = grid.Position(grid.Tile(1, 1));

        Assert.Equal(StepReason.NotStandable,
            rules.CanStep(Squeezing, inside, grid.Position(grid.Tile(2, 0)), StepPurpose.Goal, OccupancyView.Planning).Reason);
        Assert.Equal(StepReason.NotStandable,
            rules.CanStep(Squeezing, inside, grid.Position(grid.Tile(0, 2)), StepPurpose.Goal, OccupancyView.Planning).Reason);
        Assert.Equal(StepReason.BoundsOrAdjacency,
            rules.CanStep(Squeezing, corner, new(-1, -1, 0), StepPurpose.Goal, OccupancyView.Planning).Reason);

        var search = new PathSearch(grid, settings);
        using var lease = PathWorkspacePool.Rent(grid.SlotCapacity, grid.ActiveNodeCount);
        var route = new Route();
        Assert.Equal(PathOutcome.Found, search.Find(new(Squeezing, corner, 2, 2), lease.Workspace, route));
        Assert.All(route.Steps.ToArray(), step => Assert.False(grid.TileVoid[step.Tile]));
        Assert.Equal(PathOutcome.InvalidGoal, search.Find(new(Squeezing, corner, 2, 0), lease.Workspace, route));
    }

    private static void AssertSameAsGlobalRule(NavGrid grid, PathfindingSettings settings, (int X, int Y) from, (int X, int Y) to,
        StepReason expected)
    {
        var rules = new MovementRules(grid, settings);
        var start = grid.Position(grid.Tile(from.X, from.Y));
        var target = grid.Position(grid.Tile(to.X, to.Y));
        Assert.Equal(expected, rules.CanStep(new(), start, target, StepPurpose.Transit, OccupancyView.Planning).Reason);
        Assert.Equal(expected, rules.CanStep(Squeezing, start, target, StepPurpose.Transit, OccupancyView.Planning).Reason);
    }

    // Furniture on (1,1) and (2,2) of an open 4x4 floor; the tiles touch at a corner.
    private static (NavGrid Grid, PathfindingSettings Settings) CornerFurniture(CornerRule corner)
    {
        var settings = new PathfindingSettings { CornerRule = corner };
        var (grid, inputs, compiler) = NavTest.Create(4, 4, settings);
        inputs.Publish(NavTest.Record(1, 1, [grid.Tile(1, 1)], walkable: false));
        inputs.Publish(NavTest.Record(2, 1, [grid.Tile(2, 2)], walkable: false));
        compiler.ApplyNow();

        return (grid, settings);
    }
}
