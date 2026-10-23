using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Help;

/// <summary>GuideReportingStatus: [statusCode], plus the pending ticket only when the code is 1.</summary>
public class GuideReportingStatusComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.GuideReportingStatusComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteInteger(0); //0-3, sends 0 on Habbo for this purpose.
}
