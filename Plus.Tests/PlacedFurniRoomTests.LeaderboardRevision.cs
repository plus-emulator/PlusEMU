using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Leaderboard;
using Xunit;

namespace Plus.Tests;

// The query revision of a board comes from one counter for the room. The counter starts at 0, so the first board save shows revision 1,
// and every board save adds one, also an unchanged save. A save of another box adds nothing.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public async Task TheFirstBoardSaveOfARoomShowsRevisionOne()
    {
        var (_, _, revision) = await PublishedBoard();

        Assert.Equal(1, revision);
    }

    [Fact]
    public async Task EveryBoardSaveAddsOneRevisionAlsoAnUnchangedSave()
    {
        var (clock, service, revision) = await PublishedBoard();

        foreach (var (at, title) in new[] { (20000, "lb probe"), (24000, "lb probe"), (28000, "other title") }) {
            await SaveBoard(clock, service, at, title);
            Assert.Equal(++revision, Assert.Single(BoardSnapshots()));
        }
    }

    [Fact]
    public async Task ASaveOfAnotherBoxLeavesTheRevisionCounterAlone()
    {
        var (clock, service, revision) = await PublishedBoard();

        clock.Now = BoardOrigin.AddMilliseconds(20000);
        _client.Packets.Clear();
        await new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket((int)OtherConnector, 0, "1=q", 0, 0, 0, 0, 0));
        Assert.Empty(BoardSnapshots());

        await SaveBoard(clock, service, 24000, "lb probe");
        Assert.Equal(revision + 1, Assert.Single(BoardSnapshots()));
    }

    private async Task SaveBoard(SpeechClock clock, WiredConfigurationService service, int at, string title)
    {
        int[] ints = [0, 3, 0, 1, 5, -1, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 2, 0, 0];
        var text = title + "\t\tName\tValue\tText" + new string('\t', 12);
        clock.Now = BoardOrigin.AddMilliseconds(at);
        _client.Packets.Clear();
        await new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket([(int)BoardBox, ints.Length, .. ints.Cast<object>(), text, 0, 0, 0,
            WiredLeaderboardConfig.VariableCount, .. Enumerable.Repeat((object)"0", WiredLeaderboardConfig.VariableCount), 0]));
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
    }
}
