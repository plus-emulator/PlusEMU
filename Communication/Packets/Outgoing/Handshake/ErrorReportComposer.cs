using System.Globalization;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Handshake;

public sealed class ErrorReportComposer(int messageId, int errorCode, DateTimeOffset time) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.ErrorReportComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(messageId);
        packet.WriteInteger(errorCode);
        packet.WriteString(time.UtcDateTime.ToString("M/d/yy, h:mm tt", CultureInfo.InvariantCulture));
    }
}
