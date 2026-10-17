using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Recolor;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// AIR WIN63-202610131318-860371703 package_100/class_2402.as:11 (outgoing 569 on WIN63-202610151745-318700621 as class_2414):
/// int furni id, int scope, then the colour as three ints r, g, b.
/// </summary>
internal sealed class RecolorFurnitureEvent : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var itemId = (uint)packet.ReadInt();
        var scope = packet.ReadInt();
        var red = packet.ReadInt();
        var green = packet.ReadInt();
        var blue = packet.ReadInt();

        FurnitureRecolor.Apply(room, session, itemId, scope, red, green, blue);

        return Task.CompletedTask;
    }
}
