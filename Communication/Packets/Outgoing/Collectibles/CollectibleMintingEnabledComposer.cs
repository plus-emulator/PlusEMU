using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>AIR collectibles/class_4334.as:25.</summary>
public sealed class CollectibleMintingEnabledComposer(bool enabled) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.CollectibleMintingEnabledComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteBoolean(enabled);
}
