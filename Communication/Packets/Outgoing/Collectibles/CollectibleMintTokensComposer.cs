using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>AIR collectibles/class_4115.as:25: the wallet's mint tokens.</summary>
public sealed class CollectibleMintTokensComposer(int totalTokens) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.CollectibleMintTokensComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteInteger(totalTokens);
}
