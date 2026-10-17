using System.Collections.Immutable;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.HabboHotel.Catalog.BuildersClub;
using Plus.HabboHotel.Catalog.Pets;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Catalog;

public sealed record PetPaletteEntry(int Breed, int Palette);
public sealed record PetPaletteSnapshot(string Type, int PetId, ImmutableArray<PetPaletteEntry> Races)
{
    public static PetPaletteSnapshot Capture(string type, int petId, IEnumerable<PetRace> races) =>
        new(type, petId, races.Select(race => new PetPaletteEntry(race.PrimaryColour, race.SecondaryColour)).ToImmutableArray());
}
public sealed record PromotableRoomSnapshot(uint Id, string Name);
public readonly record struct CatalogPageRequest(int PageId, int OfferId, string Mode);

public interface ICatalogBrowsingService
{
    void ShowPetPalettes(GameClient session, string type);
    void ShowPromotableRooms(GameClient session);
    void ShowPage(GameClient session, CatalogPageRequest request);
    void ShowIndex(GameClient session, string mode);
    void ShowMode(GameClient session, string mode);
    void ShowOffer(GameClient session, int offerId);
}

public sealed class CatalogBrowsingService(IItemDataManager items, IPetRaceManager races, IRoomDataLoader rooms,
    TimeProvider clock, ICatalogManager catalog, ICatalogSnapshotService snapshots, IBuildersClubService? buildersClub = null) : ICatalogBrowsingService
{
    public void ShowPetPalettes(GameClient session, string type)
    {
        var item = items.GetItemByName(type);

        if (item == null) {
            return;
        }

        session.Send(new SellablePetBreedsComposer(PetPaletteSnapshot.Capture(type, item.BehaviourData,
            races.GetRacesForRaceId(item.BehaviourData))));
    }

    public void ShowPromotableRooms(GameClient session) =>
        session.Send(new PromotableRoomsComposer(CapturePromotableRooms(session.GetHabbo().Id)));

    public void ShowPage(GameClient session, CatalogPageRequest request)
    {
        // A page opens only in its own catalogue: the client shows it in the window of the mode it carries.
        if (!catalog.TryGetPage(request.PageId, out var page) || page.CatalogMode != CatalogModes.FromClient(request.Mode) || !page.CanOpen(session.GetHabbo())) {
            return;
        }

        session.Send(new CatalogPageComposer(snapshots.CapturePage(page,
            page.Offers.TryGetValue(request.OfferId, out var preselected) && preselected.Enabled ? request.OfferId : -1)));
    }

    public void ShowIndex(GameClient session, string mode)
    {
        ShowMode(session, mode);
        session.Send(new CatalogItemDiscountComposer());
    }

    // The index of the requested catalogue. Observed on hhus: the BUILDERS_CLUB index is followed by the Builders Club furni count.
    public void ShowMode(GameClient session, string mode)
    {
        mode = CatalogModes.FromClient(mode);
        session.Send(new CatalogIndexComposer(snapshots.CaptureIndex(session.GetHabbo(), catalog.Pages, mode)));

        if (mode == CatalogModes.BuildersClub) {
            buildersClub?.SendFurniCount(session);
        }
    }

    public void ShowOffer(GameClient session, int offerId)
    {
        if (catalog.TryGetOffer(offerId, session.GetHabbo(), out _, out var item) && item.Enabled) {
            session.Send(new CatalogOfferComposer(snapshots.CaptureOffer(item)));
        }
    }

    internal ImmutableArray<PromotableRoomSnapshot> CapturePromotableRooms(int ownerId)
    {
        var data = rooms.GetRoomsDataByOwnerSortByName(ownerId);
        var now = clock.GetUtcNow();

        return data.Where(room => room.Promotion == null || room.Promotion.HasExpiredAt(now))
            .Select(room => new PromotableRoomSnapshot(room.Id, room.Name)).ToImmutableArray();
    }
}
