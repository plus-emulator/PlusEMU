using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Inventory.Achievements;

public class AchievementProgressedComposer : IServerPacket
{
    private readonly AchievementProgressSnapshot _snapshot;
    public uint MessageId => ServerPacketHeader.AchievementProgressedComposer;

    public AchievementProgressedComposer(AchievementProgressSnapshot snapshot) => _snapshot = snapshot;

    // A single achievement entry.
    public void Compose(IOutgoingPacket packet) => AchievementsComposer.WriteAchievement(packet, _snapshot);
}
