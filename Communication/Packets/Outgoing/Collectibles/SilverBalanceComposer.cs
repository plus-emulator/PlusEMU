using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>AIR collectibles/class_2086.as:25.</summary>
public sealed class SilverBalanceComposer(int balance) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.SilverBalanceComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteInteger(balance);
}
