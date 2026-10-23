using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Avatar;

/// <summary>The avatar editor saves a collectible as the user's look: the wardrobe item id.</summary>
internal class SaveUserNftWardrobeEvent(INftWardrobeService collectibles) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => collectibles.Wear(session, packet.ReadString());
}
