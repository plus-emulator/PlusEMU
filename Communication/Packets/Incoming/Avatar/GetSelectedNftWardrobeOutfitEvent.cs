using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Avatar;

/// <summary>The avatar editor asks which collectible the user wears (AIR HabboAvatarEditor.as init and after saving one).</summary>
internal class GetSelectedNftWardrobeOutfitEvent(INftWardrobeService collectibles) : IPacketEvent
{
    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (await collectibles.Worn(session.GetHabbo().Id) is { } worn) {
            session.Send(new SelectedNftWardrobeOutfitComposer(worn.TokenId, worn.FallbackFigure, worn.FallbackGender));
        }
    }
}
