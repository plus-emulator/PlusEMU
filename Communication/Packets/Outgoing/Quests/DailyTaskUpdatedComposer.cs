using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests.DailyTasks;

namespace Plus.Communication.Packets.Outgoing.Quests;

/// <summary>New repeats and status of one task.</summary>
public sealed class DailyTaskUpdatedComposer(long taskId, int repeats, DailyTaskStatus status, int secondsLeft) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.DailyTaskUpdatedComposer;

    public void Compose(IOutgoingPacket packet)
    {
        DailyTaskWireWriter.WriteLong(packet, taskId);
        packet.WriteInteger(repeats);
        packet.WriteByte((byte)status);
        packet.WriteInteger(secondsLeft);
    }
}
