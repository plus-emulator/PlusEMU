using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests.DailyTasks;

namespace Plus.Communication.Packets.Incoming.Quests;

/// <summary>GetDailyTasks has no fields (AIR WIN63-202610151745-318700621, package_24/class_1820.as:9).</summary>
internal sealed class GetDailyTasksEvent(IDailyTaskManager dailyTasks) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        dailyTasks.SendActive(session);

        return Task.CompletedTask;
    }
}
