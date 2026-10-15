using System.Security.Cryptography;
using System.Text;
using Dapper;
using Plus.Database;
using Plus.HabboHotel.Items.Wired.Configuration;

namespace Plus.HabboHotel.Items.Wired.WebApi;

/// <summary>What a Web API key opens: the add-on's room, whether it may write and whether it may bulk delete.</summary>
public sealed record WiredWebApiGrant(uint ItemId, uint RoomId, bool Write, bool BulkDelete);

/// <summary>
/// Keys of the Variables Web API add-on (VariablesWebApiAddon.as, WIN63-202610131318-860371703). The server makes them (32 random
/// bytes, base64url) when the editor asks; the new key replaces the old one at once. Only SHA-256 hashes are stored here; the
/// editor keeps the keys in its saved text, and a save is only accepted with the keys the server last made for that add-on.
/// </summary>
public interface IWiredWebApiStore
{
    void Issue(uint itemId, uint roomId, bool read, string key);
    (string? ReadHash, string? WriteHash) Hashes(uint itemId);
    void Saved(uint itemId, uint roomId, string readKey, string writeKey, bool bulkDelete);
    WiredWebApiGrant? Find(string key);
}

public sealed class DatabaseWiredWebApiStore(IDatabase database) : IWiredWebApiStore
{
    public static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static string? HashOrNull(string key) => key.Length == 0 ? null : Hash(key);

    public void Issue(uint itemId, uint roomId, bool read, string key)
    {
        using var connection = database.Connection();
        var column = read ? "read_key_hash" : "write_key_hash";
        connection.Execute($"INSERT INTO wired_web_api_keys (item_id, room_id, {column}) VALUES (@ItemId, @RoomId, @Hash) "
            + $"ON DUPLICATE KEY UPDATE room_id = @RoomId, {column} = @Hash", new { ItemId = itemId, RoomId = roomId, Hash = Hash(key) });
    }

    public (string? ReadHash, string? WriteHash) Hashes(uint itemId)
    {
        using var connection = database.Connection();

        return connection.QuerySingleOrDefault<(string?, string?)>("SELECT read_key_hash, write_key_hash FROM wired_web_api_keys WHERE item_id = @ItemId",
            new { ItemId = itemId });
    }

    public void Saved(uint itemId, uint roomId, string readKey, string writeKey, bool bulkDelete)
    {
        using var connection = database.Connection();
        connection.Execute("INSERT INTO wired_web_api_keys (item_id, room_id, read_key_hash, write_key_hash, bulk_delete) "
            + "VALUES (@ItemId, @RoomId, @Read, @Write, @Bulk) ON DUPLICATE KEY UPDATE room_id = @RoomId, read_key_hash = @Read, "
            + "write_key_hash = @Write, bulk_delete = @Bulk",
            new { ItemId = itemId, RoomId = roomId, Read = HashOrNull(readKey), Write = HashOrNull(writeKey), Bulk = bulkDelete && writeKey.Length > 0 });
    }

    public WiredWebApiGrant? Find(string key)
    {
        if (!WiredWebApiKeyFormat.IsKey(key)) {
            return null;
        }

        var hash = Hash(key);
        using var connection = database.Connection();
        var row = connection.QuerySingleOrDefault<(long ItemId, long RoomId, string? Read, string? Write, bool Bulk)>(
            "SELECT item_id, room_id, read_key_hash, write_key_hash, bulk_delete FROM wired_web_api_keys "
            + "WHERE read_key_hash = @Hash OR write_key_hash = @Hash LIMIT 1", new { Hash = hash });

        return row.ItemId == 0 ? null : new((uint)row.ItemId, (uint)row.RoomId, row.Write == hash, row.Bulk && row.Write == hash);
    }
}
