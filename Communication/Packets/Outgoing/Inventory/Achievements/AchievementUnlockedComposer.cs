using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Inventory.Achievements;

public class AchievementUnlockedComposer : IServerPacket
{
    private readonly AchievementUnlockSnapshot _snapshot;
    public uint MessageId => ServerPacketHeader.AchievementUnlockedComposer;

    public AchievementUnlockedComposer(AchievementUnlockSnapshot snapshot) => _snapshot = snapshot;

    // WIN63 package_53.class_3440. AIR reads type (compared with achievementId by the resolution view), level, badgeId and
    // badgeCode (inventory), removedBadgeCode, category (notification link), ownerCount and badgeRarityId; points, bonusPoints,
    // achievementID and showDialogToUser are parsed but unused.
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_snapshot.Id); // type: the achievement id
        packet.WriteInteger(_snapshot.Level); // level
        packet.WriteInteger(1); // badgeId: the inventory id; BadgesComposer and the unseen badge notification use 1 for every badge.
        packet.WriteString(_snapshot.Badge); // badgeCode
        packet.WriteInteger(_snapshot.PointReward); // points: achievement score gained
        packet.WriteInteger(_snapshot.PixelReward); // levelRewardPoints
        packet.WriteInteger(0); // levelRewardPointType: duckets
        packet.WriteInteger(0); // bonusPoints: none
        packet.WriteInteger(_snapshot.Id); // achievementID
        packet.WriteString(_snapshot.PreviousBadge); // removedBadgeCode
        packet.WriteString(_snapshot.Category); // category
        packet.WriteBoolean(true); // showDialogToUser
        packet.WriteInteger(_snapshot.Rarity.OwnerCount); // ownerCount
        packet.WriteInteger((int)_snapshot.Rarity.Tier); // badgeRarityId
    }
}
