using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests.DailyTasks;

namespace Plus.Communication.Packets.Outgoing.Quests;

/// <summary>The user's daily tasks; the client replaces its list.</summary>
public sealed class ActiveDailyTasksComposer(IReadOnlyList<DailyTaskWire> tasks) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.ActiveDailyTasksComposer;

    public void Compose(IOutgoingPacket packet) => DailyTaskWireWriter.WriteTasks(packet, tasks);
}
