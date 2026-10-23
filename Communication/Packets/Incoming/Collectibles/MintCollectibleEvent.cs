using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>An inventory furni is minted into a collectible: inventory item id, wallet.</summary>
internal sealed class MintCollectibleEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.Mint(session, packet.ReadInt(), packet.ReadString());
}
