using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Avatar;

/// <summary>The avatar editor's Avatar Collectibles tab asks for the user's collectible avatars (AIR nft/NftAvatarsModel.as requestNftAvatars).</summary>
internal class GetUserNftWardrobeEvent(INftWardrobeService collectibles) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => throw new NotImplementedException();
}
