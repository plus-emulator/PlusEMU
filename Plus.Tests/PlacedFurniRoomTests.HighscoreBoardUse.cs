using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Quests;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Using a high score board switches its state between 0 and 1. The room gets one ObjectDataUpdate with the
    // whole board, and state 1 makes the client show the board.
    [Fact]
    public async Task UsingAHighScoreBoardTogglesItsStateAndSendsTheWholeBoard()
    {
        PrepareSpeech(new SpeechClock());
        var board = HighscoreBoard(601, 1);
        board.Definition.Modes = 2;
        board.ExtraData = new HighscoreDataFormat { State = "", ScoreType = 1, Entries = [new(3, ["owner"])] };
        board.SetState(2, 2, 0);
        _room.GetRoomItemHandler().LoadFurniture([board]);
        var handler = new UseFurnitureEvent(new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null)));
        _client.Packets.Clear();

        await handler.Parse(_room, _client, UsePacket(board.Id, 0));

        Assert.Equal("1", Assert.IsType<HighscoreDataFormat>(board.ExtraData).State);
        var update = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer);
        Assert.Equal(BoardData("601", "1", 1, 0, (3, ["owner"])), update.Body);
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
        _client.Packets.Clear();

        await handler.Parse(_room, _client, UsePacket(board.Id, 0));

        Assert.Equal("0", Assert.IsType<HighscoreDataFormat>(board.ExtraData).State);
        Assert.Equal(BoardData("601", "0", 1, 0, (3, ["owner"])),
            Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer).Body);
        var entry = Assert.Single(((HighscoreDataFormat)board.ExtraData).Entries);
        Assert.Equal(3, entry.Score);
        Assert.Equal(["owner"], entry.Users.ToArray());
    }

    [Fact]
    public void ANewHighScoreBoardStartsAtStateZero()
    {
        var definition = HighscoreBoard(602, 2).Definition;
        definition.ItemName = "highscore_classic*2";

        var board = Assert.IsType<HighscoreDataFormat>(FurniExtraData.Load(definition, "", keepLegacy: true));

        Assert.Equal("0", board.State);
        Assert.Equal(2u, board.ScoreType);
        Assert.Equal(1u, board.ClearType);
    }

    private static FlashIncomingPacket UsePacket(uint id, int parameter)
    {
        using var stream = PlusMemoryStream.GetStream();
        var packet = new FlashOutgoingPacket(stream);
        packet.WriteInteger((int)id);
        packet.WriteInteger(parameter);

        return new() { Buffer = stream.ToArray().AsMemory(6) };
    }

    private static byte[] BoardData(string id, string state, int scoreType, int clearType, params (int Score, string[] Users)[] entries)
    {
        using var stream = PlusMemoryStream.GetStream();
        var packet = new FlashOutgoingPacket(stream);
        packet.WriteString(id);
        packet.WriteInteger(6);
        packet.WriteString(state);
        packet.WriteInteger(scoreType);
        packet.WriteInteger(clearType);
        packet.WriteInteger(entries.Length);

        foreach (var (score, users) in entries) {
            packet.WriteInteger(score);
            packet.WriteInteger(users.Length);

            foreach (var user in users) {
                packet.WriteString(user);
            }
        }

        return stream.ToArray()[6..];
    }
}
