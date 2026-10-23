using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.Items.Wired.Leaderboard;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The text of a leaderboard row for a variable with a Text Connector. Observed on AIR WIN63-202610201032-107964540 (a board with a
/// text column over a variable with the connector, values 5 and 9 given by a Says box): a labelled value shows its label, an unlabelled
/// value shows the "_=" fallback (empty when the fallback is empty), and with no fallback line the row has no text.
/// </summary>
public sealed class WiredLeaderboardConnectorFallbackTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly WiredLeaderboardConfig Config = new()
    {
        Title = "Top",
        Columns = [new(0, "Name", 3), new(1, "Coins", 0), new(5, "Text", 2)]
    };

    private static WiredLeaderboardRow Row(string connectorText, long value)
    {
        var holders = new Dictionary<WiredVariableKey, WiredVariableValue> { [new(9, WiredVariableTarget.User, 1)] = new(value, T0, T0) };
        var choice = new WiredLeaderboardChoice(new(new(9, 1, 1, "lbv", WiredVariableTarget.User, WiredVariableAvailability.Persistent, true), true, false),
            null, WiredVariableMetadataBox.ParseConnector(connectorText));

        return Assert.Single(WiredLeaderboardRanking.Chunk(WiredLeaderboardRanking.Rank(Config, holders, null, null), 0, choice, new Profiles(), Config));
    }

    [Theory]
    [InlineData("5=five\n_=other", 5, "five")]
    [InlineData("5=five\n_=other", 9, "other")]
    [InlineData("_=other", 5, "other")]
    [InlineData("_=other", 9, "other")]
    [InlineData("5=five", 5, "five")]
    [InlineData("5=five", 9, null)]
    [InlineData("5=five\n_=", 5, "five")]
    [InlineData("5=five\n_=", 9, "")]
    [InlineData("5=five\n_=a=b", 9, "a=b")]
    [InlineData("7=seven", 5, null)]
    [InlineData("7=seven", 9, null)]
    public void AnUnlabelledValueShowsTheFallbackAndALabelledValueKeepsItsLabel(string connector, long value, string? expected) =>
        Assert.Equal(expected, Row(connector, value).TextConnection);

    // The row's text is written as a flag and a string, so an empty fallback is still present on the wire.
    [Theory]
    [InlineData("5=five\n_=", 9, true, "")]
    [InlineData("5=five", 9, false, null)]
    public void AnEmptyFallbackIsSentAsAPresentEmptyText(string connector, long value, bool present, string? text)
    {
        var packet = new HabbiconTestSupport.RecordingPacket();
        new WiredVariableLeaderboardChunkComposer(new(1, 2, "user:9", 0, 1, [Row(connector, value)])).Compose(packet);
        var tail = packet.Writes.SkipWhile(item => item is not "fig").Skip(1).ToArray();

        Assert.Equal(present, tail.OfType<bool>().Last());
        Assert.Equal(text, tail.LastOrDefault() as string);
    }

    private sealed class Profiles : IWiredLeaderboardProfiles
    {
        public IReadOnlyDictionary<long, (string Username, string Figure)> Find(IReadOnlyCollection<long> userIds) =>
            userIds.ToDictionary(id => id, id => ("u" + id, "fig"));
    }
}
