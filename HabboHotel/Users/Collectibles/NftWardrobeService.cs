using System.Collections.Immutable;
using System.Data;
using Dapper;
using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Users.Collectibles;

/// <summary>The collectible worn now: its token and the look it replaced.</summary>
public sealed record WornCollectible(string TokenId, string FallbackFigure, string FallbackGender);

/// <summary>The hotel's avatar collectibles: looks staff give to a user, listed in the avatar editor's Avatar Collectibles tab.</summary>
public interface INftWardrobeService
{
    Task<ImmutableArray<NftWardrobeItem>> List(GameClient session);
    Task<WornCollectible?> Worn(int userId);
    Task Wear(GameClient session, string id);
    Task<NftWardrobeItem> Give(Habbo target, string figure, string gender, string contractKey);
}

public sealed class NftWardrobeService(IDatabase database) : INftWardrobeService
{
    public async Task<ImmutableArray<NftWardrobeItem>> List(GameClient session)
    {
        var habbo = session.GetHabbo();
        using var connection = database.Connection();
        var rows = await connection.QueryAsync<CollectibleRow>(
            "SELECT id, figure, gender, contract_key AS ContractKey FROM user_nft_wardrobe WHERE user_id = @userId ORDER BY id",
            new { userId = habbo.Id });
        habbo.NftWardrobeListed = true;

        return [.. rows.Select(row => row.ToItem())];
    }

    public async Task<WornCollectible?> Worn(int userId)
    {
        using var connection = database.Connection();

        return await Worn(connection, null, userId);
    }

    public async Task Wear(GameClient session, string id)
    {
        if (!int.TryParse(id, out var nftId)) {
            return;
        }

        var habbo = session.GetHabbo();
        CollectibleRow? collectible;

        using (var connection = database.Connection()) {
            connection.Open();
            using var transaction = connection.BeginTransaction();
            collectible = await connection.QuerySingleOrDefaultAsync<CollectibleRow>(
                "SELECT id, figure, gender, contract_key AS ContractKey FROM user_nft_wardrobe WHERE id = @nftId AND user_id = @userId",
                new { nftId, userId = habbo.Id }, transaction);

            if (collectible == null) {
                return;
            }

            if (await Worn(connection, transaction, habbo.Id) == null) {
                await connection.ExecuteAsync("""
                    INSERT INTO user_nft_outfits (user_id, nft_id, fallback_figure, fallback_gender)
                    SELECT id, @nftId, COALESCE(look, ''), COALESCE(gender, 'M') FROM users WHERE id = @userId
                    ON DUPLICATE KEY UPDATE nft_id = VALUES(nft_id), fallback_figure = VALUES(fallback_figure), fallback_gender = VALUES(fallback_gender)
                    """, new { nftId, userId = habbo.Id }, transaction);
            }
            else {
                await connection.ExecuteAsync("UPDATE user_nft_outfits SET nft_id = @nftId WHERE user_id = @userId", new { nftId, userId = habbo.Id }, transaction);
            }

            await connection.ExecuteAsync("UPDATE users SET look = @figure, gender = @gender WHERE id = @userId",
                new { collectible.Figure, collectible.Gender, userId = habbo.Id }, transaction);
            transaction.Commit();
        }

        habbo.Look = collectible.Figure;
        habbo.Gender = collectible.Gender.ToLower();
        session.Send(new AvatarAspectUpdateComposer(collectible.Figure, collectible.Gender));

        if (habbo.InRoom && habbo.CurrentRoom.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id) is { } roomUser) {
            session.Send(new UserChangeComposer(AvatarChangeSnapshot.Capture(roomUser, true)));
            habbo.CurrentRoom.SendPacket(new UserChangeComposer(AvatarChangeSnapshot.Capture(roomUser, false)));
        }
    }

    public async Task<NftWardrobeItem> Give(Habbo target, string figure, string gender, string contractKey)
    {
        using var connection = database.Connection();
        var id = await connection.ExecuteScalarAsync<int>(
            "INSERT INTO user_nft_wardrobe (user_id, figure, gender, contract_key) VALUES (@userId, @figure, @gender, @contractKey); SELECT LAST_INSERT_ID()",
            new { userId = target.Id, figure, gender, contractKey });
        var item = new CollectibleRow(id, figure, gender, contractKey).ToItem();

        if (target.NftWardrobeListed) {
            target.Client?.Send(new UserNftWardrobeComposer([item]));
        }

        return item;
    }

    // Worn only while the saved look is still the collectible's figure, so any other look change takes it off.
    private static async Task<WornCollectible?> Worn(IDbConnection connection, IDbTransaction? transaction, int userId) =>
        await connection.QuerySingleOrDefaultAsync<WornCollectible>("""
            SELECT CAST(o.nft_id AS CHAR) AS TokenId, o.fallback_figure AS FallbackFigure, o.fallback_gender AS FallbackGender
            FROM user_nft_outfits o
            JOIN user_nft_wardrobe w ON w.id = o.nft_id
            JOIN users u ON u.id = o.user_id
            WHERE o.user_id = @userId AND u.look = w.figure
            """, new { userId }, transaction);

    private sealed record CollectibleRow(int Id, string Figure, string Gender, string ContractKey)
    {
        public NftWardrobeItem ToItem() => new(Id.ToString(), Figure, Gender, Id.ToString(), ContractKey);
    }
}
