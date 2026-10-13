using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const int TeleportTargetId = 20;

    // Official native 8 (WIN63-202610131318, 3 s windows): the owner got avatar effect 4 at the fire, an in-place update on the third state update,
    // the relocation one update later and the effect cleared three updates after that. No avatar movement or direction entry was collected.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void NativeCompiledTeleportToLandsTheStandingHumanOnTheTargetWithoutAvatarMovementEntries(int flag)
    {
        var (f, rider, packets) = TeleportToRoom(flag);

        f.Owned(() => rider.OnChat(0, "pulse", false));
        f.Advance(200);
        Assert.Equal((4, 11, 4), (rider.X, rider.Y, rider.CurrentEffect));
        f.Advance(750);
        Assert.Equal((4, 11), (rider.X, rider.Y));
        Assert.Equal(4, rider.CurrentEffect);
        f.Advance(500);
        Assert.Equal((4, 11), (rider.X, rider.Y));
        f.Advance(500);

        Assert.Equal((8, 10, 1.0), (rider.X, rider.Y, rider.Z));
        Assert.Equal(4, rider.CurrentEffect);
        f.Advance(1500);
        Assert.Equal(0, rider.CurrentEffect);
        Assert.Equal((6, 6), (rider.RotBody, rider.RotHead));
        var own = ReadMovementEntries(packets).Where(entry => entry.Type is 0 or 3 && entry.Id == rider.VirtualId).ToArray();
        Assert.Empty(own);
    }

    // Control: the card is valid and the placement is real, whichever way the avatar publication goes.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void NativeCompiledTeleportToCardEndsOnTheTargetTile(int flag)
    {
        var (f, rider, _) = TeleportToRoom(flag);

        f.Owned(() => rider.OnChat(0, "pulse", false));
        f.Advance(3500);

        Assert.Equal((8, 10, 1.0), (rider.X, rider.Y, rider.Z));
        Assert.Equal((6, 6), (rider.RotBody, rider.RotHead));
        Assert.False(rider.IsWalking);
    }

    private static (Fixture Fixture, RoomUser Rider, List<byte[]> Packets) TeleportToRoom(int flag)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_teleport_to", out var descriptor));
        // The saved row is the native editor shape and the real store compiles it: F100 picks the one target, U0 is the triggering user.
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [flag],
            PrimaryItems = [new(TeleportTargetId, false)],
            FurniSourceTypes = [100],
            UserSourceTypes = [0],
            Delay = 0
        };
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            // bc_tile_1 is the fixture's 1x1 walkable stackable floor item of height 1 (default interaction, not a seat).
            (TeleportTargetId, 8, 10, 0.0, 0, "bc_tile_1", null),
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "pulse" })),
            (22, 4, 12, 0.65, 0, "wf_act_teleport_to", "native:" + JsonSerializer.Serialize(native))
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var room = f.MovementContext().Room;
        var target = f.Items[TeleportTargetId];

        // A faithful target: one 1x1 walkable non-seat floor item at z 0 whose tile the map reports at height 1.
        Assert.Equal((1, 1, true, false, false), (target.Definition.Width, target.Definition.Length, target.Definition.Walkable, target.Definition.IsSeat, target.IsWallItem));
        Assert.Equal((8, 10, 0.0, 1.0), (target.GetX, target.GetY, target.GetZ, target.Definition.Height));
        Assert.True(room.GetGameMap().ValidTile(8, 10));
        Assert.Equal(1.0, room.GetGameMap().SqAbsoluteHeight(8, 10));
        // The publication path needs one registered avatar with a client in a mapped room, standing.
        Assert.Same(rider, Assert.Single(room.GetRoomUserManager().GetRoomUsers()));
        Assert.Same(rider, room.GetRoomUserManager().GetRoomUserByVirtualId(rider.VirtualId));
        Assert.NotNull(rider.GetClient());
        Assert.Contains(rider, room.GetGameMap().GetRoomUsers(new(4, 11)));
        Assert.Equal((4, 11, 0.0), (rider.X, rider.Y, rider.Z));
        Assert.False(rider.IsWalking);
        var stored = ((IWiredConfiguredItem)f.Box(22)).Configuration;
        Assert.NotNull(stored.Origin?.Native);
        Assert.Equal([flag, 100, 0], stored.IntParams.ToArray());
        Assert.Equal(100, stored.FurniSources["targets"]);
        Assert.Equal(0, stored.UserSources["users"]);
        Assert.Equal([(uint)TeleportTargetId], stored.SelectedItems.ToArray());

        rider.RotBody = 6;
        rider.RotHead = 6;

        return (f, rider, CapturePublication(rider));
    }
}
