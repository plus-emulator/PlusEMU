using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

/// <summary>[furni count]: the Builders Club furni the user has placed. Sent after the BUILDERS_CLUB index and after every Builders Club furni placed or removed.</summary>
public sealed class BuildersClubFurniCountComposer(int count) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.BuildersClubFurniCountComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteInteger(count);
}
