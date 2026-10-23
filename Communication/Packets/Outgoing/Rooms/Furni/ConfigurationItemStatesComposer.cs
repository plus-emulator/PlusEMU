using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni;

// The client reads these four room-wide switches in this order; the later three are optional on its side
public sealed class ConfigurationItemStatesComposer(bool chooserDisabled, bool invisibleFurni, bool handItemBlocked = false) : IServerPacket
{
    public bool HandItemBlocked => handItemBlocked;
    public bool ChooserDisabled => chooserDisabled;
    public bool InvisibleFurni => invisibleFurni;
    public uint MessageId => ServerPacketHeader.ConfigurationItemStatesComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBool(handItemBlocked);
        packet.WriteBool(chooserDisabled);
        packet.WriteBool(false); // free furni movements
        packet.WriteBool(invisibleFurni);
        // Room entry carries five bytes; the client reads the first four.
        packet.WriteBool(false);
    }
}
