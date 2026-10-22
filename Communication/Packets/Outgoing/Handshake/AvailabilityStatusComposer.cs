using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Handshake;

public class AvailabilityStatusComposer(bool onShutDown = false) : IServerPacket
{
    private readonly bool _onShutDown = onShutDown;

    public uint MessageId => ServerPacketHeader.AvailabilityStatusComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(true);
        packet.WriteBoolean(_onShutDown);
        packet.WriteBoolean(true);
    }
}
