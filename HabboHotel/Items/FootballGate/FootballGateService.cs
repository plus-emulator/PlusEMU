using Dapper;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Items.FootballGate;

public sealed record FootballGateLookRequest(uint ItemId, string Gender, string Look);

public interface IFootballGateStore
{
    void SetLooks(uint itemId, uint roomId, string data);
}

public sealed class FootballGateStore(IDatabase database) : IFootballGateStore
{
    // An identical value changes no rows, so the item is looked up first instead of trusting the update count.
    public void SetLooks(uint itemId, uint roomId, string data)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (connection.Query<uint>("SELECT id FROM items WHERE id=@itemId AND room_id=@roomId FOR UPDATE", new { itemId, roomId }, transaction).Count() != 1) {
            throw new InvalidOperationException("Football gate is not in the room.");
        }

        connection.Execute("UPDATE items SET extra_data=@data WHERE id=@itemId AND room_id=@roomId LIMIT 1", new { itemId, roomId, data }, transaction);
        transaction.Commit();
    }
}

public interface IFootballGateService
{
    void SaveLook(GameClient session, FootballGateLookRequest request);
}

public sealed class FootballGateService(IFootballGateStore store) : IFootballGateService
{
    /// <summary>
    /// Stores one gender's look and keeps the other. The editor is only offered to the room owner, room controllers and users with
    /// rights.
    /// </summary>
    public void SaveLook(GameClient session, FootballGateLookRequest request)
    {
        var room = session.GetHabbo()?.CurrentRoom;

        if (room == null || !room.CheckRights(session, false, true)) {
            return;
        }

        var item = room.GetRoomItemHandler().GetItem(request.ItemId);

        if (item == null || item.IsTemporary || item.Definition?.InteractionType != InteractionType.FootballGate) {
            return;
        }

        var gender = request.Gender.ToUpperInvariant();
        var look = FootballGateLooks.Sanitize(request.Look);

        if (gender is not (FootballGateLooks.Male or FootballGateLooks.Female) || look.Length == 0) {
            return;
        }

        var (male, female) = FootballGateLooks.Parse(item.LegacyDataString);
        var data = gender == FootballGateLooks.Male ? FootballGateLooks.Serialize(look, female) : FootballGateLooks.Serialize(male, look);

        store.SetLooks(item.Id, room.Id, data);

        item.LegacyDataString = data;
        item.UpdateState(true, true);
    }
}
