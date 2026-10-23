using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Quests;

// The client reads an expired flag, then the cancelled quest block.
public sealed class QuestAbortedComposer(QuestWireData quest, bool expired = false) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.QuestAbortedComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(expired);
        QuestWireSerializer.Write(packet, quest);
    }
}
