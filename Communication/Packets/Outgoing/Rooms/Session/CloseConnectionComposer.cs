using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Session;

/// <param name="reason">The short a Wired kick sends (2); other removals send an empty body.</param>
public class CloseConnectionComposer(short? reason = null) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.CloseConnectionComposer;

    public void Compose(IOutgoingPacket packet)
    {
        if (reason is { } value) {
            packet.WriteShort(value);
        }
    }
}
