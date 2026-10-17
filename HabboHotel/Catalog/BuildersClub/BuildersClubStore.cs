using System.Data;
using Dapper;
using Plus.Database;

namespace Plus.HabboHotel.Catalog.BuildersClub;

// A user's Builders Club membership: when it ends and the furni bought on top of the furni limit.
// FurniLimit is the highest limit a bought package gave (0 for none).
public sealed record BuildersClubMembership(DateTimeOffset ExpiresAt, int FurniLimit, int FurniAddon);

public interface IBuildersClubStore
{
    // The user's membership; null when the user never had one.
    BuildersClubMembership? Membership(int userId);
    // Adds days to a running membership or starts one now, raising its furni limit to the package's; part of the purchase's transaction.
    void Extend(IDbConnection connection, IDbTransaction transaction, int userId, int days, int furniLimit, DateTimeOffset now);
    // Adds furni to a running membership's limit; false without one. Part of the purchase's transaction.
    bool AddFurni(IDbConnection connection, IDbTransaction transaction, int userId, int furni, DateTimeOffset now);
    // Builders Club furni the user has standing in rooms.
    int FurniCount(int userId);
    // Stores a new Builders Club furni for the user, not yet in a room, and returns its id.
    uint Create(uint definitionId, int userId, string extraData);
    void Delete(uint itemId);
}

public sealed class BuildersClubStore(IDatabase database) : IBuildersClubStore
{
    public BuildersClubMembership? Membership(int userId)
    {
        using var connection = database.Connection();

        var row = connection.QuerySingleOrDefault<MembershipRow>(
            "SELECT expires_at AS ExpiresAt, furni_limit AS FurniLimit, furni_addon AS FurniAddon FROM user_builders_club WHERE user_id = @userId", new { userId });

        return row == null ? null : new(row.ExpiresAt, row.FurniLimit, row.FurniAddon);
    }

    private sealed class MembershipRow
    {
        public DateTimeOffset ExpiresAt { get; set; }
        public int FurniLimit { get; set; }
        public int FurniAddon { get; set; }
    }

    public void Extend(IDbConnection connection, IDbTransaction transaction, int userId, int days, int furniLimit, DateTimeOffset now)
    {
        var current = connection.ExecuteScalar<DateTimeOffset?>("SELECT expires_at FROM user_builders_club WHERE user_id = @userId FOR UPDATE",
            new { userId }, transaction);
        var expiry = (current is { } end && end > now ? end : now).AddDays(days).UtcDateTime;
        connection.Execute("INSERT INTO user_builders_club (user_id, expires_at, furni_limit) VALUES (@userId, @expiry, @furniLimit) " +
            "ON DUPLICATE KEY UPDATE expires_at = @expiry, furni_limit = GREATEST(furni_limit, @furniLimit)", new { userId, expiry, furniLimit }, transaction);
    }

    public bool AddFurni(IDbConnection connection, IDbTransaction transaction, int userId, int furni, DateTimeOffset now) =>
        connection.Execute("UPDATE user_builders_club SET furni_addon = furni_addon + @furni WHERE user_id = @userId AND expires_at > @now",
            new { userId, furni, now = now.UtcDateTime }, transaction) == 1;

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
