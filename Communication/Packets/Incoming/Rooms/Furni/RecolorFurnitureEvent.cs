using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Recolor;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>Int furni id, int scope, then the colour as three ints r, g, b.</summary>
internal sealed class RecolorFurnitureEvent : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var itemId = BuildersClubItemIds.FromWire((uint)packet.ReadInt());
        var scope = packet.ReadInt();
        var red = packet.ReadInt();
        var green = packet.ReadInt();
        var blue = packet.ReadInt();

        FurnitureRecolor.Apply(room, session, itemId, scope, red, green, blue);

        return Task.CompletedTask;
    }
}
