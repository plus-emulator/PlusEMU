using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

public sealed class WiredValidationErrorComposer(string error, IReadOnlyList<KeyValuePair<string, string>>? parameters = null) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredValidationErrorComposer;

    // AIR WIN63-202610151745-318700621 userdefinedroomevents/class_3246.as:27: a localization key, then a count of
    // (placeholder key, value) pairs (incoming/userdefinedroomevents/class_3128.as:13).
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
