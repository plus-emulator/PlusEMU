using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// Asks the client to confirm a pickup: package_64/class_3455 -> package_71/class_3609 (parse L52-62, WIN63-202610131318-860371703) reads
/// [category (1 wall, else floor), id, title key, body key]; the client shows them as ${key} and on OK sends PickupObject with
/// confirmed set (room/class_1993.as:340-363).
/// </summary>
public sealed class ObjectRemoveConfirmComposer(uint itemId, bool wall, string title, string body) : IServerPacket
{
    // Observed on hhus (r90) for a permanent user variable: 83 bytes, which these two keys make.
    public const string VariableTitle = "wiredfurni.variables.pickup.title";
    public const string VariableBody = "wiredfurni.variables.pickup.body";

    public uint MessageId => ServerPacketHeader.ObjectRemoveConfirmComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(wall ? 1 : 2);
        packet.WriteUInteger(itemId);
        packet.WriteString(title);
        packet.WriteString(body);
    }
}
