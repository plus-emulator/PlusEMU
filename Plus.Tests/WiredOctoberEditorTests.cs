using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Leaderboard;
using Plus.HabboHotel.Items.Wired.Links;
using Xunit;

namespace Plus.Tests;

/// <summary>The 22 October boxes: registry entries and the editors' save grammar.</summary>
public sealed class WiredOctoberEditorTests
{
    [Theory]
    [InlineData("wf_act_progress_ach", WiredBoxCategory.Action, 51)]
    [InlineData("wf_act_give_effect", WiredBoxCategory.Action, 52)]
    [InlineData("wf_act_override_height", WiredBoxCategory.Action, 53)]
    [InlineData("wf_act_progress_reward_track", WiredBoxCategory.Action, 58)]
    [InlineData("wf_act_reset_reward_track", WiredBoxCategory.Action, 59)]
    [InlineData("wf_act_set_visibility", WiredBoxCategory.Action, 61)]
    [InlineData("wf_act_set_highlight", WiredBoxCategory.Action, 62)]
    [InlineData("wf_act_send_message", WiredBoxCategory.Action, 63)]
    [InlineData("wf_act_send_dataframe", WiredBoxCategory.Action, 64)]
    [InlineData("wf_trg_room_initializes", WiredBoxCategory.Trigger, 28)]
    [InlineData("wf_trg_receive_message", WiredBoxCategory.Trigger, 29)]
    [InlineData("wf_trg_receive_dataframe", WiredBoxCategory.Trigger, 30)]
    [InlineData("wf_cnd_user_level", WiredBoxCategory.Condition, 44)]
    [InlineData("wf_xtra_var_share", WiredBoxCategory.Addon, 1003)]
    [InlineData("wf_xtra_var_import", WiredBoxCategory.Addon, 1004)]
    [InlineData("wf_xtra_var_leaderboard", WiredBoxCategory.Addon, 1005)]
    [InlineData("wf_xtra_global_placeholder", WiredBoxCategory.Addon, 2000)]
    [InlineData("wf_xtra_achievement_enabler", WiredBoxCategory.Addon, 2001)]
    [InlineData("wf_xtra_var_web_api", WiredBoxCategory.Addon, 2002)]
    [InlineData("wf_xtra_link_speaker", WiredBoxCategory.Addon, 2003)]
    [InlineData("wf_var_dailytask", WiredBoxCategory.Variable, 8)]
    [InlineData("wf_var_pointer", WiredBoxCategory.Variable, 9)]
    public void EveryBoxIsRegisteredAirSourceOnlyAndOpensOnItsCompiledDefaults(string name, WiredBoxCategory category, int code)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        Assert.Equal(category, descriptor.Category);
        Assert.Equal(code, descriptor.EditorCode);
        Assert.Equal(name == "wf_xtra_var_leaderboard" ? WiredEvidenceLevel.Observed : WiredEvidenceLevel.AirSourceOnly, WiredEvidenceRegistry.LevelOf(name));
        Assert.True(WiredNativeEditorProjection.Supports(name));
        Assert.NotNull(WiredNativeEditorProjection.DefaultRuntime(1, descriptor));
    }

    [Theory]
    [InlineData("wf_act_set_visibility", new[] { 1, 100, 1, 4, 1 }, true)]
    [InlineData("wf_act_set_visibility", new[] { 0, 50, 0, 0, 0 }, false)]
    [InlineData("wf_act_set_highlight", new[] { 1, 5, 100, 0xFFFFFF, 1, 4, 0 }, true)]
    [InlineData("wf_act_set_highlight", new[] { 0, 4, 38, 0, 1, 0, 0 }, true)]
    [InlineData("wf_act_set_highlight", new[] { 0, 4, 50, 0, 1, 0, 0 }, false)]
    [InlineData("wf_act_set_highlight", new[] { 0, 6, 0, 0, 0, 0, 0 }, false)]
    [InlineData("wf_act_set_highlight", new[] { 0, 0, 0, 0x1000000, 0, 0, 0 }, false)]
    [InlineData("wf_act_give_effect", new[] { 10000, 2, 1 }, true)]
    [InlineData("wf_act_give_effect", new[] { 10001, 0, 0 }, false)]
    [InlineData("wf_act_give_effect", new[] { 0, 3, 0 }, false)]
    [InlineData("wf_act_override_height", new[] { 8000, 1 }, true)]
    [InlineData("wf_act_override_height", new[] { 8001, 0 }, false)]
    [InlineData("wf_cnd_user_level", new[] { 30, 2 }, true)]
    [InlineData("wf_cnd_user_level", new[] { 0, 0 }, false)]
    [InlineData("wf_cnd_user_level", new[] { 31, 0 }, false)]
    [InlineData("wf_cnd_user_level", new[] { 1, 3 }, false)]
    [InlineData("wf_xtra_var_share", new[] { 0, 1, 0 }, true)]
    // Any non-zero flag saves and is stored as 1.
    [InlineData("wf_xtra_var_import", new[] { 2, 0, 0 }, true)]
    [InlineData("wf_xtra_link_speaker", new[] { 1, 0 }, true)]
    [InlineData("wf_xtra_link_speaker", new[] { 1, 2 }, false)]
    [InlineData("wf_var_pointer", new[] { -20 }, true)]
    // A target the form does not offer saves and is stored as furni.
    [InlineData("wf_var_pointer", new[] { 2 }, true)]
    public void IntFieldsFollowTheEditorRanges(string name, int[] ints, bool accepted) =>
        Assert.Equal(accepted, WiredOctoberEditorParityTests.TryCompile(name, native => native with { OwnedIntParams = [.. ints] }));

    [Fact]
    public void UserLevelAdvertisesTheUserConditionFooter()
    {
        // Both the level condition and the badge condition get one user source and a user quantifier.
        var level = WiredNativeEditorProjection.Metadata("wf_cnd_user_level");
        var badge = WiredNativeEditorProjection.Metadata("wf_cnd_wearing_badge");

        Assert.Equal(badge.UsersAllowed.Single().ToArray(), level.UsersAllowed.Single().ToArray());
        Assert.Equal((badge.QuantifierType, badge.FurniAllowed.Length), (level.QuantifierType, level.FurniAllowed.Length));
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_cnd_user_level", native => native with { UserSourceTypes = [200], Quantifier = 1 }));
        // 11 is offered only when the stack has a Click User trigger.
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_cnd_user_level", native => native with { UserSourceTypes = [11], Quantifier = 1 }));
    }

    [Fact]
    public void ProgressAchievementTakesValueOrVariableAndTheAchievementName()
    {
        var runtime = WiredOctoberEditorParityTests.Compile("wf_act_progress_ach", native => native with
        {
            OwnedIntParams = [0, 1, 25, 1],
            Text = "ACH_RoomEntry",
            VariableIds = ["user:9"],
            UserSourceTypes = [200, 0],
            FurniSourceTypes = [100]
        });

        // Target 1 (user) is the scalar user target 0; recipients and the reference keep their own sources.
        Assert.Equal(new[] { 0, 1, 25, 0 }, runtime.IntParams);
        Assert.Equal(200, runtime.UserSources["users"]);
        Assert.Equal(100, runtime.FurniSources["reference"]);
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_progress_ach", native => native with { OwnedIntParams = [2, 0, 0, -10] }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_progress_ach", native => native with { OwnedIntParams = [1, 0, -1, -10] }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_progress_ach", native => native with { OwnedIntParams = [1, 0, 0, 5] }));
    }

    [Theory]
    [InlineData("season_1\ttask_a", new[] { 1, 0, 1, -10 }, true)]
    [InlineData("season_1\ttask a", new[] { 1, 0, 1, -10 }, false)]
    [InlineData("season_1", new[] { 1, 0, 1, -10 }, false)]
    [InlineData("season_1\ttask_a", new[] { 1, 0, 0, -10 }, false)]
    [InlineData("season_1\ttask_a", new[] { 0, 1, 0, -20 }, true)]
    public void ProgressRewardTrackSavesTrackTabTaskAndAScoreOfAtLeastOne(string text, int[] ints, bool accepted) =>
        Assert.Equal(accepted, WiredOctoberEditorParityTests.TryCompile("wf_act_progress_reward_track", native => native with { Text = text, OwnedIntParams = [.. ints] }));

    [Fact]
    public void ResetRewardTrackTakesOneTrackIdOfAtMostAHundredCharacters()
    {
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_act_reset_reward_track", native => native with { Text = new string('a', 100) }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_reset_reward_track", native => native with { Text = new string('a', 101) }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_reset_reward_track", native => native with { Text = "a\tb" }));
    }

    [Fact]
    public void SendLinkMessageCountsUtf8Bytes()
    {
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_act_send_message", native => native with { Text = new string('a', 1000) }));
        // The limit counts UTF-8 bytes: 500 two-byte characters fill the 1000 bytes.
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_act_send_message", native => native with { Text = new string('é', 500) }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_send_message", native => native with { Text = new string('é', 501) }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_send_message", native => native with { OwnedIntParams = [2] }));
    }

    [Fact]
    public void SendDataframeIsThirtySevenIntsRowsAsJsonAndTwelveVariables()
    {
        var ints = SendInts((0, 42, -10), (3, 0, -10));
        var runtime = WiredOctoberEditorParityTests.Compile("wf_act_send_dataframe", native => native with
        {
            OwnedIntParams = ints,
            Text = "[[\"score\",\"\"],[\"greeting\",\"hello\"]]"
        });

        Assert.Equal(2, runtime.IntParams[0]);
        Assert.Equal(new[] { 0, 42, 3 }, runtime.IntParams.Skip(1).Take(3));
        Assert.Equal(13, runtime.FurniSources.Count);
        Assert.Equal(12, runtime.UserSources.Count);
        // A row count that the JSON does not match, a bad key and an over-long custom message are refused like LinkDataframeCodec does.
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_send_dataframe", native => native with { OwnedIntParams = ints, Text = "[[\"score\",\"\"]]" }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_send_dataframe", native => native with
        {
            OwnedIntParams = ints,
            Text = "[[\"sco-re\",\"\"],[\"greeting\",\"hello\"]]"
        }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_send_dataframe", native => native with
        {
            OwnedIntParams = ints,
            Text = "[[\"score\",\"\"],[\"greeting\",\"" + new string('a', 201) + "\"]]"
        }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_act_send_dataframe", native => native with { OwnedIntParams = [.. ints.Take(36)] }));
    }

    [Fact]
    public void ReceiveDataframeRowsArePlaceholdersOrVariables()
    {
        ImmutableArray<int> ints = [2, 0, 1, .. Enumerable.Repeat(0, 10)];

        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_trg_receive_dataframe", native => native with
        {
            OwnedIntParams = ints,
            Text = "[[\"greeting\",\"greeting\"],[\"score\",\"\"]]"
        }));
        // A placeholder row cannot fill the trigger's own $(trigger_text).
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_trg_receive_dataframe", native => native with
        {
            OwnedIntParams = ints,
            Text = "[[\"greeting\",\"trigger_text\"],[\"score\",\"\"]]"
        }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_trg_receive_dataframe", native => native with
        {
            OwnedIntParams = [2, 0, 2, .. Enumerable.Repeat(0, 10)],
            Text = "[[\"greeting\",\"greeting\"],[\"score\",\"\"]]"
        }));
    }

    [Theory]
    [InlineData("score\tHello world", new[] { 0, 0, 0 }, true)]
    [InlineData("trigger_text\tHello", new[] { 0, 0, 0 }, false)]
    [InlineData("score\tHello", new[] { 0, 0, 3 }, false)]
    [InlineData("score", new[] { 1, 0, 3 }, true)]
    [InlineData("score", new[] { 0, 1, 0 }, false)]
    public void GlobalPlaceholderIsANameTabTextOrARoomIndex(string text, int[] ints, bool accepted) =>
        Assert.Equal(accepted, WiredOctoberEditorParityTests.TryCompile("wf_xtra_global_placeholder", native => native with { Text = text, OwnedIntParams = [.. ints] }));

    [Fact]
    public void WebApiKeysAreServerKeysAndBulkDeleteNeedsAWriteKey()
    {
        var read = WiredWebApiKeyFormat.Create();
        var write = WiredWebApiKeyFormat.Create();

        Assert.True(WiredWebApiKeyFormat.IsKey(read));
        Assert.NotEqual(read, write);
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_xtra_var_web_api", native => native with { Text = read + "\t" + write, OwnedIntParams = [1] }));
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_xtra_var_web_api", native => native with { Text = read + "\t", OwnedIntParams = [0] }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_xtra_var_web_api", native => native with { Text = read + "\t", OwnedIntParams = [1] }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_xtra_var_web_api", native => native with { Text = "guess\t", OwnedIntParams = [0] }));
    }

    [Fact]
    public void AchievementEnablerAndDailyTaskKeepTheirTextLimits()
    {
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_xtra_achievement_enabler", native => native with { Text = "ACH_A\nACH_B" }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_xtra_achievement_enabler", native => native with { Text = new string('a', 2001) }));
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_var_dailytask", native => native with { Text = "tasks\t" + new string('1', 100) }));
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_var_dailytask", native => native with { Text = "tasks\t" + new string('1', 101) }));
    }

    [Fact]
    public void LeaderboardDefaultIsTheUnpublishedUsernameAndValueBoard()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_var_leaderboard", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        var config = WiredLeaderboardConfig.Decode(native.OwnedIntParams, native.VariableIds, native.Text);

        Assert.NotNull(config);
        Assert.False(config.Published);
        Assert.Equal(new WiredLeaderboardColumn[] { new(0, "", 3), new(1, "", 0) }, config.Columns.ToArray());
        Assert.Equal(10, config.PageSize);
    }

    [Fact]
    public void PublishedLeaderboardRoundTripsTheCodecFields()
    {
        // Sort latest update, 3 columns (Username XL, Value Small, Last update Medium), filter with a literal comparison, two overrides.
        ImmutableArray<int> ints = [5, 3, 0, 1, 2, -1, 1, 1, 0, 2, 4, 6, -8, 100, 3, 0, 1, 0, 2];
        ImmutableArray<string> ids = ["user:7", "0", "user:7", "user:8", "0", "0", "0", "0", "0", "0", "0", "0"];
        var text = string.Join('\t', ["Top", "Best players", "Name", "Coins", "Seen", "", "-9000000000", "Gold", "Silver", .. Enumerable.Repeat("", 8)]);

        var config = WiredLeaderboardConfig.Decode(ints, ids, text);

        Assert.NotNull(config);
        Assert.Equal(-9_000_000_000L, config.Literal);
        Assert.Equal(20, config.PageSize);
        Assert.Equal(["user:7", "user:8"], config.Overrides.Select(entry => entry.VariableId));
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_xtra_var_leaderboard", native => native with { OwnedIntParams = ints, VariableIds = ids, Text = text }));

        // The first column must be Username, types may not repeat, an enabled filter needs its variable, literals are canonical.
        Assert.Null(WiredLeaderboardConfig.Decode(ints.SetItem(2, 1).SetItem(3, 0), ids, text));
        Assert.Null(WiredLeaderboardConfig.Decode(ints.SetItem(4, 1), ids, text));
        Assert.Null(WiredLeaderboardConfig.Decode(ints, ids.SetItem(0, "0"), text));
        Assert.Null(WiredLeaderboardConfig.Decode(ints, ids, text.Replace("-9000000000", "+9000000000")));
        Assert.Null(WiredLeaderboardConfig.Decode(ints.SetItem(12, 33), ids, text));
        Assert.Null(WiredLeaderboardConfig.Decode(ints, ids, text.Replace("Top", new string('t', 51))));
    }

    [Fact]
    public void LeaderboardDisplayNamesFollowTheClientFormatter()
    {
        Assert.Equal("Coins Earned", WiredLeaderboardConfig.SuggestName("coins_EARNED"));
        Assert.Equal("Variable", WiredLeaderboardConfig.SuggestName("__"));
    }

    [Fact]
    public void EditorRecordsRoundTripThroughTheSaveAndLoadPackets()
    {
        // A client save of Set Furni Highlight (out 1789 envelope: ints, text, picks, delay, furni sources, user sources, variables, picks 2).
        var packet = EditorTestSupport.Incoming(7, 1, 3, 50, 0xFF3030, 0, 2, 0, "", 1, 55, 0, 1, 100, 2, 200, 0, 0, 0);
        Assert.True(WiredLegacyProtocol.TryReadNative(packet, WiredBoxCategory.Action, out var native));
        Assert.True(WiredBoxRegistry.TryGet("wf_act_set_highlight", out var descriptor));
        native = native with { NativeCode = descriptor.EditorCode };
        Assert.True(WiredNativeEditorProjection.TryCompile(9, descriptor, native, out var runtime));

        var written = new HabbiconTestSupport.RecordingPacket();
        WiredLegacyProtocol.Write(written, 9, 19396, descriptor, runtime, 100, []);

        // furni limit, picks, picks 2, sprite, id, text, the seven ints, no variables, sources, code then the delay.
        Assert.Equal(new object[] { 100, 1, 55, 0, 19396, 9u, "", 7, 1, 3, 50, 0xFF3030, 0, 2, 0, 0, 1, 100, 2, 200, 0, 62, 0 },
            written.Writes.Take(23).ToArray());
    }

    [Fact]
    public void DataframeKeysNormalizeLikeTheClient()
    {
        Assert.Equal("high_score", WiredLinkDataframeCodec.NormalizeKey("  High Score_ "));
        Assert.True(WiredLinkDataframeCodec.IsKey("High Score"));
        Assert.False(WiredLinkDataframeCodec.IsKey(new string('a', 41)));
        Assert.Equal(new[] { ("a", "1") }, WiredLinkDataframeCodec.Decode(WiredLinkDataframeCodec.Encode([("a", "1")]), 1)!.Value.ToArray());
        Assert.Null(WiredLinkDataframeCodec.Decode("[[\"a\",1]]", 1));
    }

    private static ImmutableArray<int> SendInts(params (int Mode, int Literal, int Target)[] rows) =>
        [rows.Length, .. Enumerable.Range(0, 12).SelectMany(index => index < rows.Length
            ? new[] { rows[index].Mode, rows[index].Literal, rows[index].Target } : new[] { 0, 0, -10 })];

    // The editors write 1..1200 pulses plus one, and 1201 saves.
    [Theory]
    [InlineData("wf_cnd_time_more_than")]
    [InlineData("wf_cnd_time_less_than")]
    public void TheTimeConditionsSaveTheirLargestEditorValue(string name)
    {
        Assert.True(WiredOctoberEditorParityTests.TryCompile(name, native => native with { OwnedIntParams = [1201] }));
        Assert.True(WiredOctoberEditorParityTests.TryCompile(name, native => native with { OwnedIntParams = [2] }));
        // Any threshold is stored, also above the editor's largest.
        Assert.True(WiredOctoberEditorParityTests.TryCompile(name, native => native with { OwnedIntParams = [1202] }));
    }
}
