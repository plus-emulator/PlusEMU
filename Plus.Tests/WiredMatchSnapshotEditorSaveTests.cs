using System.Drawing;
using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

// Raw saves of Match to Snapshot through the real save packet, read back the way the editor reopens them. The reopened frame has
// the four options and a fifth hotel int that is 0 after any save.
public sealed class WiredMatchSnapshotEditorSaveTests
{
    private const string Name = "wf_act_match_to_sshot";

    // The saves below were sent in this order to one box with one picked furni, each read back after its save.
    [Fact]
    public void EverySaveReopensWithTheFiveHabboInts()
    {
        using var save = new WiredRawSave(Name);
        uint[] picks = [save.Furni()];
        (int[] Sent, int[] Stored)[] steps =
        [
            ([1, 0, 0, 0], [1, 0, 0, 0, 0]), ([0, 1, 0, 0], [0, 1, 0, 0, 0]), ([0, 0, 1, 0], [0, 0, 1, 0, 0]), ([0, 0, 0, 1], [0, 0, 0, 1, 0]),
            ([1, 1, 1, 1], [1, 1, 1, 1, 0]), ([0, 0, 0, 0], [0, 0, 0, 0, 0]), ([2, 0, 0, 0], [0, 0, 0, 0, 0]), ([0, 2, 0, 0], [0, 0, 0, 0, 0]),
            ([0, 0, 2, 0], [0, 0, 0, 0, 0]), ([0, 0, 0, 2], [0, 0, 0, 0, 0]), ([-1, -1, -1, -1], [0, 0, 0, 0, 0]), ([1], [1, 0, 0, 0, 0]),
            ([], [0, 0, 0, 0, 0]), ([1, 1, 1, 1, 1], [1, 1, 1, 1, 0]), ([1, 1, 1, 1, 0], [1, 1, 1, 1, 0])
        ];

        foreach (var (sent, stored) in steps) {
            var result = save.Save(ints: sent, picks: picks);

            Assert.True(result.Accepted, $"[{string.Join(',', sent)}]: {result}");
            Assert.Equal(stored, result.Ints);
        }
    }

    // A missing option takes its default, as with one int and no ints, and ints past the fifth are dropped.
    [Theory]
    [InlineData(new[] { 1, 0, 1 }, new[] { 1, 0, 1, 0, 0 })]
    [InlineData(new[] { 1, 1, 1, 1, 1, 1 }, new[] { 1, 1, 1, 1, 0 })]
    public void ShortAndLongSavesKeepTheFiveIntLayout(int[] sent, int[] stored)
    {
        using var save = new WiredRawSave(Name);

        var result = save.Save(ints: sent, picks: [save.Furni()]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(stored, result.Ints);
    }

    // The reopened text is the snapshot listing, not the text that was sent.
    [Fact]
    public void ATextIsAcceptedAndTheIntsKeepTheirLayout()
    {
        using var save = new WiredRawSave(Name);

        var result = save.Save(ints: [1, 0, 0, 0], text: "abc", picks: [save.Furni()]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([1, 0, 0, 0, 0], result.Ints);
        Assert.DoesNotContain("abc", result.Text);
    }

    // A new box opens with [0,0,0,0,1].
    [Fact]
    public void ANewBoxOpensWithTheHotelIntAtOne()
    {
        using var save = new WiredRawSave(Name);

        Assert.Equal([0, 0, 0, 0, 1], save.Stored().Ints);
    }

    [Fact]
    public void AFourIntRowStoredByTheOldCodeReopensWithFiveIntsAndRunsTheSameMatch()
    {
        Assert.True(WiredBoxRegistry.TryGet(Name, out var descriptor));
        var snapshot = new WiredFurniSnapshot(7, 1, 8, 12, 0, 0, "0");
        var row = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [0, 0, 1, 0],
            PrimaryItems = [new(7, false)],
            FurniSourceTypes = [100],
            SavedState = new() { Snapshots = [snapshot] }
        };

        var runtime = Assert.IsType<WiredConfiguration>(WiredConfigurationStore.DecodeRow(7, Name, JsonSerializer.Serialize(row)));
        var frame = WiredNativeEditorProjection.HotelFrame(descriptor, runtime.Origin!.Native);

        Assert.Equal([0, 0, 1, 0], runtime.Origin!.Native.OwnedIntParams.ToArray());
        Assert.Equal([0, 0, 1, 0, 0], frame.OwnedIntParams.ToArray());
        Assert.Equal("7,N,N,8,12,N", frame.Text);

        var item = ModernWiredRuntimeTests.MakeItemForG3(7, "tile");
        var moves = new List<Point>();
        new WiredMovementActions().Execute(Name, runtime, [item], [], [],
            (_, x, y, _, _) => { moves.Add(new(x, y)); return true; }, (_, _, _, _, _) => false, (_, _) => { });

        Assert.Equal(new Point(8, 12), Assert.Single(moves));
    }
}
