using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Avatar;

/// <summary>The worn collectible's token id, then the fallback figure and gender.</summary>
public class SelectedNftWardrobeOutfitComposer(string tokenId, string fallbackFigure, string fallbackGender) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.SelectedNftWardrobeOutfitComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(tokenId);
        packet.WriteString(fallbackFigure);
        packet.WriteString(fallbackGender);
    }
}
