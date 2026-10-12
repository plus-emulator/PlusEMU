using System.Collections.Immutable;
using Plus.Communication.Packets.Outgoing.Inventory.Achievements;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Game;

// WIN63 UserGameAchievementsMessageParser: the game type id, then the Achievements (1487) payload.
public class GameAchievementListComposer(int gameId, ImmutableArray<AchievementProgressSnapshot> achievements) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.GameAchievementListComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(gameId);
        packet.WriteInteger(achievements.Length);

        foreach (var achievement in achievements) {
            AchievementsComposer.WriteAchievement(packet, achievement);
        }

        packet.WriteString(""); // Default category
    }
}
