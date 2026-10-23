using Plus.HabboHotel.Catalog.Claims;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Catalog;

// String claimId.
public sealed class HasClaimedProductEvent(IClaimProductService claims) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        claims.SendHasClaimed(session, packet.ReadString());

        return Task.CompletedTask;
    }
}
