using Plus.HabboHotel.Catalog.Claims;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Catalog;

// AIR WIN63-202610151745-318700621, package_143/class_2610.as:12: string claimId, then an int choice that is only
// written when it is not -1.
public sealed class ClaimProductEvent(IClaimProductService claims) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var claimId = packet.ReadString();
        int? choice = packet.Buffer.Length >= sizeof(int) ? packet.ReadInt() : null;
        claims.Claim(session, claimId, choice);

        return Task.CompletedTask;
    }
}
