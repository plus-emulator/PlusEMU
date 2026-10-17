using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.WiredVariables;

/// <summary>
/// One object's variable values for the inspection tab (AIR WIN63-202610151745-318700621 package_121/WiredObjectInspectionData.as:25-64):
/// type (0 furni, 1 user, -10 room), the furni's item id or the user's room index (nothing for the room), then id/value pairs, and for a
/// furni the ids of the Wired boxes that pick it.
/// </summary>
public sealed class WiredVariablesForObjectComposer(int type, int objectId, IReadOnlyList<(string Id, long Value)> values,
    IReadOnlyList<uint> configuredIn) : IServerPacket
{
    private readonly (string Id, long Value)[] _values = [.. values];
    private readonly uint[] _configuredIn = [.. configuredIn];
    public uint MessageId => ServerPacketHeader.WiredVariablesForObjectComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInt(type);

        if (type is 0 or 1) {
            packet.WriteInt(objectId);
        }

        packet.WriteInt(_values.Length);

        foreach (var (id, value) in _values) {
            packet.WriteString(id);
            packet.WriteInt((int)Math.Clamp(value, int.MinValue, int.MaxValue));
        }

        if (type == 0) {
            packet.WriteInt(_configuredIn.Length);

            foreach (var id in _configuredIn) {
                packet.WriteUInteger(id);
            }
        }
    }
}
