using Plus.HabboHotel.Catalog.Claims;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Catalog;

// AIR WIN63-202610151745-318700621, package_143/class_3538.as:12: string claimId.
public sealed class HasClaimedProductEvent(IClaimProductService claims) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        claims.SendHasClaimed(session, packet.ReadString());

        return Task.CompletedTask;
    }
}
