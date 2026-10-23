using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

public sealed class WiredValidationErrorComposer(string error, IReadOnlyList<KeyValuePair<string, string>>? parameters = null) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredValidationErrorComposer;

    // A localization key, then a count of (placeholder key, value) pairs.
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(error);
        packet.WriteInteger(parameters?.Count ?? 0);

        foreach (var (key, value) in parameters ?? []) {
            packet.WriteString(key);
            packet.WriteString(value);
        }
    }
}
