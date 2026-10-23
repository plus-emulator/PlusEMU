using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>The Collections tab asks for every set and how much of it a wallet owns: wallet.</summary>
internal sealed class GetNftCollectionsEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.SendCollections(session, packet.ReadString());
}
