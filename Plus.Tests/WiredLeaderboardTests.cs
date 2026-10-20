using System.Collections.Immutable;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Leaderboard;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

/// <summary>Variable Leaderboard overlay packets and ranking (VariableLeaderboard*, WIN63-202610131318-860371703).</summary>
public sealed class WiredLeaderboardTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static WiredLeaderboardConfig Config(int sort, bool filter = false, bool comparison = false) => new()
    {
        Sort = sort,
        Filter = filter,
        Comparison = comparison,
        Title = "Top",
        Columns = [new(0, "Name", 3), new(1, "Coins", 0)]
    };

    private static Dictionary<WiredVariableKey, WiredVariableValue> Holders(params (long User, long Value, int Created, int Updated)[] rows) =>
        rows.ToDictionary(row => new WiredVariableKey(9, WiredVariableTarget.User, row.User),
            row => new WiredVariableValue(row.Value, T0.AddMinutes(row.Created), T0.AddMinutes(row.Updated)));

    [Theory]
    [InlineData(0, new long[] { 3, 1, 2 })]
    [InlineData(1, new long[] { 2, 1, 3 })]
    [InlineData(2, new long[] { 2, 3, 1 })]
    [InlineData(3, new long[] { 1, 3, 2 })]
    [InlineData(4, new long[] { 1, 2, 3 })]
    [InlineData(5, new long[] { 3, 2, 1 })]
    public void SortModesRankHighestLowestOldestAndLatest(int sort, long[] order)
    {
        var holders = Holders((1, 50, 30, 1), (2, 10, 10, 2), (3, 90, 20, 3));

        Assert.Equal(order, WiredLeaderboardRanking.Rank(Config(sort), holders, null, null).Select(row => row.UserId));
    }

    [Fact]
    public void TheFilterKeepsHoldersOfTheFilterVariableAndTheComparisonMatchesItsOperand()
    {
        var holders = Holders((1, 50, 0, 0), (2, 10, 0, 0), (3, 90, 0, 0));
        var filter = Holders((1, 7, 0, 0), (3, 8, 0, 0));

        Assert.Equal([3L, 1L], WiredLeaderboardRanking.Rank(Config(0, filter: true), holders, filter, null).Select(row => row.UserId));
        Assert.Equal([3L], WiredLeaderboardRanking.Rank(Config(0, filter: true, comparison: true), holders, filter, 8).Select(row => row.UserId));
        Assert.Empty(WiredLeaderboardRanking.Rank(Config(0, filter: true), holders, null, null));
    }

    [Fact]
    public void ChunksAreFiftyRowsRankedFromTheTopOfTheWholeList()
    {
        var holders = Holders([.. Enumerable.Range(1, 120).Select(user => ((long)user, (long)user, 0, 0))]);
        var ranked = WiredLeaderboardRanking.Rank(Config(0), holders, null, null);
        var choice = new WiredLeaderboardChoice(new(new(9, 1, 1, "coins", WiredVariableTarget.User, WiredVariableAvailability.Persistent, true), true, false),
            null, new Dictionary<int, string> { [120] = "Gold" });
        var profiles = new Profiles();

        var second = WiredLeaderboardRanking.Chunk(ranked, 1, choice, profiles);
        var third = WiredLeaderboardRanking.Chunk(ranked, 2, choice, profiles);
        var first = WiredLeaderboardRanking.Chunk(ranked, 0, choice, profiles);

        Assert.Equal(50, second.Count);
        Assert.Equal((51, 70, "u70", "70"), (second[0].Rank, second[0].UserId, second[0].Username, second[0].RawValue));
        Assert.Equal(20, third.Count);
        Assert.Equal(120, third[^1].Rank);
        Assert.Equal("Gold", first[0].TextConnection);
        Assert.Null(first[1].TextConnection);
        Assert.Null(first[0].Level);
    }

    // Observed on AIR WIN63-202610151745-318700621: the row times are 0 unless the board shows a last-update (2) or creation (3) column.
    [Fact]
    public void RowTimesGoOutOnlyForShownTimeColumns()
    {
        var holders = Holders((1, 5, 1, 2));
        var ranked = WiredLeaderboardRanking.Rank(Config(0), holders, null, null);
        var choice = new WiredLeaderboardChoice(new(new(9, 1, 1, "coins", WiredVariableTarget.User, WiredVariableAvailability.Persistent, true), true, false),
            null, new Dictionary<int, string>());
        var plain = WiredLeaderboardRanking.Chunk(ranked, 0, choice, new Profiles(), Config(0))[0];
        Assert.Equal((0L, 0L), (plain.CreationTime, plain.LastUpdateTime));
        var timed = WiredLeaderboardRanking.Chunk(ranked, 0, choice, new Profiles(), Config(0) with
        {
            Columns = [new(0, "Name", 3), new(1, "Coins", 0), new(2, "Updated", 0), new(3, "Created", 0)]
        })[0];
        Assert.Equal((T0.AddMinutes(1).ToUnixTimeMilliseconds(), T0.AddMinutes(2).ToUnixTimeMilliseconds()), (timed.CreationTime, timed.LastUpdateTime));
    }

    [Fact]
    public void SnapshotDescriptorFollowsClass2737()
    {
        var config = Config(5) with { Description = "Best", TileX = 4, TileY = 6, OffsetX = -3, Altitude = 120, PageSizeOption = 1 };
        var packet = new HabbiconTestSupport.RecordingPacket();
        new WiredVariableLeaderboardSnapshotComposer([new(77, 2, config, [new("user:9", "Coins")], "user:9")]).Compose(packet);

        Assert.Equal(new object[] { 1, 77, 2, "Top", "Best", 4, 6, -3, 120, 5, 2, 0, "Name", 3, 1, "Coins", 0, 1, "user:9", "Coins", "user:9", 1 },
            packet.Writes);
        Assert.Equal(3516u, new WiredVariableLeaderboardSnapshotComposer([]).MessageId);
    }

    [Fact]
    public void ChunkRowsWriteTheirTimesAsTwoWordLongs()
    {
        var time = 1_760_443_200_123L;
        var packet = new HabbiconTestSupport.RecordingPacket();
        new WiredVariableLeaderboardChunkComposer(new(77, 2, "user:9", 0, 1,
            [new(1, 5, "alice", "hd-180", "1,000", time, 0, "3", null)])).Compose(packet);

        Assert.Equal(new object[] { 77, 2, "user:9", 0, 1, 1, 1, 5, "alice", "hd-180", "1,000",
            (int)(time >> 32), unchecked((int)time), 0, 0, true, "3", false }, packet.Writes);
        Assert.Equal(3948u, new WiredVariableLeaderboardChunkComposer(new(0, 0, "", 0, 0, [])).MessageId);
    }

    [Fact]
    public void EditorLoadCarriesTheLeaderboardContextAsSectionEight()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_var_leaderboard", out var descriptor));
        var runtime = WiredNativeEditorProjection.DefaultRuntime(3, descriptor)!;
        var choice = new WiredLeaderboardChoice(new(new(9, 1, 1, "coins", WiredVariableTarget.User, WiredVariableAvailability.Persistent, true), true, false),
            null, new Dictionary<int, string>());
        var packet = new HabbiconTestSupport.RecordingPacket();

        WiredLegacyProtocol.Write(packet, 3, 19408, descriptor, runtime, 100, [], catalogHash: 5, leaderboard: new(0, 4, 6, 1, [choice]));

        // Two sections (the catalog hash, then the leaderboard context), then the 19 owned defaults.
        var tail = packet.Writes.TakeLast(34).ToArray();
        Assert.Equal(new object[] { 2, 0, 5, 8, 0, 4, 6, 1, 1, "user:9", "coins", true, true, 0, 19 }, tail.Take(15).ToArray());
    }

    private sealed class Profiles : IWiredLeaderboardProfiles
    {
        public IReadOnlyDictionary<long, (string Username, string Figure)> Find(IReadOnlyCollection<long> userIds) =>
            userIds.ToDictionary(id => id, id => ("u" + id, "fig"));
    }
}
