using System.Drawing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;

namespace Plus.HabboHotel.Rooms.PathFinding;

internal sealed class TransportLandingService(Room room)
{
    internal void Trigger(RoomUser actor, Point destination, Item? roller = null)
    {
        if (TriggerWalkOn(actor, destination)) {
            TriggerWalkOff(actor, roller);
        }
    }

    // Returns false when the actor has no client or a hook moved it away.
    internal bool TriggerWalkOn(RoomUser actor, Point destination)
    {
        var habbo = actor.GetClient()?.GetHabbo();

        if (habbo == null) {
            return false;
        }

        var revision = actor.Movement.LocationRevision;
        var grid = room.GetGameMap().Navigation?.Grid;
        var contact = SurfaceContacts.ContactSlot(grid, destination.X, destination.Y, actor.Movement.CurrentRef, actor.Movement.SupportZ);
        var items = SurfaceContacts.Filter(grid, destination.X, destination.Y, contact,
            room.GetGameMap().GetRoomItemForSquare(destination.X, destination.Y));

        foreach (var item in items) {
            room.GetWired().TriggerEvent(WiredBoxType.TriggerWalkOnFurni, habbo, item);

            if (actor.Movement.LocationRevision != revision || actor.Movement.State != NavState.Active) {
                return false;
            }
        }

        return true;
    }

    internal void TriggerWalkOff(RoomUser actor, Item? roller)
    {
        var habbo = actor.GetClient()?.GetHabbo();

        if (habbo != null && roller != null && ReferenceEquals(room.GetRoomItemHandler().GetItem(roller.Id), roller)) {
            room.GetWired().TriggerEvent(WiredBoxType.TriggerWalkOffFurni, habbo, roller);
        }
    }
}
