using Plus.Communication.Packets.Outgoing.Rooms.AI.Pets;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Pets.Locale;

namespace Plus.HabboHotel.Rooms.AI;

public interface IHorseRidingService
{
    void Ride(Room room, GameClient session, int petId, bool mount);
}

public sealed class HorseRidingService(IPetLocale petLocale) : IHorseRidingService
{
    public void Ride(Room room, GameClient session, int petId, bool mount)
    {
        // The packet's room is the one the user occupies now. A stale reference from before a move is ignored.
        var habbo = session.GetHabbo();

        if (!ReferenceEquals(habbo.CurrentRoom, room)) {
            return;
        }

        var user = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);

        if (user == null) {
            return;
        }

        if (!room.GetRoomUserManager().TryGetPet(petId, out var pet) || pet.PetData == null) {
            return;
        }

        if (!MayRide(session, user, pet)) {
            return;
        }

        room.GetGameMap().Navigation!.Mounts.Ride(user, pet, mount, petLocale);
    }

    private static bool MayRide(GameClient session, RoomUser user, RoomUser pet)
    {
        if (pet.PetData is { } petData && (petData.AnyoneCanRide != 0 || petData.OwnerId == user.UserId)) {
            return true;
        }

        session.SendNotification(
            "You are unable to ride this horse.\nThe owner of the pet has not selected for anyone to ride it.");

        return false;
    }
}
