using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests.DailyTasks;

namespace Plus.Communication.Packets.Incoming.Quests;

/// <summary>ClaimDailyTask sends the task id as an int (AIR WIN63-202610151745-318700621, package_24/class_2568.as:11-15).</summary>
internal sealed class ClaimDailyTaskEvent(IDailyTaskManager dailyTasks) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        dailyTasks.Claim(session, packet.ReadInt());

        return Task.CompletedTask;
    }
}
