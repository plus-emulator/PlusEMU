using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>AIR collectibles/class_4244.as:28-30: collection id, wallet and whether the bonus was given.</summary>
public sealed class NftCollectionBonusClaimResultComposer(string collectionId, string wallet, bool success) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.NftCollectionBonusClaimResultComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(collectionId);
        packet.WriteString(wallet);
        packet.WriteBoolean(success);
    }
}
