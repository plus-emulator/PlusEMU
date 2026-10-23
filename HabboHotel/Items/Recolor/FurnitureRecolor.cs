using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Recolor;

/// <summary>
/// The in-room recolour window's Apply. The client offers it to the room owner, users with rights and room controllers for floor furni with a
/// recolorable colour, and only sends colours from the furni's palette. The scope is the window's "Apply to:" menu.
/// </summary>
public static class FurnitureRecolor
{
    public const int OnlyThis = 0;
    public const int SameColor = 1;
    public const int SameType = 2;
    public const int SameTypeAndColor = 3;

    /// <summary>
    /// Recolours the furni the scope selects and tells the room. Each furni keeps to its own palette, so one whose palette
    /// lacks the colour is left alone. Returns the furni now in the colour, empty when the request was refused.
    /// </summary>
    public static IReadOnlyList<Item> Apply(Room room, GameClient session, uint itemId, int scope, int red, int green, int blue)
    {
        if (!ReferenceEquals(session.GetHabbo()?.CurrentRoom, room) || !room.CheckRights(session, false, true)) {
            return [];
        }

        if (scope is < OnlyThis or > SameTypeAndColor || red is < 0 or > 255 || green is < 0 or > 255 || blue is < 0 or > 255) {
            return [];
        }

        var target = room.GetRoomItemHandler().GetItem(itemId);

        if (target is not { IsTemporary: false, IsFloorItem: true } || target.ExtraData is not RecolorableDataFormat targetColor) {
            return [];
        }

        var rgb = (red << 16) | (green << 8) | blue;

        if (!RecolorablePalette.Allows(target.Definition.ItemName, rgb)) {
            return [];
        }

        var fromColor = targetColor.Rgb;
        var recoloured = new List<Item>();

        foreach (var item in Select(room, target, scope, fromColor)) {
            if (!RecolorablePalette.Allows(item.Definition.ItemName, rgb)) {
                continue;
            }

            if (((RecolorableDataFormat)item.ExtraData).SetColor(rgb)) {
                room.GetRoomItemHandler().UpdateItem(item);
            }

            recoloured.Add(item);
        }

        room.SendPacket(new FurniRecolorUpdateComposer([new FurniRecolorUpdate([.. recoloured.Select(item => item.Id)], red, green, blue)]));

        return recoloured;
    }

    private static IEnumerable<Item> Select(Room room, Item target, int scope, int fromColor)
    {
        if (scope == OnlyThis) {
            return [target];
        }

        return room.GetRoomItemHandler().GetFloor
            .Where(item => item is { IsTemporary: false } && item.ExtraData is RecolorableDataFormat color
                && (scope == SameColor || item.Definition.Id == target.Definition.Id)
                && (scope == SameType || color.Rgb == fromColor))
            .OrderBy(item => item.Id);
    }
}
