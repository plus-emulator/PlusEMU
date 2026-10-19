using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

/// <summary>
/// [category, id, confirmed] (package_66/class_3322.as:15-41, WIN63-202610131318-860371703). A permanent or shared variable box is
/// only picked up once confirmed: the first pickup is answered with ObjectRemoveConfirm and leaves the box (observed on hhus, r90).
/// </summary>
internal class PickupObjectEvent(IRoomItemPickupService pickup) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var category = packet.ReadInt();
        var itemId = TemporaryItemIds.FromWire(BuildersClubItemIds.FromWire(packet.ReadUInt()), category == 1);
        var confirmed = packet.HasDataRemaining() && packet.ReadBool();
        var room = session.GetHabbo()?.CurrentRoom;
        var stored = room?.GetRoomItemHandler().GetItem(itemId) is { } item && room.GetWired().StoresVariableData(item);

        if (stored && !confirmed) {
            session.Send(new ObjectRemoveConfirmComposer(itemId, false, ObjectRemoveConfirmComposer.VariableTitle, ObjectRemoveConfirmComposer.VariableBody));

            return Task.CompletedTask;
        }

        if (!stored) {
            return pickup.PickUp(session, itemId);
        }

        // The dialog promises that the variable's references and stored data go with it.
        if (pickup.TryPickUp(session, itemId)) {
            room!.GetWired().Variables.DefinitionDeleted(itemId);
        }

        return Task.CompletedTask;
    }
}
