using System.Data.Common;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.Database;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Rooms.Chat.Commands.User;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public async Task DisableDiagonalSavesTheRoomsSqueezeSettingForOwnersAndRightsHolders()
    {
        EnterAsOwner();
        _room.UsersWithRights = [];
        var persisted = new List<object?>();
        var commands = DisableDiagonalCommands(persisted);
        _client.GetHabbo().Access = UserAccess.Create([], [new(PermissionKeys.CommandDisablediagonal, false)]);
        Assert.False(_room.DiagonalSqueeze);

        _client.GetHabbo().Username = "visitor";
        Assert.True(await commands.Parse(_client, ":disablediagonal"));
        Assert.False(_room.DiagonalSqueeze);
        Assert.Equal("Oops, you need rights in this room to run this command!", LastWhisper());

        _client.GetHabbo().Username = "owner";
        Assert.True(await commands.Parse(_client, ":disablediagonal"));
        Assert.True(_room.DiagonalSqueeze);
        Assert.Equal("You can now walk diagonally between furniture in this room.", LastWhisper());

        // Someone with rights who does not own the room may switch it back.
        _client.GetHabbo().Username = "visitor";
        _room.UsersWithRights.Add(_client.GetHabbo().Id);
        Assert.True(await commands.Parse(_client, ":disablediagonal"));
        Assert.False(_room.DiagonalSqueeze);
        Assert.Equal("You can no longer walk diagonally between furniture in this room.", LastWhisper());

        Assert.Equal([true, false], persisted);
        // Ordinary diagonal walking is never switched off.
        Assert.True(_room.GetGameMap().DiagonalEnabled);
    }

    [Fact]
    public async Task DisableDiagonalLetsTheV2WalkSqueezeBetweenCornerFurnitureUntilSwitchedOff()
    {
        // Two furniture tiles touching at a corner, between (2,1) and (1,2).
        Add(60, 1, 1);
        Add(61, 2, 2);
        var actor = ExecutorActor(2, 1);
        var commands = DisableDiagonalCommands([]);
        _client.GetHabbo().Access = UserAccess.Create([], [new(PermissionKeys.CommandDisablediagonal, false)]);

        Assert.True(await commands.Parse(_client, ":disablediagonal"));
        actor.MoveTo(1, 2);
        ExecutorTick();
        Assert.Contains("/mv 1,2,0.0/", ExecutorUpdate(actor).Status);
        ExecutorTick();
        Assert.Equal((1, 2), (actor.X, actor.Y));

        Assert.True(await commands.Parse(_client, ":disablediagonal"));
        actor.MoveTo(2, 1);
        var steps = new List<(int X, int Y)>();

        for (var tick = 0; tick < 10 && (actor.X, actor.Y) != (2, 1); tick++) {
            ExecutorTick();

            if (steps.Count == 0 || steps[^1] != (actor.X, actor.Y)) {
                steps.Add((actor.X, actor.Y));
            }
        }

        // Without squeezing the walk goes around the furniture: three steps, never between the two corners.
        Assert.Equal((2, 1), (actor.X, actor.Y));
        Assert.Equal(4, steps.Count);
        Assert.Equal((1, 2), steps[0]);
        Assert.All(steps, step => Assert.NotEqual((1, 1), step));
        Assert.All(steps, step => Assert.NotEqual((2, 2), step));
    }

    private CommandManager DisableDiagonalCommands(List<object?> persisted)
    {
        var database = Proxy<IDatabase>((method, _) => method == "Connection"
            ? new NoOpConnection(write: (sql, parameters) =>
            {
                Assert.StartsWith("UPDATE rooms SET diagonal_squeeze", sql);
                var values = parameters.Cast<DbParameter>().ToDictionary(p => p.ParameterName.TrimStart('@'), p => p.Value);
                Assert.Equal(RoomId, values["roomId"]);
                persisted.Add(values["squeeze"]);
            })
            : throw new InvalidOperationException(method));

        return new CommandManager([new DisableDiagonalCommand(database)], TestGameClientManager.Empty, _database, _interactionClock);
    }

    private string LastWhisper()
    {
        var whisper = new FlashIncomingPacket { Buffer = _client.Packets.Last(packet => packet.Header == ServerPacketHeader.WhisperComposer).Body };
        _client.Packets.Clear();
        whisper.ReadInt();

        return whisper.ReadString();
    }
}
