using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.MysteryBoxes;

/// <summary>The prize shown in the reward dialog: the product type (<c>s</c> or <c>i</c>) and the furni sprite id.</summary>
public sealed class GotMysteryBoxPrizeComposer(string productType, int spriteId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.GotMysteryBoxPrizeComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(productType);
        packet.WriteInteger(spriteId);
    }
}
