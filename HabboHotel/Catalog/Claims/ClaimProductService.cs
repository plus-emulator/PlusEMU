using Microsoft.Extensions.Logging;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Communication.Packets.Outgoing.Inventory.Pets;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;

namespace Plus.HabboHotel.Catalog.Claims;

public interface IClaimProductService
{
    void SendHasClaimed(GameClient session, string claimId);
    void Claim(GameClient session, string claimId, int? choice);
}

/// <summary>
/// HasClaimedProduct and ClaimProduct (AIR WIN63-202610151745-318700621, package_143/class_3538.as:12 and
/// package_143/class_2610.as:12). Every decision is made here; the composers only write what they are given.
/// </summary>
public sealed class ClaimProductService(IClaimProductCatalog catalog, IClaimProductStore store, IAccountSessionGate sessions,
    TimeProvider clock, ILogger<ClaimProductService> logger) : IClaimProductService
{
    public const int MaxClaimIdLength = 64;

    public void SendHasClaimed(GameClient session, string claimId)
    {
        if (session.GetHabbo() is not { } habbo) {
            return;
        }

        session.Send(new HasClaimedProductResponseComposer(claimId, HasClaimed(habbo.Id, claimId)));
    }

    /// <summary>
    /// Only a stored claim answers true. An unknown or closed claim answers false: the window then offers the claim and
    /// ClaimProduct reports it unavailable (2 or 3), whereas true would show it as already claimed.
    /// </summary>
    internal bool HasClaimed(int userId, string claimId)
    {
        if (claimId.Length is 0 or > MaxClaimIdLength) {
            return false;
        }

        try {
            return store.HasClaimed(userId, claimId);
        }
        catch (Exception exception) {
            logger.LogError(exception, "Reading claim {ClaimId} for user {UserId} failed", claimId, userId);

            return false;
        }
    }

    public void Claim(GameClient session, string claimId, int? choice)
    {
        if (session.GetHabbo() is not { } habbo) {
            return;
        }

        ClaimProductStoreResult outcome;

        using (sessions.Enter(habbo.Id)) {
            if (habbo.AccessClosed || !ReferenceEquals(session.GetHabbo(), habbo)) {
                return;
            }

            outcome = Claim(habbo, claimId, choice);
        }

        if (habbo.AccessClosed || !ReferenceEquals(session.GetHabbo(), habbo)) {
            return;
        }

        if (outcome.Grant?.Pet is { } pet && habbo.Inventory is { } inventory) {
            // The same packets as a catalog pet purchase, so the pet inventory updates live.
            session.Send(new FurniListNotificationComposer((uint)pet.PetId, 3));
            session.Send(new PetInventoryComposer(PetAppearanceSnapshots.Inventory(inventory.Pets.Pets.Values.ToList())));
        }

        session.Send(new ClaimProductResultComposer(claimId, (int)outcome.Result));
    }

    /// <summary>Decides and stores one claim. A granted pet is already in the loaded inventory when this returns.</summary>
    internal ClaimProductStoreResult Claim(Habbo habbo, string claimId, int? choice)
    {
        if (claimId.Length is 0 or > MaxClaimIdLength || catalog.Find(claimId) is not { } definition) {
            return new(ClaimProductResult.NotFound);
        }

        var now = clock.GetUtcNow();

        // A stored claim wins over a closed window, so the window keeps showing it as claimed.
        if (HasClaimed(habbo.Id, claimId)) {
            return new(ClaimProductResult.AlreadyClaimed);
        }

        if (!definition.Active(now)) {
            return new(ClaimProductResult.NotActive);
        }

        // The reward lands in the loaded inventory, so nothing is claimed without one.
        if (definition.Reward(choice) is not { } reward || habbo.Inventory is not { } inventory) {
            return new(ClaimProductResult.Failed);
        }

        var outcome = store.Claim(new(habbo.Id, habbo.Username, claimId, choice, reward, now));

        if (outcome.Grant?.Pet is { } pet) {
            pet.RoomId = 0;
            pet.PlacedInRoom = false;
            inventory.Pets.AddPet(pet);
        }

        return outcome;
    }
}
