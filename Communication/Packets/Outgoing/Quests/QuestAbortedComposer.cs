using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Quests;

// AIR WIN63-202610151745-318700621 quest/class_3399.as:25 reads an expired flag, then the cancelled quest block.
public sealed class QuestAbortedComposer(QuestWireData quest, bool expired = false) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.QuestAbortedComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(expired);
        QuestWireSerializer.Write(packet, quest);
    }
}
