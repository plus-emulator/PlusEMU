using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>Collection id, wallet and whether the bonus was given.</summary>
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
