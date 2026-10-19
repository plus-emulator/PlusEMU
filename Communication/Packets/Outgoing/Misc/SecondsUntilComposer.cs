using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Misc;

// AIR WIN63-202610151745-318700621, parser package_205/class_3987.as:24: string time (echoed), int secondsUntil.
public sealed class SecondsUntilComposer(string time, int secondsUntil) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.SecondsUntilComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(time);
        packet.WriteInteger(secondsUntil);
    }
}
