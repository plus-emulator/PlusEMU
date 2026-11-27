using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.MysteryBoxes;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni.MysteryBoxes;

internal sealed class MysteryBoxWaitingCanceledEvent(IMysteryBoxService mysteryBoxes) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        mysteryBoxes.CancelWait(room, session, packet.ReadInt());

        return Task.CompletedTask;
    }
}
