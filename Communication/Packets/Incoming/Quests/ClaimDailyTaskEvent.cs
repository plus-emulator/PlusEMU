using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests.DailyTasks;

namespace Plus.Communication.Packets.Incoming.Quests;

/// <summary>ClaimDailyTask sends the task id as an int.</summary>
internal sealed class ClaimDailyTaskEvent(IDailyTaskManager dailyTasks) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        dailyTasks.Claim(session, packet.ReadInt());

        return Task.CompletedTask;
    }
}
