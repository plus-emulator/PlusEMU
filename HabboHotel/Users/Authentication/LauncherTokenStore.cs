using System.Security.Cryptography;
using System.Text;
using Dapper;
using Plus.Database;

namespace Plus.HabboHotel.Users.Authentication;

/// <summary>
/// Per-user tokens the AIR launcher sends as a Bearer token to get a game ticket. Only the SHA-256 hex digest of a
/// token is stored (user_launcher_tokens). A token stops working when it expires, is revoked, or the user's
/// credential_generation moves on (a password change signs every credential out).
/// </summary>
public interface ILauncherTokenStore
{
    /// <summary>A new token for the user, or null when the user does not exist.</summary>
    Task<string?> Issue(int userId, TimeSpan lifetime);

    /// <summary>The user a live token belongs to.</summary>
    Task<int?> Resolve(string token);

    Task Revoke(string token);
}

public sealed class LauncherTokenStore(IDatabase database, TimeProvider time) : ILauncherTokenStore
{
    public async Task<string?> Issue(int userId, TimeSpan lifetime)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = time.GetUtcNow().UtcDateTime;
        using var connection = database.Connection();
        var inserted = await connection.ExecuteAsync(
            "INSERT INTO `user_launcher_tokens` (`user_id`, `token_hash`, `credential_generation`, `created_at`, `expires_at`) " +
            "SELECT `id`, @hash, `credential_generation`, @now, @expiresAt FROM `users` WHERE `id` = @userId",
            new { userId, hash = Hash(token), now, expiresAt = now.Add(lifetime) });

        return inserted == 1 ? token : null;
    }

    public async Task<int?> Resolve(string token)
    {
        if (string.IsNullOrEmpty(token)) {
            return null;
        }

        var now = time.GetUtcNow().UtcDateTime;
        using var connection = database.Connection();
        var userId = await connection.QueryFirstOrDefaultAsync<int?>(
            "SELECT t.`user_id` FROM `user_launcher_tokens` t JOIN `users` u ON u.`id` = t.`user_id` " +
            "WHERE t.`token_hash` = @hash AND t.`revoked_at` IS NULL AND t.`expires_at` > @now AND u.`credential_generation` = t.`credential_generation` LIMIT 1",
            new { hash = Hash(token), now });

        if (userId != null) {
            await connection.ExecuteAsync("UPDATE `user_launcher_tokens` SET `last_used_at` = @now WHERE `token_hash` = @hash", new { hash = Hash(token), now });
        }

        return userId;
    }

    public async Task Revoke(string token)
    {
        using var connection = database.Connection();
        await connection.ExecuteAsync("UPDATE `user_launcher_tokens` SET `revoked_at` = @now WHERE `token_hash` = @hash AND `revoked_at` IS NULL",
            new { hash = Hash(token), now = time.GetUtcNow().UtcDateTime });
    }

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
