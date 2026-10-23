using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

/// <summary>
/// [category, id, confirmed]. A permanent or shared variable box is
/// only picked up once confirmed: the first pickup is answered with ObjectRemoveConfirm and leaves the box.
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

        // Picking up a Text Connector republishes the leaderboards on its tile.
        if (!stored && room?.GetRoomItemHandler().GetItem(itemId) is { } connector && room.GetWired().IsTextConnector(connector)) {
            if (pickup.TryPickUp(session, itemId)) {
                room.GetWired().TextConnectorPickedUp(connector);
            }

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
