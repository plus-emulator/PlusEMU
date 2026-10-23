using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>Score, highest score and level.</summary>
public sealed class CollectorScoreComposer(int score, int highestScore, int level) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.CollectorScoreComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(score);
        packet.WriteInteger(highestScore);
        packet.WriteInteger(level);
    }
}
