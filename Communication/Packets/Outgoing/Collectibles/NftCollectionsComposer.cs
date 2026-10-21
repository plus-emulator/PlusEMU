using Plus.Communication.Packets.Outgoing.Quests;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>
/// AIR collectibles/class_3936.as:27-31 and class_4381.as:51-95: per set its items with the owned amount, id, name, score, total score,
/// boost score, the optional bonus and reward items, release and snapshot times in ms (-1 = none), status and the claim of each prize.
/// </summary>
public sealed class NftCollectionsComposer(CollectorBook book, DateTimeOffset now) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.NftCollectionsComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(book.Sets.Length);

        foreach (var set in book.Sets) {
            packet.WriteInteger(set.Products.Length);

            foreach (var product in set.Products) {
                NftProductWriter.Write(packet, product, book.Owned(product));
            }

            packet.WriteString(set.Id);
            packet.WriteString(set.Name);
            packet.WriteInteger(book.SetScore(set));
            packet.WriteInteger(set.TotalScore);
            packet.WriteInteger(set.BoostScore);
            WritePrize(packet, set.Bonus);
            WritePrize(packet, set.Reward);
            DailyTaskWireWriter.WriteLong(packet, set.ReleasedAt?.ToUnixTimeMilliseconds() ?? -1);
            DailyTaskWireWriter.WriteLong(packet, set.SnapshotAt?.ToUnixTimeMilliseconds() ?? -1);
            packet.WriteShort(0);
            WriteClaim(packet, set, SetPrize.Bonus);
            WriteClaim(packet, set, SetPrize.Reward);
        }
    }

    private void WritePrize(IOutgoingPacket packet, CollectibleProduct? product)
    {
        packet.WriteBoolean(product != null);

        if (product != null) {
            NftProductWriter.Write(packet, product, book.Owned(product));
        }
    }

    // AIR CollectibleItemClaim.as:23-26. A prize can be claimed once; status 0 means it can be claimed now.
    private void WriteClaim(IOutgoingPacket packet, CollectibleSet set, SetPrize prize)
    {
        if (set.Prize(prize) == null) {
            return;
        }

        packet.WriteString(set.ClaimId(prize));
        packet.WriteInteger(book.HasClaimed(set, prize) ? 1 : 0);
        packet.WriteInteger(1);
        packet.WriteShort((short)(book.CanClaim(set, prize, now) ? 0 : 1));
    }
}
