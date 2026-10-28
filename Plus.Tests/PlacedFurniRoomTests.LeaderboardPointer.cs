using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Leaderboard;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A leaderboard offers the user variables on its tile. A pointer on that tile is never one of them: a board that stands on a pointer
/// alone offers no variable. Every box is saved from its raw packet through the real save events.
/// </summary>
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(NamedUser, true)]
    [InlineData(NamedPointer, false)]
    public async Task ALeaderboardOffersTheUserVariableOnItsTileButNeverAPointer(uint variable, bool offered)
    {
        var service = NamedVariableRoom();
        _room.GetWired().LeaderboardProfiles = new BoardProfiles();
        PlaceSavedBox(BoardBox, "wf_xtra_var_leaderboard", (int)(variable % 4), (int)(variable / 4 % 4), 2);
        int[] ints = [0, 3, 0, 1, 5, -1, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 2, 0, 0];
        var text = "lb probe\t\tName\tValue\tText" + new string('\t', 12);
        _client.Packets.Clear();
        await new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket([(int)BoardBox, ints.Length, .. ints.Cast<object>(), text, 0, 0, 0,
            WiredLeaderboardConfig.VariableCount, .. Enumerable.Repeat((object)"0", WiredLeaderboardConfig.VariableCount), 0]));
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);

        var board = Assert.Single(_room.GetWired().LeaderboardDescriptors());
        Assert.Equal(offered ? 1 : 0, board.Choices.Count);
        Assert.Equal(offered ? board.Choices[0].VariableId : "0", board.DefaultVariableId);
    }
}
