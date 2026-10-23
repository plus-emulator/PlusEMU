using System.Diagnostics.CodeAnalysis;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Catalog;

// Remembers which pages show each offer; an offer can be on several pages. Normal and Builders Club pages are indexed
// apart: an offer found through the normal catalogue is bought, one found through the Builders Club catalogue is placed.
public class CatalogOfferIndex
{
    // Offers created without an official Habbo offer id are numbered from here.
    public const int CustomOfferIdBase = 1_000_000_000;

    private readonly Dictionary<int, List<CatalogPage>> _pagesByOffer = new();
    private readonly Dictionary<int, List<CatalogPage>> _buildersClubPagesByOffer = new();

    // pages must be in catalog order.
    public void Build(IEnumerable<CatalogPage> pages)
    {
        _pagesByOffer.Clear();
        _buildersClubPagesByOffer.Clear();

        foreach (var page in pages) {
            var index = page.IsBuildersClub ? _buildersClubPagesByOffer : _pagesByOffer;

            foreach (var offerId in page.Offers.Keys) {
                if (!index.TryGetValue(offerId, out var offerPages)) {
                    index[offerId] = offerPages = new();
                }

                offerPages.Add(page);
            }
        }
    }

    // First normal page the user can open that sells this offer.
    public bool TryGet(int offerId, Habbo habbo, [NotNullWhen(true)] out CatalogPage? page, [NotNullWhen(true)] out CatalogOffer? offer)
    {
        foreach (var candidate in _pagesByOffer.GetValueOrDefault(offerId) ?? []) {
            if (candidate.CanOpen(habbo) && candidate.Offers.TryGetValue(offerId, out offer!) && offer.CanPurchase(habbo)) {
                page = candidate;

                return true;
            }
        }

        page = null!;
        offer = null!;

        return false;
    }

    // First Builders Club page the user can open that shows this offer. The info stand's Builders Club copy sends the
    // furnidata bcofferid with page -1.
    public bool TryGetBuildersClub(int offerId, Habbo habbo, [NotNullWhen(true)] out CatalogPage? page, [NotNullWhen(true)] out CatalogOffer? offer)
    {
        foreach (var candidate in _buildersClubPagesByOffer.GetValueOrDefault(offerId) ?? []) {
            if (candidate.CanOpen(habbo) && candidate.Offers.TryGetValue(offerId, out offer!) && offer.Enabled) {
                page = candidate;

                return true;
            }
        }

        page = null!;
        offer = null!;

        return false;
    }
}
