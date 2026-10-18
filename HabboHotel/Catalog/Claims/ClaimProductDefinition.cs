namespace Plus.HabboHotel.Catalog.Claims;

/// <summary>What a claim gives. Add a record per reward kind and deliver it in <see cref="ClaimProductStore"/>.</summary>
public abstract record ClaimProductReward;

public sealed record PetClaimReward(int Type, string Race, string Colour, string Name) : ClaimProductReward;

/// <summary>
/// A free product a user claims once. <paramref name="Reward"/> maps the client's choice (null when the client sent none)
/// to the reward, or to null when the choice is not one the claim offers.
/// </summary>
public sealed record ClaimProductDefinition(string ClaimId, bool Enabled, DateTimeOffset? ExpiresAt, Func<int?, ClaimProductReward?> Reward)
{
    public bool Active(DateTimeOffset now) => Enabled && (ExpiresAt is not { } expiresAt || now < expiresAt);
}
