using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.MysteryBoxes;

public sealed class CancelMysteryBoxWaitComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.CancelMysteryBoxWaitComposer;

    public void Compose(IOutgoingPacket packet) { }
}
