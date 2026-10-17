using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Help;

// AIR WIN63-202610151745-318700621 package_58/class_2526.as:24 reads a list of sanctions, each
// (name, hours, probation days) + reason + gradual flag + probation hours left + (next name, hours, probation days).
// No sanction history is tracked yet, so the list is empty and the client shows its "no sanctions" text.
public class SanctionStatusComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.SanctionStatusComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(0);
    }
}
