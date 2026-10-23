using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

public sealed class CollectibleMintingEnabledComposer(bool enabled) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.CollectibleMintingEnabledComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteBoolean(enabled);
}
