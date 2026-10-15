using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;

/// <summary>
/// class_3027 [box id] (AIR WIN63-202610131318-860371703, UserDefinedRoomEventsCtrl.applySnapshot L1268-1271): the editor's
/// "Apply Furni to set conditions" button. Puts the furni of the box's saved snapshot back, with wired write permission.
/// </summary>
public sealed class ApplySnapshotEvent(IWiredRequestGateService gates) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        try {
            var boxId = packet.ReadUInt();

            if (!packet.HasDataRemaining() && gates.TryPass(session, WiredRequestKind.ApplySnapshot)) {
                room.GetWired().ApplySnapshot(session, boxId);
            }
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException) {
        }

        return Task.CompletedTask;
    }
}
