using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.Items.FootballGate;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// Walking through the gate puts on its look for the user's gender; walking through again gives the user's own look back.
/// Using the gate only opens the client's editor (FurnitureClothingChangeLogic.as:68-97), so a trigger changes nothing.
/// </summary>
internal sealed class InteractorFootballGate(Item gate) : IFurniInteractor
{
    public void OnWalkOn(RoomUser user)
    {
        if (user.IsBot || user.GetClient() is not { } client || client.GetHabbo() is not { } habbo || gate.GetRoom() is not { } room) {
            return;
        }

        if (user.FootballGateOutfit is { } worn && worn.BaseLook == habbo.Look) {
            user.FootballGateOutfit = null;
        }
        else {
            var gateLook = FootballGateLooks.LookFor(gate.LegacyDataString, habbo.Gender);

            if (gateLook.Length == 0) {
                return;
            }

            user.FootballGateOutfit = new(habbo.Look, FootballGateLooks.Merge(habbo.Look, gateLook));
        }

        client.Send(new UserChangeComposer(AvatarChangeSnapshot.Capture(user, true)));
        room.SendPacket(new UserChangeComposer(AvatarChangeSnapshot.Capture(user, false)));
    }
}
