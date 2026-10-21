using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>AIR collectibles/class_1875.as:25.</summary>
public sealed class EmeraldBalanceComposer(int balance) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.EmeraldBalanceComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteInteger(balance);
}
