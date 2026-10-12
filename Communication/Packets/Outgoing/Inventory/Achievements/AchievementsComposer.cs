using System.Collections.Immutable;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Inventory.Achievements;

public class AchievementsComposer(ImmutableArray<AchievementProgressSnapshot> achievements) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.AchievementsComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(achievements.Length);

        foreach (var achievement in achievements) {
            WriteAchievement(packet, achievement);
        }

        packet.WriteString(""); // Default category: none, the window opens on its overview.
    }

    // WIN63 AchievementData (package_92.class_3624), shared by Achievements 1487, AchievementProgressed 1049 and UserGameAchievements 564.
    internal static void WriteAchievement(IOutgoingPacket packet, AchievementProgressSnapshot achievement)
    {
        packet.WriteInteger(achievement.Id); // achievementId
        packet.WriteInteger(achievement.TargetLevel); // level
        packet.WriteString(achievement.Badge); // badgeId
        packet.WriteInteger(achievement.ScoreAtStartOfLevel); // scoreAtStartOfLevel: the previous level's threshold
        packet.WriteInteger(achievement.Requirement); // scoreLimit: this level's threshold
        packet.WriteInteger(achievement.RewardPixels); // levelRewardPoints
        packet.WriteInteger(0); // levelRewardPointType: duckets
        packet.WriteInteger(achievement.Progress); // currentPoints: the total score
        packet.WriteBoolean(achievement.Completed); // finalLevel
        packet.WriteString(achievement.Category); // category
        packet.WriteString(string.Empty); // subCategory
        packet.WriteInteger(achievement.TotalLevels); // levelCount
        packet.WriteInteger(0); // displayMethod: show the progress of the level
        packet.WriteShort((short)achievement.State); // state
    }
}
