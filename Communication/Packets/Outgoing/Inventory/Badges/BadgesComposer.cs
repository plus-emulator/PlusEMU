using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Badges;
using Plus.HabboHotel.Users.Inventory.Badges;

namespace Plus.Communication.Packets.Outgoing.Inventory.Badges;

/// <summary>
/// Total fragments, fragment index (0-based), then per badge
/// id, code, owner count and rarity id. Worn badges are not part of this packet.
/// </summary>
public sealed class BadgesComposer(BadgeInventorySnapshot snapshot) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.BadgesComposer;

    public void Compose(IOutgoingPacket packet)
    {
        // The whole inventory goes in one fragment.
        packet.WriteInteger(1);
        packet.WriteInteger(0);
        packet.WriteInteger(snapshot.Badges.Length);

        foreach (var badge in snapshot.Badges) {
            packet.WriteInteger(1);
            packet.WriteString(badge.Code);
            packet.WriteInteger(badge.Rarity.OwnerCount);
            packet.WriteInteger((int)badge.Rarity.Tier);
        }
    }
}

public static class BadgeInventoryPackets
{
    /// <summary>
    /// Sends the badge inventory followed by the user's own worn badges. The client resets worn markers on every badge list
    /// and takes them from HabboUserBadges for its own user id.
    /// </summary>
    public static void SendBadgeInventory(this GameClient? client, int userId, IEnumerable<Badge> badges)
    {
        if (client == null) {
            return;
        }

        var snapshot = BadgeInventorySnapshot.Capture(badges);
        client.Send(new BadgesComposer(snapshot));
        client.Send(new HabboUserBadgesComposer(userId, snapshot.Equipped));
    }
}
