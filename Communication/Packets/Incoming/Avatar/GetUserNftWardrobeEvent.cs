using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Avatar;

/// <summary>The avatar editor's Avatar Collectibles tab asks for the user's collectible avatars.</summary>
internal class GetUserNftWardrobeEvent(INftWardrobeService collectibles) : IPacketEvent
{
    public async Task Parse(GameClient session, IIncomingPacket packet) => session.Send(new UserNftWardrobeComposer(await collectibles.List(session)));
}
