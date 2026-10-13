using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

public sealed class WiredValidationErrorComposer(string error) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredValidationErrorComposer;

    // The official packet carries a localization key and then a parameter list. None of the observed errors has parameters.
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(error);
        packet.WriteInteger(0);
    }
}
