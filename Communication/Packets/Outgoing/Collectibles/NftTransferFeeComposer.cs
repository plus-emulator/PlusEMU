using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>AIR collectibles/class_4225.as:25: the transfer fee in silver.</summary>
public sealed class NftTransferFeeComposer(int fee) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.NftTransferFeeComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteInteger(fee);
}
