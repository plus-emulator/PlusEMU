namespace Plus.HabboHotel.Catalog.Claims;

/// <summary>
/// The result codes of ClaimProductResult, named after Habbo's texts <c>claim_product.result.N</c>.
/// The cursed dragon window treats 0 and 1 as claimed, 2 and 3 as unavailable and anything else as retryable.
/// </summary>
public enum ClaimProductResult
{
    Claimed = 0,
    AlreadyClaimed = 1,
    NotFound = 2,
    NotActive = 3,
    Failed = 4
}
