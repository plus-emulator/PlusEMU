using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.MysteryBoxes;

public sealed class ShowMysteryBoxWaitComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.ShowMysteryBoxWaitComposer;

    public void Compose(IOutgoingPacket packet) { }
}
