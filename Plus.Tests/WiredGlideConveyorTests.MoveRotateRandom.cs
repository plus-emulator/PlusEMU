using System.Drawing;
using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint MoveRotateSubjectId = 30;
    private const uint MoveRotateBoxId = 31;
    private static readonly Point[] MoveRotateCompass = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0), new(1, -1), new(1, 1), new(-1, 1), new(-1, -1)];

    // The official ramp saw NW and SW diagonal endpoints for editor option 1, so the local random choice has to be able to reach both.
    [Theory]
    [InlineData(-1, -1)]
    [InlineData(-1, 1)]
    public void NativeSavedMoveRotateOptionOneCanReachTheMeasuredDiagonalEndpoints(int dx, int dy)
    {
        var reached = Enumerable.Range(0, 8).Select(draw => MoveRotateOffset(1, new ScriptedRandom(draw))).ToArray();

        Assert.All(reached, offset => Assert.True(Math.Abs(offset.X) <= 1 && Math.Abs(offset.Y) <= 1 && offset != Point.Empty, $"offset {offset} is not a neighbouring tile"));
        Assert.Contains(new Point(dx, dy), reached);
    }

    // Controls: the options that are not random, and the paired random options, are unchanged whatever the draw.
    [Fact]
    public void NativeSavedMoveRotateOptionZeroNeverMoves()
    {
        Assert.All(Enumerable.Range(0, 8), draw => Assert.Equal(Point.Empty, MoveRotateOffset(0, new ScriptedRandom(draw))));
    }

    [Theory]
    [InlineData(2, 1, 0, -1, 0)]
    [InlineData(3, 0, -1, 0, 1)]
    public void NativeSavedMoveRotatePairedRandomOptionsStayWithinTheirPair(int option, int firstX, int firstY, int secondX, int secondY)
    {
        var reached = Enumerable.Range(0, 8).Select(draw => MoveRotateOffset(option, new ScriptedRandom(draw))).ToHashSet();

        Assert.Subset(new HashSet<Point> { new(firstX, firstY), new(secondX, secondY) }, reached);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    [InlineData(4, false)]
    [InlineData(11, false)]
    public void NativeSavedMoveRotateFixedOptionsIgnoreTheDraw(int option, bool scripted)
    {
        var expected = MoveRotateCompass[option - 4];

        foreach (var draw in scripted ? new[] { 0, 7 } : [0]) {
            Assert.Equal(expected, MoveRotateOffset(option, scripted ? new ScriptedRandom(draw) : null));
        }
    }

    // Offset of the subject from (9,9) after one real native-saved move/rotate card run with the given random source (null = default).
    private static Point MoveRotateOffset(int option, Random? random)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate", out var descriptor));
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (MoveRotateSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null),
            (MoveRotateBoxId, 4, 12, 0.65, 0, "wf_act_move_rotate", null)
        };
        var f = new Fixture(layout, live: true);
        var room = f.MovementContext().Room;
        var box = f.Items[MoveRotateBoxId];
        var subject = f.Items[MoveRotateSubjectId];
        // A schema 2 native row [option, turn 0], F100 picking the subject; the real store compiles it.
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [option, 0],
            PrimaryItems = [new(MoveRotateSubjectId, false)],
            FurniSourceTypes = [100],
            Delay = 0
        };
        var row = new ModernWiredRuntimeTests.StoredRuntimeRow(MoveRotateBoxId, "wf_act_move_rotate", 2, JsonSerializer.Serialize(native));
        var config = new SavedConfigurations([row]).Load(MoveRotateBoxId, descriptor);
        Assert.NotNull(config);
        Assert.NotNull(config.Origin?.Native);
        int[] stored = [-1, 8, 9, 10, 0, 2, 4, 6, 1, 3, 5, 7];
        Assert.Equal([stored[option], 0, 100, 0], config.IntParams.ToArray());
        Assert.Equal([MoveRotateSubjectId], config.SelectedItems.ToArray());

        var action = new WiredModernAction(room, box, descriptor, new(), _ => { }, (_, _, _) => { }, new(), TestLogging.Logger, TimeProvider.System,
            TestWiredRewardService.Instance, TestBotManagementStore.Instance, TestWiredClients.Empty, TestWiredDefinitions.Unused, TestItemRuntime.Travel,
            movementRandom: random);
        action.ApplyConfiguration(config);
        Assert.Equal((9, 9), (subject.GetX, subject.GetY));

        f.Owned(() => action.Execute(f.MovementContext()));

        return new(subject.GetX - 9, subject.GetY - 9);
    }

    // A deterministic stand-in for the random source: every request is checked to be a valid bound and answered with the scripted draw clamped into it.
    private sealed class ScriptedRandom(int draw) : Random
    {
        public override int Next() => draw;
        public override int Next(int maxValue)
        {
            Assert.True(maxValue > 0, $"Next({maxValue}) asks for an empty range");

            return Math.Min(draw, maxValue - 1);
        }
        public override int Next(int minValue, int maxValue)
        {
            Assert.True(minValue < maxValue, $"Next({minValue}, {maxValue}) asks for an empty range");

            return Math.Min(minValue + draw, maxValue - 1);
        }
    }
}
