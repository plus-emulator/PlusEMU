using System.Collections.Immutable;
using Plus.Communication.Packets.Outgoing.Quests;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>A monthly collector reward and how often the wallet claimed it.</summary>
public sealed record NftRewardClaim(string ClaimId, string ProductCode, string SetId, string SetName, int ClaimLimit,
    DateTimeOffset ValidFrom, DateTimeOffset ValidTo, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Claimed)
{
    public bool Open => Claimed < ClaimLimit;
}

/// <summary>
/// AIR collectibles/class_3967.as:27-31, class_4380.as:33-47 and class_4407.as:13-17: per claim its id, status (0 = claimable), claimed
/// amount, limit, valid from, valid to, created and updated in ms, collection, product code, wallet, then the product with its set id and
/// set name.
/// </summary>
public sealed class NftClaimsComposer(string wallet, ImmutableArray<(NftRewardClaim Claim, CollectibleProduct Product)> claims) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.NftClaimsComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(claims.Length);

        foreach (var (claim, product) in claims) {
            packet.WriteString(claim.ClaimId);
            packet.WriteInteger(claim.Open ? 0 : 1);
            packet.WriteInteger((int)claim.Claimed);
            packet.WriteInteger(claim.ClaimLimit);
            WriteTime(packet, claim.ValidFrom);
            WriteTime(packet, claim.ValidTo);
            WriteTime(packet, claim.CreatedAt);
            WriteTime(packet, claim.UpdatedAt);
            packet.WriteString(claim.SetId);
            packet.WriteString(claim.ProductCode);
            packet.WriteString(wallet);
            NftProductWriter.Write(packet, product);
            packet.WriteString(claim.SetId);
            packet.WriteString(claim.SetName);
        }
    }

    private static void WriteTime(IOutgoingPacket packet, DateTimeOffset time) => DailyTaskWireWriter.WriteLong(packet, time.ToUnixTimeMilliseconds());
}
