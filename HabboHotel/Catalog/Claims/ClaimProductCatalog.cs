using Plus.Core.Settings;
using Plus.Utilities;

namespace Plus.HabboHotel.Catalog.Claims;

public interface IClaimProductCatalog
{
    ClaimProductDefinition? Find(string claimId);
}

/// <summary>
/// The claims the hotel knows. Each claim reads two optional server settings:
/// <c>claim_product.&lt;claim id&gt;.enabled</c> ("0" turns it off) and <c>claim_product.&lt;claim id&gt;.expires_at</c>
/// ("yyyy-MM-dd HH:mm" in the server's time zone; claims from then on are not active).
/// </summary>
public sealed class ClaimProductCatalog(ISettingsManager settings, TimeProvider clock) : IClaimProductCatalog
{
    public const string CursedDragonAdoption = "cursed_dragon_adoption";

    // pet.configuration index 37 is curseddragon; the window previews palette 0 as the blue package and palette 1 as the
    // purple one with colour ffffff.
    public const int CursedDragonPetType = 37;
    public const string CursedDragonColour = "ffffff";

    // Habbo's name for the adopted dragon is not known; claim_product.cursed_dragon_adoption.pet_name overrides it.
    public const string DefaultCursedDragonName = "Dragon";

    public ClaimProductDefinition? Find(string claimId) => claimId switch
    {
        CursedDragonAdoption => Definition(claimId, CursedDragon),
        _ => null
    };

    private ClaimProductReward? CursedDragon(int? choice) => choice is 0 or 1
        ? new PetClaimReward(CursedDragonPetType, choice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), CursedDragonColour,
            PetName(CursedDragonAdoption, DefaultCursedDragonName))
        : null;

    private ClaimProductDefinition Definition(string claimId, Func<int?, ClaimProductReward?> reward)
    {
        var enabled = settings.GetOptionalValue($"claim_product.{claimId}.enabled")?.Trim() != "0";
        var expiry = settings.GetOptionalValue($"claim_product.{claimId}.expires_at");
        DateTimeOffset? expiresAt = string.IsNullOrWhiteSpace(expiry) ? null
            : ServerTimeText.ParseLocal(expiry, clock.LocalTimeZone) ?? DateTimeOffset.MinValue; // An unreadable expiry closes the claim.

        return new(claimId, enabled, expiresAt, reward);
    }

    private string PetName(string claimId, string fallback)
    {
        var name = settings.GetOptionalValue($"claim_product.{claimId}.pet_name")?.Trim();

        return name != null && Catalog.Utilities.PetUtility.CheckPetName(name) ? name : fallback;
    }
}
