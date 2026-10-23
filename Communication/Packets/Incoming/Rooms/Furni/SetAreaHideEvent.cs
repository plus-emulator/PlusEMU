using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.AreaHide;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// The area hider widget saves: int id, int rootX, int rootY, int width, int length, bool invisibility, bool wallItems, bool invert.
/// Every change of the widget is sent at once, and the answer is a data update.
/// </summary>
internal sealed class SetAreaHideEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var itemId = BuildersClubItemIds.FromWire(packet.ReadUInt());
        var x = packet.ReadInt();
        var y = packet.ReadInt();
        var width = packet.ReadInt();
        var length = packet.ReadInt();
        var invisibility = packet.ReadBool();
        var wallItems = packet.ReadBool();
        var invert = packet.ReadBool();

        if (session.GetHabbo().CurrentRoom is { } room) {
            AreaHideState.Apply(room, session, itemId, x, y, width, length, invisibility, wallItems, invert);
        }

        return Task.CompletedTask;
    }
}
