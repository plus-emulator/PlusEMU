using Plus.Communication.Packets.Outgoing.Sound;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Sound;

internal class GetSoundSettingsEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        session.Send(SoundSettingsComposer.For(session.GetHabbo()));

        return Task.CompletedTask;
    }
}
