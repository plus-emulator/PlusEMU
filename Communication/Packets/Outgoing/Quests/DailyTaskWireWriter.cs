using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests.DailyTasks;

namespace Plus.Communication.Packets.Outgoing.Quests;

/// <summary>
/// Writes daily tasks the way AIR WIN63-202610151745-318700621 reads them: package_180/class_2919.as:39-61 for a task and
/// package_180/class_4315.as:17-24 for a reward.
/// </summary>
internal static class DailyTaskWireWriter
{
    public static void WriteTasks(IOutgoingPacket packet, IReadOnlyList<DailyTaskWire> tasks)
    {
        packet.WriteInteger(tasks.Count);

        foreach (var task in tasks) {
            WriteTask(packet, task);
        }
    }

    public static void WriteTask(IOutgoingPacket packet, DailyTaskWire task)
    {
        WriteLong(packet, task.TaskId);
        packet.WriteString(task.TaskCode);
        packet.WriteString(task.QuestTypeCode);
        packet.WriteBoolean(task.IsBonus);
        packet.WriteString(task.ImageVersion);
        packet.WriteString(task.CatalogName);
        packet.WriteInteger(task.RequiredRepeats);
        packet.WriteInteger(task.Repeats);
        packet.WriteByte((byte)task.Status);
        packet.WriteInteger(task.SecondsLeft);
        packet.WriteInteger(task.Rewards.Length);

        foreach (var reward in task.Rewards) {
            packet.WriteShort(reward.ProductItemTypeId);
            packet.WriteString(reward.RewardTypeId);
            packet.WriteString(reward.ExtraParams);
            packet.WriteInteger(reward.Amount);
        }
    }

    /// <summary>readLong: the high and then the low 32 bits.</summary>
    public static void WriteLong(IOutgoingPacket packet, long value)
    {
        packet.WriteInteger(unchecked((int)(value >> 32)));
        packet.WriteInteger(unchecked((int)value));
    }
}
