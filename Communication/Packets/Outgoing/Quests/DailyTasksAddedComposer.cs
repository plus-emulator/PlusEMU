using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests.DailyTasks;

namespace Plus.Communication.Packets.Outgoing.Quests;

/// <summary>
/// Tasks added to the list; a bonus task first makes the client announce it (AIR package_162/class_2799, parser
/// package_180/class_3531.as:23-35, DailyTasksController.onTasksAdded).
/// </summary>
public sealed class DailyTasksAddedComposer(IReadOnlyList<DailyTaskWire> tasks) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.DailyTasksAddedComposer;

    public void Compose(IOutgoingPacket packet) => DailyTaskWireWriter.WriteTasks(packet, tasks);
}
