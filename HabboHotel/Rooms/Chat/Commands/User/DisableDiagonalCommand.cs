using Dapper;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// Toggles and saves whether avatars in the room may walk diagonally between two furniture (or other
/// blocked) tiles that touch at a corner. Ordinary diagonal walking is never switched off.
/// </summary>
internal class DisableDiagonalCommand : IChatCommand
{
    private readonly IDatabase _database;

    public string Key => "disablediagonal";

    public string Parameters => "";

    public string Description => "Allow or block walking diagonally between furniture in your room.";

    public DisableDiagonalCommand(IDatabase database)
    {
        _database = database;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (!room.CheckRights(session, false, true)) {
            session.SendWhisper("Oops, you need rights in this room to run this command!");

            return;
        }

        room.DiagonalSqueeze = !room.DiagonalSqueeze;

        using var connection = _database.Connection();
        connection.Execute("UPDATE rooms SET diagonal_squeeze=@squeeze WHERE id=@roomId LIMIT 1", new { squeeze = room.DiagonalSqueeze, roomId = room.Id });

        session.SendWhisper(room.DiagonalSqueeze
            ? "You can now walk diagonally between furniture in this room."
            : "You can no longer walk diagonally between furniture in this room.");
    }
}
