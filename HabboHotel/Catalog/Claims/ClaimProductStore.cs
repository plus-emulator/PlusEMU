using Dapper;
using Microsoft.Extensions.Logging;
using Plus.Database;
using Plus.HabboHotel.Catalog.Utilities;
using Plus.HabboHotel.Rooms.AI;

namespace Plus.HabboHotel.Catalog.Claims;

public sealed record ClaimProductRequest(int UserId, string OwnerName, string ClaimId, int? Choice, ClaimProductReward Reward, DateTimeOffset ClaimedAt);

/// <summary>What was delivered, for the session to publish. <see cref="Pet"/> is set for a pet reward.</summary>
public sealed record ClaimProductGrant(Pet? Pet);

public sealed record ClaimProductStoreResult(ClaimProductResult Result, ClaimProductGrant? Grant = null);

public interface IClaimProductStore
{
    bool HasClaimed(int userId, string claimId);
    ClaimProductStoreResult Claim(ClaimProductRequest request);
}

public sealed class ClaimProductStore(IDatabase database, ILogger<ClaimProductStore> logger) : IClaimProductStore
{
    public bool HasClaimed(int userId, string claimId)
    {
        using var connection = database.Connection();

        return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_claimed_products WHERE user_id = @userId AND claim_id = @claimId",
            new { userId, claimId }) > 0;
    }

    public ClaimProductStoreResult Claim(ClaimProductRequest request)
    {
        try {
            using var connection = database.Connection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            // The claim row goes in first: the primary key makes a second claim (or a double click racing the first) insert
            // nothing. The reward commits with it, so a failed delivery leaves the claim open.
            if (connection.Execute("INSERT IGNORE INTO user_claimed_products (user_id, claim_id, choice, claimed_at) VALUES (@UserId, @ClaimId, @Choice, @ClaimedAt)",
                    new { request.UserId, request.ClaimId, Choice = request.Choice ?? -1, ClaimedAt = request.ClaimedAt.UtcDateTime }, transaction) != 1) {
                return new(ClaimProductResult.AlreadyClaimed);
            }

            ClaimProductGrant grant;

            switch (request.Reward) {
                case PetClaimReward pet: {
                        var created = PetUtility.CreatePet(connection, transaction, request.ClaimedAt, request.OwnerName, request.UserId,
                            pet.Name, pet.Type, pet.Race, pet.Colour);

                        if (created == null) {
                            return new(ClaimProductResult.Failed);
                        }

                        grant = new(created);
                        break;
                    }
                default:
                    return new(ClaimProductResult.Failed);
            }

            transaction.Commit();

            return new(ClaimProductResult.Claimed, grant);
        }
        catch (Exception exception) {
            logger.LogError(exception, "Claiming {ClaimId} for user {UserId} failed", request.ClaimId, request.UserId);

            return new(ClaimProductResult.Failed);
        }
    }
}
