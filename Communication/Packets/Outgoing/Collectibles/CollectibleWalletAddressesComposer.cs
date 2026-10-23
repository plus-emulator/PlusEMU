using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>The Collector Wallet, then the linked wallets. This hotel has none, so the count is 0.</summary>
public sealed class CollectibleWalletAddressesComposer(string collectorWallet) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.CollectibleWalletAddressesComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(collectorWallet);
        packet.WriteInteger(0);
    }
}
