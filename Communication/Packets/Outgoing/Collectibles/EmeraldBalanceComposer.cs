using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

public sealed class EmeraldBalanceComposer(int balance) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.EmeraldBalanceComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteInteger(balance);
}
