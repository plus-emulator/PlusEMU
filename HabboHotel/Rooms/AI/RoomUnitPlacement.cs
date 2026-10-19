using Plus.Core;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Rooms.AI;

/// <summary>
/// Snaps a placed bot or pet to a tile and facing without walking, as AIR's Shift+click rotate and
/// Alt-move do for rentable bots and monsterplants.
/// </summary>
internal static class RoomUnitPlacement
{
    /// <summary>
    /// Checks the tile, lets <paramref name="commit"/> store the new position (given its height) before the room
    /// sees it, then snaps the unit. A false commit leaves the unit as it was. All three run on the room
    /// owner's next tick.
    /// </summary>
    public static void Place(Room room, RoomUser unit, int x, int y, int rotation, Func<double, bool> commit)
    {
        var navigation = room.GetGameMap().Navigation;
        navigation.RunOwner(unit, (actor, sequence) => PlaceOwned(room, navigation, actor, x, y, rotation, commit, sequence));
    }

    // A rotation keeps the unit's own tile; a move needs an open floor tile no other unit stands on.
    private static bool CanPlace(Room room, RoomUser unit, int x, int y)
    {
        // A ridden horse moves with its rider.
        if (unit.RidingHorse) {
            return false;
        }

        if (unit.X == x && unit.Y == y) {
            return true;
        }

        var map = room.GetGameMap();

        return map.ValidTile(x, y) && map.SquareIsOpen(x, y, false)
            && !map.GetRoomUsers(new(x, y)).Any(other => !ReferenceEquals(other, unit));
    }

    private static double HeightAt(Room room, RoomUser unit, int x, int y) =>
        unit.X == x && unit.Y == y ? unit.Z : room.GetGameMap().SqAbsoluteHeight(x, y);

    private static void PlaceOwned(Room room, RoomNavigation navigation, RoomUser actor, int x, int y, int rotation,
        Func<double, bool> commit, long sequence)
    {
        try {
            // Checked again here: another unit may have taken the tile since the packet arrived.
            if (!CanPlace(room, actor, x, y) || !commit(HeightAt(room, actor, x, y))) {
                return;
            }
        }
        catch (Exception error) {
            // A failed store must not abort the rest of the room's queued movement.
            ExceptionLogger.LogException(error);

            return;
        }

        actor.RotBody = rotation;
        actor.RotHead = rotation;

        if (actor.X == x && actor.Y == y) {
            navigation.CancelThrough(actor, sequence);
            actor.UpdateNeeded = true;

            return;
        }

        navigation.ForcePlaceThrough(actor, x, y, room.GetGameMap().SqAbsoluteHeight(x, y), ForceResolution.ExactZ, sequence);
    }
}
