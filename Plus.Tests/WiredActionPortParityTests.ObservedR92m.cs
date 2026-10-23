using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredActionPortParityTests
{
    // move_rotate_user marks the moved owner with "wf"; furni_to_user lifts the owner without it. user_to_furni keeps the facing the owner arrived with (4 after walking south, 6 after walking west).
    [Fact]
    public void WiredUserMovesMarkTheWfStatusAndKeepTheFacing()
    {
        var (room, map, items) = World();
        using var roomOwnerScope = RoomOwnerScope.Enter(room);
        var owner = Avatar(room, 1, 2, 2);
        owner.RotBody = owner.RotHead = 4;
        Register(room, map, owner);

        Assert.True(Box(room, "wf_act_move_rotate_user", [2, -1, 0], []).Execute(Context(room, [], [owner], owner)));
        Assert.True(owner.HasStatus("wf"));
        Assert.Equal((4, 4), (owner.RotBody, owner.RotHead));

        var (otherRoom, otherMap, otherItems) = World();
        using var otherRoomOwnerScope = RoomOwnerScope.Enter(otherRoom);
        var lifted = Avatar(otherRoom, 1, 3, 3);
        Register(otherRoom, otherMap, lifted);
        var mover = Floor(1, 1, 1);
        mover.Definition.Stackable = true;
        mover.Definition.Walkable = true;
        mover.Definition.Height = 1;
        Place(otherMap, otherItems, mover);

        Assert.True(Box(otherRoom, "wf_act_furni_to_user", [100, 0], [mover.Id]).Execute(Context(otherRoom, [mover], [lifted], lifted)));
        Assert.False(lifted.HasStatus("wf"));
    }
}
