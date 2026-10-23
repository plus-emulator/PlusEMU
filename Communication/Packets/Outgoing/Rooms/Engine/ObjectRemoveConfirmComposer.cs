using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// Asks the client to confirm a pickup. It reads
/// [category (1 wall, else floor), id, title key, body key]; the client shows them as ${key} and on OK sends PickupObject with
/// confirmed set.
/// </summary>
public sealed class ObjectRemoveConfirmComposer(uint itemId, bool wall, string title, string body) : IServerPacket
{
    public const string VariableTitle = "wiredfurni.variables.pickup.title";
    public const string VariableBody = "wiredfurni.variables.pickup.body";

    public uint MessageId => ServerPacketHeader.ObjectRemoveConfirmComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(wall ? 1 : 2);
        packet.WriteUInteger(BuildersClubItemIds.ToWire(itemId));
        packet.WriteString(title);
        packet.WriteString(body);
    }
}
