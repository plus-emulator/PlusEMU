using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests.DailyTasks;

namespace Plus.Communication.Packets.Incoming.Quests;

/// <summary>GetDailyTasks has no fields.</summary>
internal sealed class GetDailyTasksEvent(IDailyTaskManager dailyTasks) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        dailyTasks.SendActive(session);

        return Task.CompletedTask;
    }
}
