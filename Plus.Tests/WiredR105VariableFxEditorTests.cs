using System.Collections.Immutable;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// The Variable FX cards and the Variable Leaderboard, saved the way Habbo stores them.
// Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredR105VariableFxEditorTests
{
    private static readonly string[] Cards =
    [
        "wf_xtra_var_fx_health", "wf_xtra_var_fx_progress", "wf_xtra_var_fx_level", "wf_xtra_var_fx_status", "wf_xtra_var_fx_boss",
        "wf_xtra_var_fx_number"
    ];

    public static TheoryData<string> AllCards => new(Cards);

    private static int[] Fresh(string card)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));

        return [.. WiredNativeEditorProjection.DefaultNative(descriptor).OwnedIntParams];
    }

    private static WiredR104Save.Result Save(string card, Action<int[]>? edit = null, string? text = null, string[]? variables = null,
        int[]? replace = null)
    {
        var ints = replace ?? Fresh(card);
        edit?.Invoke(ints);
        using var world = new G1World();

        return WiredR104Save.Save(world, card, ints, text: text, variables: variables);
    }

    private static int[] Stored(string card, Action<int[]>? edit = null, string? text = null, string[]? variables = null, int[]? replace = null)
    {
        var result = Save(card, edit, text, variables, replace);
        Assert.True(result.Accepted, result.ToString());

        return result.Ints;
    }

    [Theory]
    [MemberData(nameof(AllCards))]
    public void AFreshCardOpensWithColourWidthAndRendererUnset(string card)
    {
        using var world = new G1World();
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));
        var item = world.AddItem(card, InteractionType.WiredEffect);
        var box = world.Wired.CreateConfiguredBox(item) ?? throw new InvalidOperationException(card);
        var frame = WiredNativeEditorProjection.HotelFrame(descriptor, box.Configuration.Origin!.Native).OwnedIntParams;

        Assert.Equal("1,2,0,7,0,3000,0,-1,-1,-1,0,0,0,100,0,0,1,1,0,0,0," + (card.EndsWith("level", StringComparison.Ordinal) ? 2 : 0) + ",0",
            string.Join(',', frame));
    }

    [Theory]
    [InlineData("wf_xtra_var_fx_health", 1000, 2, 10)]
    [InlineData("wf_xtra_var_fx_progress", 1, 2, 0)]
    [InlineData("wf_xtra_var_fx_level", 1001, 2, 20)]
    [InlineData("wf_xtra_var_fx_status", -1, 2, 2)]
    [InlineData("wf_xtra_var_fx_boss", 5, 4, 100)]
    [InlineData("wf_xtra_var_fx_number", 1, -1, 201)]
    public void TheFirstSaveFillsTheStyleDefaults(string card, int colour, int width, int renderer) =>
        Assert.Equal([colour, width, renderer], Stored(card).Skip(7).Take(3));

    [Theory]
    [MemberData(nameof(AllCards))]
    public void NumbersOutsideTheEditorAreFittedNotRefused(string card)
    {
        // Source: furni (0) or user (1), anything else is user. A furni source also retargets both range overrides.
        Assert.Equal(1, Stored(card, p => p[0] = int.MinValue)[0]);
        Assert.Equal(1, Stored(card, p => p[0] = 7)[0]);
        Assert.Equal(0, Stored(card, p => p[0] = 0)[16]);
        // Audience: 0, 1, 3 and 4 stay; anything else is 2.
        Assert.Equal(3, Stored(card, p => p[1] = 3)[1]);
        Assert.Equal(4, Stored(card, p => p[1] = 4)[1]);
        Assert.Equal(2, Stored(card, p => p[1] = 5)[1]);
        Assert.Equal(2, Stored(card, p => p[1] = -1)[1]);
        // Show mode: 0, 1 and 2 stay and mode 2 switches the hover flag on; anything else is 0.
        Assert.Equal([2, 1], Stored(card, p => p[2] = 2).Skip(2).Where((_, index) => index is 0 or 2));
        Assert.Equal([0, 0], Stored(card, p => { p[2] = 3; p[4] = 1; }).Skip(2).Where((_, index) => index is 0 or 2));
        // Observed on AIR WIN63-202610201032-107964540: show mode 1 keeps the hover flag as sent, and show mode 0 stores it off.
        Assert.Equal(1, Stored(card, p => { p[2] = 1; p[4] = 1; })[4]);
        Assert.Equal(0, Stored(card, p => { p[2] = 1; p[4] = 0; })[4]);
        Assert.Equal(0, Stored(card, p => { p[2] = 0; p[4] = 1; })[4]);
        // The update mask is kept as sent.
        Assert.Equal(-2147483648, Stored(card, p => p[3] = int.MinValue)[3]);
        // The duration runs from 1500 to 20000.
        Assert.Equal(1500, Stored(card, p => p[5] = 0)[5]);
        Assert.Equal(20000, Stored(card, p => p[5] = int.MaxValue)[5]);
        // Segments run from 0 to 100.
        Assert.Equal(100, Stored(card, p => p[20] = 3000)[20]);
        Assert.Equal(0, Stored(card, p => p[20] = -1)[20]);
        // A style the card does not have is style 0.
        Assert.Equal(0, Stored(card, p => p[6] = 100)[6]);
    }

    [Theory]
    [MemberData(nameof(AllCards))]
    public void TheValueRangeIsTwoSignedNumbersAndTheMaximumStaysAboveTheMinimum(string card)
    {
        if (card.EndsWith("level", StringComparison.Ordinal)) {
            Assert.Equal([-1, -1, -1, -1], Stored(card, p => p[11] = 5).Skip(10).Take(4));

            return;
        }

        // max 100 above min 5: unchanged
        Assert.Equal([0, 5, 0, 100], Stored(card, p => p[11] = 5).Skip(10).Take(4));
        // max not above min: max = min + 1
        Assert.Equal([0, 0, 0, 1], Stored(card, p => p[13] = 0).Skip(10).Take(4));
        Assert.Equal([0, 100, 0, 101], Stored(card, p => p[11] = 100).Skip(10).Take(4));
        // the low words read as unsigned: min lo -1 is 4294967295, so the maximum becomes 4294967296
        Assert.Equal([0, -1, 1, 0], Stored(card, p => p[11] = -1).Skip(10).Take(4));
        Assert.Equal([0, int.MaxValue, 0, int.MinValue], Stored(card, p => p[11] = int.MaxValue).Skip(10).Take(4));
        // a high word above the maximum's
        Assert.Equal([1, 0, 1, 1], Stored(card, p => p[10] = 1).Skip(10).Take(4));
        Assert.Equal([-1, 0, 0, 100], Stored(card, p => p[10] = -1).Skip(10).Take(4));
    }

    [Fact]
    public void ALevellingBarKeepsOneOfItsSubRenderers()
    {
        var level = "wf_xtra_var_fx_level";

        Assert.Equal(2, Stored(level, p => p[21] = 1)[21]);
        Assert.Equal(3, Stored(level, p => p[21] = 3)[21]);
        Assert.Equal(4, Stored(level, p => p[21] = 5)[21]);
        Assert.Equal(4, Stored(level, p => p[21] = int.MaxValue)[21]);
        Assert.Equal(2, Stored(level, p => p[21] = int.MinValue)[21]);
    }

    [Fact]
    public void ANumberDisplayKeepsItsIconSlotAndIconOnly()
    {
        var number = "wf_xtra_var_fx_number";

        Assert.Equal(1, Stored(number, p => p[21] = 1)[21]);
        Assert.Equal(2, Stored(number, p => p[21] = 2)[21]);
        Assert.Equal(0, Stored(number, p => p[21] = 3)[21]);
        Assert.Equal(0, Stored(number, p => p[21] = -1)[21]);
        Assert.Equal("gems", Save(number, text: "gems").Text);
        Assert.Equal("", Save(number, text: "abc").Text);
        Assert.Equal("", Save("wf_xtra_var_fx_health", text: "gems").Text);
        Assert.Equal(0, Stored("wf_xtra_var_fx_health", p => p[21] = 1)[21]);
    }

    [Fact]
    public void AnEditorFrameOfTheWrongLengthTakesTheDefaultsAfterIt()
    {
        var health = "wf_xtra_var_fx_health";
        var defaults = Stored(health);

        Assert.Equal(defaults, Stored(health, replace: [1, 2, 0, 7, 0]));
        Assert.Equal(defaults, Stored(health, replace: [.. Fresh(health), 4, 5, 6]));
        Assert.Equal(defaults, Stored(health, replace: []));
    }

    [Fact]
    public void RangeOverridesNeedTheirVariable()
    {
        var health = "wf_xtra_var_fx_health";

        Assert.Equal([0, 0], Stored(health, p => p[14] = p[15] = 1, variables: ["0", "0", "0"]).Skip(14).Take(2));
        Assert.Equal([1, 0], Stored(health, p => p[14] = p[15] = 1, variables: ["custom:10", "0", "0"]).Skip(14).Take(2));
        // an audience value needs an audience of 3 or 4
        Assert.Equal([0, 0], Stored(health, p => { p[18] = 5; p[19] = 6; }).Skip(18).Take(2));
        // an audience of 3 without a variable is stored as sent
        Assert.Equal(3, Stored(health, p => p[1] = 3, variables: ["0", "0", "0"])[1]);
    }

    [Theory]
    [MemberData(nameof(AllCards))]
    public void AStoredRowKeepsItsFormWhenItLoadsAgain(string card)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        // Hover on with show mode 0 was a valid save before the hotel derived the hover flag from the show mode.
        var ints = Stored(card, p => { p[2] = 2; p[3] = 15; });
        ints[2] = 0;
        var row = native with { OwnedIntParams = [.. ints] };

        Assert.True(WiredNativeEditorProjection.TryCompile(50, descriptor, row, out var runtime, storedRow: true));
        Assert.Equal(1, runtime.IntParams[4]);
        Assert.Equal(0, runtime.IntParams[2]);
    }

    private const string Board = "wf_xtra_var_leaderboard";
    private const string Text = "probe board\t\tUsername\tValue\t\t\t\t\t\t\t\t\t\t\t\t\t";

    private static WiredR104Save.Result SaveBoard(Action<int[]>? edit = null, string? text = Text, string[]? variables = null)
    {
        int[] ints = [.. Wired.WiredLeaderboardDefaults()];
        edit?.Invoke(ints);
        using var world = new G1World();

        return WiredR104Save.Save(world, Board, ints, text: text, variables: variables);
    }

    private static int[] StoredBoard(Action<int[]>? edit = null)
    {
        var result = SaveBoard(edit);
        Assert.True(result.Accepted, result.ToString());

        return result.Ints;
    }

    private static class Wired
    {
        public static ImmutableArray<int> WiredLeaderboardDefaults() => Plus.HabboHotel.Items.Wired.Leaderboard.WiredLeaderboardConfig.DefaultInts;
    }

    [Fact]
    public void ABoardSortModeOutsideZeroToFiveIsStoredAsZero()
    {
        Assert.Equal(5, StoredBoard(p => p[0] = 5)[0]);
        Assert.Equal(0, StoredBoard(p => p[0] = 6)[0]);
        Assert.Equal(0, StoredBoard(p => p[0] = int.MaxValue)[0]);
        Assert.Equal(0, StoredBoard(p => p[0] = int.MinValue)[0]);
    }

    [Fact]
    public void ABoardWithoutATitleIsRefused()
    {
        Assert.Equal("wiredfurni.error.variable_leaderboard.invalid_configuration", SaveBoard(text: "").Key);
        Assert.False(SaveBoard(text: "").Accepted);
    }

    [Fact]
    public void ABoardColumnCountBelowOneIsOneAndTheUnusedColumnIsEmptied()
    {
        var result = SaveBoard(p => p[1] = int.MinValue);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([1, 0, -1, -1, -1], result.Ints.Skip(1).Take(5).Select((value, index) => index == 1 ? 0 : value));
        Assert.Equal(1, result.Ints[1]);
        Assert.Equal(-1, result.Ints[3]);
        Assert.Equal("probe board\t\tUsername" + new string('\t', 14), result.Text);
    }

    [Fact]
    public void ABoardColumnWidthOutsideZeroToThreeIsZeroAndUnusedWidthsAreZero()
    {
        Assert.Equal(0, StoredBoard(p => p[14] = 4)[14]);
        Assert.Equal(0, StoredBoard(p => p[14] = -1)[14]);
        Assert.Equal(2, StoredBoard(p => p[14] = 2)[14]);
        Assert.Equal(0, StoredBoard(p => p[15] = 9)[15]);
        Assert.Equal(0, StoredBoard(p => p[16] = 2)[16]);
        Assert.Equal(0, StoredBoard(p => p[17] = 2)[17]);
        Assert.Equal(0, StoredBoard(p => p[9] = int.MinValue)[9]);
    }

    [Fact]
    public void ABoardKeepsTheComparisonSwitchesOffWhileTheFilterIsOff()
    {
        var stored = StoredBoard(p => { p[7] = 1; p[8] = 1; });

        Assert.Equal([0, 0], stored.Skip(7).Take(2));
    }

    [Fact]
    public void ABoardStoresAVariableInASectionThatIsSwitchedOff()
    {
        var result = SaveBoard(variables: ["custom:10", "custom:10", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0"]);

        Assert.True(result.Accepted, result.ToString());
    }

    [Fact]
    public void ABoardStillRefusesWhatCannotBeFitted()
    {
        const string Key = "wiredfurni.error.variable_leaderboard.invalid_configuration";

        Assert.Equal(Key, SaveBoard(p => p[18] = 3).Key);
        Assert.Equal(Key, SaveBoard(p => p[6] = 1).Key);
        Assert.Equal(Key, SaveBoard(p => p[9] = 1).Key);
        Assert.Equal(Key, SaveBoard(p => p[12] = 33).Key);
        Assert.Equal(Key, SaveBoard(p => p[1] = 3).Key);
    }

    [Fact]
    public void APublishedBoardIsOverlayOneInItsEditorBeforeItHasAUserVariable()
    {
        using var world = new G1World();
        Assert.True(WiredBoxRegistry.TryGet(Board, out var descriptor));
        var item = world.AddItem(Board, InteractionType.WiredEffect);
        var box = world.Wired.CreateConfiguredBox(item) ?? throw new InvalidOperationException(Board);
        Assert.Equal(0, world.Wired.LeaderboardContext(box).OverlayInstanceId);

        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { Text = Text };
        Assert.True(WiredNativeEditorProjection.TryCompile(item.Id, descriptor, native, out var runtime, WiredNativeEditorProjection.AnyTriggerContext));
        Assert.True(WiredConfigurationSave.TrySave(box, runtime, TestWiredConfigurationStore.Instance, out _, id => world.Floor.ContainsKey(id)));

        Assert.Equal(1, world.Wired.LeaderboardContext(box).OverlayInstanceId);
    }

    // Observed on AIR WIN63-202610201032-107964540: a published board whose stack has no user variable is sent in the snapshot with no
    // choices and the default variable "0".
    [Fact]
    public void APublishedBoardWithoutAUserVariableIsInTheSnapshotWithTheDefaultVariableZero()
    {
        using var world = new G1World();
        Assert.True(WiredBoxRegistry.TryGet(Board, out var descriptor));
        var item = world.AddItem(Board, InteractionType.WiredEffect);
        var box = world.Wired.CreateConfiguredBox(item) ?? throw new InvalidOperationException(Board);
        Assert.True(world.Wired.AddBox((Plus.HabboHotel.Items.Wired.IWiredItem)box));
        Assert.Empty(world.Wired.LeaderboardDescriptors());

        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { Text = Text };
        Assert.True(WiredNativeEditorProjection.TryCompile(item.Id, descriptor, native, out var runtime, WiredNativeEditorProjection.AnyTriggerContext));
        Assert.True(WiredConfigurationSave.TrySave(box, runtime, TestWiredConfigurationStore.Instance, out _, id => world.Floor.ContainsKey(id)));

        var sent = Assert.Single(world.Wired.LeaderboardDescriptors());
        Assert.Equal(1, sent.OverlayInstanceId);
        Assert.Empty(sent.Choices);
        Assert.Equal("0", sent.DefaultVariableId);
        Assert.Equal("probe board", sent.Config.Title);
    }

    // Observed on AIR WIN63-202610201032-107964540: the room's board revision counter starts at 1, so the first save of a board shows
    // revision 2 and every later save of any board adds one.
    [Fact]
    public void TheQueryRevisionOfABoardStartsAtTwoAndGrowsWithEverySave()
    {
        using var world = new G1World();
        Assert.True(WiredBoxRegistry.TryGet(Board, out var descriptor));
        var item = world.AddItem(Board, InteractionType.WiredEffect);
        var box = world.Wired.CreateConfiguredBox(item) ?? throw new InvalidOperationException(Board);
        Assert.True(world.Wired.AddBox((Plus.HabboHotel.Items.Wired.IWiredItem)box));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { Text = Text };
        Assert.True(WiredNativeEditorProjection.TryCompile(item.Id, descriptor, native, out var runtime, WiredNativeEditorProjection.AnyTriggerContext));
        var revisions = new List<int>();

        for (var save = 0; save < 3; save++) {
            Assert.True(WiredConfigurationSave.TrySave(box, runtime, TestWiredConfigurationStore.Instance, out _, id => world.Floor.ContainsKey(id),
                world.Wired.PublishConfigured));
            revisions.Add(Assert.Single(world.Wired.LeaderboardDescriptors()).QueryRevision);
        }

        Assert.Equal([2, 3, 4], revisions);
    }
}
