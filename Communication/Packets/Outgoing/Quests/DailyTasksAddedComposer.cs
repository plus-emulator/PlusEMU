using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests.DailyTasks;

namespace Plus.Communication.Packets.Outgoing.Quests;

/// <summary>
/// Tasks added to the list; a bonus task first makes the client announce it.
/// </summary>
public sealed class DailyTasksAddedComposer(IReadOnlyList<DailyTaskWire> tasks) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.DailyTasksAddedComposer;

    public void Compose(IOutgoingPacket packet) => DailyTaskWireWriter.WriteTasks(packet, tasks);
}
