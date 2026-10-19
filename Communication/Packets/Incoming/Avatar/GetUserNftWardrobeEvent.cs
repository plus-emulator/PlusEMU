using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Avatar;

/// <summary>
///     The avatar editor's Avatar Collectibles tab asks for the user's collectible avatars when the editor opens
///     (AIR nft/NftAvatarsModel.as requestNftAvatars). The hotel has no collectibles, so the list is always empty.
/// </summary>
internal class GetUserNftWardrobeEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        session.Send(new UserNftWardrobeComposer([]));

        return Task.CompletedTask;
    }
}
