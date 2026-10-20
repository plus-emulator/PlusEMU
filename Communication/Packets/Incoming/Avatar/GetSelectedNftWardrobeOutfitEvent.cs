using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Avatar;

/// <summary>The avatar editor asks which collectible the user wears (AIR HabboAvatarEditor.as init and after saving one).</summary>
internal class GetSelectedNftWardrobeOutfitEvent(INftWardrobeService collectibles) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => throw new NotImplementedException();
}
