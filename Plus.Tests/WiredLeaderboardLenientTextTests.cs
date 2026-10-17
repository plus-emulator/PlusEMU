using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Leaderboard;
using Xunit;

namespace Plus.Tests;

public class WiredLeaderboardLenientTextTests
{
    private static readonly ImmutableArray<string> NoVariables = [.. Enumerable.Repeat("0", WiredLeaderboardConfig.VariableCount)];

    // Observed on AIR WIN63-202610151745-318700621: one column, no override, an 8-field text with an override name; accepted and read
    // back as the title, the column label and 14 empty fields.
    [Fact]
    public void AShortTextIsPaddedAndUnusedSlotsAreEmptied()
    {
        ImmutableArray<int> ints = [0, 1, 0, -1, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0];
        const string sent = "lb probe\t\tValue\t\t\t\t\tOwner";

        Assert.Equal("lb probe\t\tValue" + new string('\t', 14), WiredLeaderboardConfig.Normalize(sent, ints));
        var config = WiredLeaderboardConfig.Decode(ints, NoVariables, sent);
        Assert.NotNull(config);
        Assert.Equal("lb probe", config!.Title);
        Assert.Equal("Value", Assert.Single(config.Columns).Label);
        Assert.Empty(config.Overrides);

        // The saved configuration keeps the hotel's form, so the editor reads it back like the hotel does.
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_var_leaderboard", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = ints, Text = sent };
        Assert.True(WiredNativeEditorProjection.TryCompile(1, descriptor, native, out var runtime));
        Assert.Equal("lb probe\t\tValue" + new string('\t', 14), runtime.Origin!.Native.Text);
    }

    // Observed on AIR WIN63-202610151745-318700621: an override variable with no override counted is refused with the leaderboard key.
    [Fact]
    public void AnOverrideVariableWithoutAnOverrideIsRefusedWithTheLeaderboardKey()
    {
        var ids = NoVariables.SetItem(2, "306999");

        Assert.Null(WiredLeaderboardConfig.Decode(WiredLeaderboardConfig.DefaultInts, ids, ""));
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_var_leaderboard", out var descriptor));
        Assert.Equal("wiredfurni.error.variable_leaderboard.invalid_configuration",
            WiredNativeEditorProjection.SourceRejectionKey(descriptor, WiredNativeEditorProjection.DefaultNative(descriptor)));
    }
}
