using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>Every collectible of a linked wallet moves to the Collector Wallet (AIR TransferNftsTab.as:146): wallet.</summary>
internal sealed class TransferNftsEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.Transfer(session, packet.ReadString());
}
