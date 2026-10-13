using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni;

// The client reads these four room-wide switches in this order; the later three are optional on its side.
public sealed class ConfigurationItemStatesComposer(bool invisibleFurni) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.ConfigurationItemStatesComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBool(false); // hand item control blocked
        packet.WriteBool(false); // chooser disabled
        packet.WriteBool(false); // free furni movements
        packet.WriteBool(invisibleFurni);
    }
}
