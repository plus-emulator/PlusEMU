using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>AIR collectibles/class_4301.as:25: 0 claimed, any other value is an error id.</summary>
public sealed class NftClaimResultComposer(short result) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.NftClaimResultComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteShort(result);
}
