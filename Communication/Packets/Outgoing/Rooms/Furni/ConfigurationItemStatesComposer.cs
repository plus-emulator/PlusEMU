using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni;

// The client reads these four room-wide switches in this order; the later three are optional on its side
// (AIR WIN63-202610131318 package_14/class_3143.as parse).
public sealed class ConfigurationItemStatesComposer(bool chooserDisabled, bool invisibleFurni) : IServerPacket
{
    public bool ChooserDisabled => chooserDisabled;
    public bool InvisibleFurni => invisibleFurni;
    public uint MessageId => ServerPacketHeader.ConfigurationItemStatesComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBool(false); // hand item control blocked
        packet.WriteBool(chooserDisabled);
        packet.WriteBool(false); // free furni movements
        packet.WriteBool(invisibleFurni);
        // Observed on AIR WIN63-202610151745-318700621: room entry carries five bytes; the client reads the first four.
        packet.WriteBool(false);
    }
}
