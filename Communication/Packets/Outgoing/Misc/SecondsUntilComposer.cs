using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Misc;

// String time (echoed), int secondsUntil.
public sealed class SecondsUntilComposer(string time, int secondsUntil) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.SecondsUntilComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(time);
        packet.WriteInteger(secondsUntil);
    }
}
