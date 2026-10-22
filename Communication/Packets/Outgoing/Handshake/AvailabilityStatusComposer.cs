using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Handshake;

public class AvailabilityStatusComposer(bool onShutDown = false) : IServerPacket
{
    private readonly bool _onShutDown = onShutDown;

    public uint MessageId => ServerPacketHeader.AvailabilityStatusComposer;

    public void Compose(IOutgoingPacket packet)
    {
        // TODO @80O: Pass variables via constructor.
        packet.WriteBoolean(true);
        packet.WriteBoolean(false);
        packet.WriteBoolean(true);
    }
}
