using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>AIR collectibles/class_2238.as:28: 0 bought, 1 refused.</summary>
public sealed class NftStorePurchaseResultComposer(short result) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.NftStorePurchaseResultComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteShort(result);
}
