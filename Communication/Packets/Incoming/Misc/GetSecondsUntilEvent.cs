using Plus.Communication.Packets.Outgoing.Misc;
using Plus.HabboHotel.GameClients;
using Plus.Utilities;

namespace Plus.Communication.Packets.Incoming.Misc;

// String time ("2026-11-05 23:59"). The client hides a new-feature promo whose notifications.new_feature.expiry time answers 0 or less.
public sealed class GetSecondsUntilEvent(TimeProvider clock) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var time = packet.ReadString();
        session.Send(new SecondsUntilComposer(time, ServerTimeText.SecondsUntil(time, clock.GetUtcNow(), clock.LocalTimeZone)));

        return Task.CompletedTask;
    }
}
