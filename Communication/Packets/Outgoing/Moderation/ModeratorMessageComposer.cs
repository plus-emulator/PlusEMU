using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Moderation;

public class ModeratorMessageComposer(string message, string url) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.ModeratorMessageComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(message);
        packet.WriteString(url);
    }
}
