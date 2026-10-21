using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>AIR collectibles/class_4310.as:25: 0 moved, any other value is an error id.</summary>
public sealed class NftTransferResultComposer(short result) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.NftTransferResultComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteShort(result);
}
