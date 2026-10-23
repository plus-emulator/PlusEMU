using Plus.Communication.Packets.Outgoing.Help;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Help;

/// <summary>GetGuideReportingStatus (no fields). Answered with status 0, no pending ticket.</summary>
internal class GetGuideReportingStatusEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        session.Send(new GuideReportingStatusComposer());

        return Task.CompletedTask;
    }
}
