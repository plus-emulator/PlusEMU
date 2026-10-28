using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Sound;

internal class GetSoundSettingsEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => throw new NotImplementedException();
}
