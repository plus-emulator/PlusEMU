using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

/// <summary>Official match_to_sshot behaviour for selector pools and snapshots.</summary>
public partial class ModernWiredBehaviorTests
{
    private static WiredConfiguration SnapshotRuntime(int source, params (uint Id, int X)[] spots)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_match_to_sshot", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [0, 0, 1, 0],
            FurniSourceTypes = [source],
            PrimaryItems = [.. spots.Select(spot => new WiredNativeItemReference(spot.Id, false))],
            SavedState = new() { Snapshots = [.. spots.Select(spot => new WiredFurniSnapshot(spot.Id, 1, spot.X, 1, 0, 0, "0"))] }
        };
        Assert.True(WiredNativeEditorProjection.TryCompile(99, descriptor, native, out var runtime));

        return runtime;
    }

    private static Dictionary<uint, int> RunSnapshot(WiredConfiguration runtime, Random random, params Plus.HabboHotel.Items.Item[] movers)
    {
        Assert.True(new WiredMovementActions(random).Execute("wf_act_match_to_sshot", runtime, movers, [], [],
            (item, x, y, _, _) => { item.GetX = x; item.GetY = y; return true; }, (_, _, _, _, _) => false, (_, _) => { }));

        return movers.ToDictionary(item => item.Id, item => item.GetX);
    }

    // With the picked furni each marker returns to its own saved spot.
    [Fact]
    public void PickedSnapshotFurniReturnToTheirOwnSpots()
    {
        var runtime = SnapshotRuntime(100, (1, 7), (2, 9), (3, 11));

        for (var seed = 0; seed < 6; seed++) {
            var moved = RunSnapshot(runtime, new Random(seed), Item(1, 10, 2), Item(2, 6, 2), Item(3, 8, 2));
            Assert.Equal(new Dictionary<uint, int> { [1] = 7, [2] = 9, [3] = 11 }, moved);
        }
    }

    // A selector pool of four furni (one without an entry) over three saved spots: every spot is used, the three snapshotted furni take a varying permutation and the fourth shares a spot. With one furni, it goes to A, then C.
    [Fact]
    public void SelectorPoolFurniAreDealtTheSavedSpotsInARandomOrder()
    {
        var runtime = SnapshotRuntime(200, (1, 7), (2, 9), (3, 11));
        var permutations = new HashSet<string>();

        for (var seed = 0; seed < 12; seed++) {
            var moved = RunSnapshot(runtime, new Random(seed), Item(1, 7, 2), Item(2, 9, 2), Item(3, 11, 2), Item(4, 8, 2));
            Assert.Equal([7, 9, 11], moved.Values.Distinct().Order());
            permutations.Add(string.Join(",", moved.Where(pair => pair.Key != 4).OrderBy(pair => pair.Key).Select(pair => pair.Value)));
        }

        Assert.True(permutations.Count > 1);

        var singles = Enumerable.Range(0, 12).Select(seed => RunSnapshot(runtime, new Random(seed), Item(1, 7, 2))[1]).ToHashSet();
        Assert.True(singles.Count > 1);
    }

    // The reopened frame of a saved snapshot carries the snapshot text with N for each part the options leave out, and ends with the hotel int at 0. A fresh box opens with [0,0,0,0,1] and no text.
    [Theory]
    [InlineData(0, 0, 0, 0, "2147418170,N,N,N,N,N")]
    [InlineData(1, 0, 0, 0, "2147418170,0,N,N,N,N")]
    [InlineData(0, 1, 0, 0, "2147418170,N,0,N,N,N")]
    [InlineData(0, 0, 1, 0, "2147418170,N,N,8,2,N")]
    [InlineData(0, 0, 0, 1, "2147418170,N,N,N,N,0")]
    [InlineData(1, 1, 1, 1, "2147418170,0,0,8,2,0")]
    public void SnapshotFrameCarriesTheHotelText(int state, int rotation, int position, int altitude, string text)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_match_to_sshot", out var descriptor));
        var fresh = WiredNativeEditorProjection.HotelFrame(descriptor, WiredNativeEditorProjection.DefaultRuntime(1, descriptor)!.Origin!.Native);
        Assert.Equal([0, 0, 0, 0, 1], fresh.OwnedIntParams.ToArray());
        Assert.Equal("", fresh.Text);

        var saved = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [state, rotation, position, altitude],
            PrimaryItems = [new(2147418170, false)],
            SavedState = new() { Snapshots = [new WiredFurniSnapshot(2147418170, 1, 8, 2, 0, 0, "0")] }
        };
        var frame = WiredNativeEditorProjection.HotelFrame(descriptor, saved);

        Assert.Equal([state, rotation, position, altitude, 0], frame.OwnedIntParams.ToArray());
        Assert.Equal(text, frame.Text);
    }

    // An opened snapshot editor saved back with its listing compiles.
    [Theory]
    [InlineData("wf_act_match_to_sshot")]
    [InlineData("wf_cnd_match_snapshot")]
    [InlineData("wf_cnd_not_match_snap")]
    public void AReopenedSnapshotFrameSavesBack(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var saved = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [0, 1, 0, 0],
            PrimaryItems = [new(2147418170, false)],
            FurniSourceTypes = [100],
            SavedState = new() { Snapshots = [new WiredFurniSnapshot(2147418170, 1, 8, 2, 0, 0, "0")] }
        };
        var frame = WiredNativeEditorProjection.HotelFrame(descriptor, saved);
        Assert.NotEqual("", frame.Text);
        Assert.True(WiredNativeEditorProjection.TryCompile(1, descriptor, frame, out _));
    }

    // Three entries are listed by descending id.
    [Fact]
    public void SnapshotFrameListsEntriesByDescendingId()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_match_to_sshot", out var descriptor));
        var saved = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [0, 0, 1, 0],
            PrimaryItems = [new(2147418170, false), new(2147418171, false), new(2147418172, false)],
            SavedState = new()
            {
                Snapshots = [new WiredFurniSnapshot(2147418170, 1, 7, 1, 0, 0, "0"), new WiredFurniSnapshot(2147418171, 1, 9, 1, 0, 0, "0"),
                    new WiredFurniSnapshot(2147418172, 1, 11, 1, 0, 0, "0")]
            }
        };

        Assert.Equal("2147418172,N,N,11,1,N;2147418171,N,N,9,1,N;2147418170,N,N,7,1,N", WiredNativeEditorProjection.HotelFrame(descriptor, saved).Text);
    }
}
