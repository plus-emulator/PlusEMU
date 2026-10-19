using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests.DailyTasks;

namespace Plus.Communication.Packets.Outgoing.Quests;

/// <summary>The user's daily tasks; the client replaces its list (AIR package_162/class_3100, parser package_180/class_3130.as:23-35).</summary>
public sealed class ActiveDailyTasksComposer(IReadOnlyList<DailyTaskWire> tasks) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.ActiveDailyTasksComposer;

    public void Compose(IOutgoingPacket packet) => DailyTaskWireWriter.WriteTasks(packet, tasks);
}
