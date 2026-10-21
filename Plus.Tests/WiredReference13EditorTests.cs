using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>The editor frames of the 13 reference cards: observed on hhus (r62 batches 01 and 02) or read from the October AIR client.</summary>
public sealed class WiredReference13EditorTests
{
    public static TheoryData<string, int[], int[], int[], int[][], int[][]> ObservedFrames => new()
    {
        { "wf_act_bot_teleport", [], [100], [100], [[0, 100, 200, 201]], [[0, 100, 200, 201]] },
        { "wf_act_bot_move", [], [100], [100], [[0, 100, 200, 201]], [[0, 100, 200, 201]] },
        { "wf_act_bot_talk", [0, -1, 0], [], [100], [], [[0, 100, 200, 201]] },
        { "wf_act_bot_talk_to_avatar", [0, -1, 0], [], [100, 0], [], [[0, 100, 200, 201], [0, 200, 201]] },
        { "wf_act_bot_follow_avatar", [1], [], [100, 0], [], [[0, 100, 200, 201], [0, 200, 201]] },
        { "wf_act_bot_give_handitem", [2], [], [100, 0], [], [[0, 100, 200, 201], [0, 200, 201]] },
        { "wf_act_bot_clothes", [], [], [100], [], [[0, 100, 200, 201]] },
        { "wf_trg_bot_reached_stf", [], [100], [100], [[100, 200]], [[100, 200]] },
        { "wf_trg_bot_reached_avtr", [], [], [100], [], [[100, 200]] },
        { "wf_trg_game_starts", [], [], [], [], [] },
        { "wf_trg_game_ends", [], [], [], [], [] }
    };

    [Theory]
    [MemberData(nameof(ObservedFrames))]
    public void TheInitialEditorMatchesTheObservedFrame(string name, int[] ints, int[] furni, int[] users, int[][] furniAllowed, int[][] usersAllowed)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        var metadata = WiredNativeEditorProjection.Metadata(name);
        Assert.Equal(ints, native.OwnedIntParams.ToArray());
        Assert.Equal(furni, native.FurniSourceTypes.ToArray());
        Assert.Equal(users, native.UserSourceTypes.ToArray());
        Assert.Equal("", native.Text);
        Assert.Equal(furniAllowed, metadata.FurniAllowed.Select(slot => slot.ToArray()).ToArray());
        Assert.Equal(usersAllowed, metadata.UsersAllowed.Select(slot => slot.ToArray()).ToArray());
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out _));
    }

    [Theory]
    [InlineData("wf_act_bot_teleport", 21)]
    [InlineData("wf_act_bot_move", 22)]
    [InlineData("wf_act_bot_talk", 23)]
    [InlineData("wf_act_bot_give_handitem", 24)]
    [InlineData("wf_act_bot_follow_avatar", 25)]
    [InlineData("wf_act_bot_clothes", 26)]
    [InlineData("wf_act_bot_talk_to_avatar", 27)]
    [InlineData("wf_trg_game_starts", 8)]
    [InlineData("wf_trg_game_ends", 9)]
    [InlineData("wf_trg_bot_reached_stf", 13)]
    [InlineData("wf_trg_bot_reached_avtr", 14)]
    [InlineData("wf_act_move_furni_to", 16)]
    public void TheEditorCodesAreTheOnesTheClientOpens(string name, int code)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        Assert.Equal(code, descriptor.EditorCode);
    }

    [Fact]
    public void TheTwoAirReadEditorsOpenWithTheirAs3Defaults()
    {
        // TriggerOnce (code 3, shared by at given time and at time long): one slider, 1..1200 half seconds. class_4162: direction 0, spacing 1.
        Assert.True(WiredBoxRegistry.TryGet("wf_trg_at_time_long", out var timer));
        Assert.Equal([1], WiredNativeEditorProjection.DefaultNative(timer).OwnedIntParams.ToArray());
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_furni_to", out var move));
        var native = WiredNativeEditorProjection.DefaultNative(move);
        Assert.Equal([0, 1], native.OwnedIntParams.ToArray());
        Assert.Equal([100, 101], native.FurniSourceTypes.ToArray());
    }

    [Theory]
    [InlineData("wf_act_bot_talk", 2, 0)]
    [InlineData("wf_act_bot_talk_to_avatar", 2, 0)]
    public void TalkEditorsTakeBubbleWidthsMinusOneToTwoAndATickedHideName(string name, int width, int hide)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [1, width, hide], Text = "Bobby\tHello" };
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out _));
        Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { OwnedIntParams = [1, 3, hide] }, out _));
        Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { OwnedIntParams = [2, width, hide] }, out _));
    }

    [Theory]
    [InlineData("wf_act_bot_teleport", 0)]
    [InlineData("wf_act_bot_follow_avatar", 1)]
    public void EveryBotNameBoxTakesThirtyTwoCharacters(string name, int ints)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        native = native with { OwnedIntParams = [.. native.OwnedIntParams.Take(ints)] };
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { Text = new string('b', 32) }, out _));
        Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { Text = new string('b', 33) }, out _));
    }

    // Observed on AIR WIN63-202610201032-107964540: Bot Reaches Avatar and Bot Reaches Furni keep a bot name of any length.
    [Theory]
    [InlineData("wf_trg_bot_reached_avtr")]
    [InlineData("wf_trg_bot_reached_stf")]
    public void BotReachesTriggersTakeALongerBotName(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { Text = new string('b', 33) }, out _));
    }

    [Fact]
    public void ThePositionOfTheBotsAndUsersSlotsFollowsTheEditor()
    {
        // Bots slot first (100 selected), users slot second (0 selected), as in the observed frames.
        Assert.True(WiredBoxRegistry.TryGet("wf_act_bot_follow_avatar", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { UserSourceTypes = [200, 201] };
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { Text = "Bobby" }, out var runtime));
        Assert.Equal(200, runtime.UserSources["bots"]);
        Assert.Equal(201, runtime.UserSources["users"]);
        Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { UserSourceTypes = [100, 10] }, out _));
    }
}
