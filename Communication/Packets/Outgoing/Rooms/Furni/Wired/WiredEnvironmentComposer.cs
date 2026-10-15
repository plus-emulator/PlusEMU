using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>
/// class_3229 -> class_3405 (WIN63-202610131318-860371703, parse L25-42): [click user wired, n, enabled achievement x n]. The enabled
/// achievements are what the room's Achievement Enabler add-ons list; Progress Achievement's editor offers them (achievementsInRoom).
/// </summary>
public sealed class WiredEnvironmentComposer(bool hasClickUser, uint roomId, IReadOnlyList<string>? achievements = null) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredEnvironmentComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(hasClickUser);
        packet.WriteInteger(achievements?.Count ?? 0);

        foreach (var achievement in achievements ?? []) {
            packet.WriteString(achievement);
        }

        packet.WriteInteger(1);
        packet.WriteInteger(unchecked((int)roomId));
    }
}
