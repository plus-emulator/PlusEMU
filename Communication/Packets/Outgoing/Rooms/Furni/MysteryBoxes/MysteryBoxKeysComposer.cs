using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.MysteryBoxes;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.MysteryBoxes;

public sealed class MysteryBoxKeysComposer(MysteryBoxColors colors) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.MysteryBoxKeysComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(colors.Box);
        packet.WriteString(colors.Key);
    }
}
