namespace Plus.HabboHotel.Catalog.Claims;

/// <summary>
/// The result codes of ClaimProductResult, named after Habbo's texts <c>claim_product.result.N</c>.
/// The cursed dragon window treats 0 and 1 as claimed, 2 and 3 as unavailable and anything else as retryable
/// (AIR WIN63-202610151745-318700621, catalog/cursed_dragon_adoption/CursedDragonAdoptionController.as:148).
/// </summary>
public enum ClaimProductResult
{
    Claimed = 0,
    AlreadyClaimed = 1,
    NotFound = 2,
    NotActive = 3,
    Failed = 4
}
