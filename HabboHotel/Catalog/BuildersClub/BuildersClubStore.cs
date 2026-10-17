using Dapper;
using Plus.Database;

namespace Plus.HabboHotel.Catalog.BuildersClub;

public interface IBuildersClubStore
{
    // When the user's Builders Club membership ends; null when the user never had one.
    DateTimeOffset? MembershipExpiry(int userId);
    // Builders Club furni the user has standing in rooms.
    int FurniCount(int userId);
    // Stores a new Builders Club furni for the user, not yet in a room, and returns its id.
    uint Create(uint definitionId, int userId, string extraData);
    void Delete(uint itemId);
}

public sealed class BuildersClubStore(IDatabase database) : IBuildersClubStore
{
    public DateTimeOffset? MembershipExpiry(int userId)
    {
        using var connection = database.Connection();

        return connection.ExecuteScalar<DateTimeOffset?>("SELECT expires_at FROM user_builders_club WHERE user_id = @userId", new { userId });
    }

    public int FurniCount(int userId)
    {
        using var connection = database.Connection();

        return connection.ExecuteScalar<int>("""
            SELECT COUNT(*) FROM items_builders_club AS builders_club
            INNER JOIN items ON items.id = builders_club.item_id
            WHERE builders_club.user_id = @userId AND items.room_id <> 0
            """, new { userId });
    }

    public uint Create(uint definitionId, int userId, string extraData)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        connection.Execute("INSERT INTO items (base_item, user_id, room_id, x, y, z, wall_pos, rot, extra_data, limited_number, limited_stack) " +
            "VALUES (@definitionId, @userId, 0, 0, 0, 0, '', 0, @extraData, 0, 0)", new { definitionId, userId, extraData }, transaction);
        var itemId = connection.QuerySingle<uint>("SELECT LAST_INSERT_ID()", transaction: transaction);
        connection.Execute("INSERT INTO items_builders_club (item_id, user_id) VALUES (@itemId, @userId)", new { itemId, userId }, transaction);
        transaction.Commit();

        return itemId;
    }

    // items_builders_club goes with the item.
    public void Delete(uint itemId)
    {
        using var connection = database.Connection();
        connection.Execute("DELETE FROM items WHERE id = @itemId LIMIT 1", new { itemId });
    }
}
