using System.Collections.Immutable;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>AIR collectibles/class_4209.as:29-39: the Collector Wallet, then the count and addresses of the linked wallets.</summary>
public sealed class CollectibleWalletAddressesComposer(string collectorWallet, ImmutableArray<string> linkedWallets) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.CollectibleWalletAddressesComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(collectorWallet);
        packet.WriteInteger(linkedWallets.Length);

        foreach (var wallet in linkedWallets) {
            packet.WriteString(wallet);
        }
    }
}
