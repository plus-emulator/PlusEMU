using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>1 minted, 0 failed.</summary>
public sealed class MintCollectibleResultComposer(short result) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.MintCollectibleResultComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteShort(result);
}
