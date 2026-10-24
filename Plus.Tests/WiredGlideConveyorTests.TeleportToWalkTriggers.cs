using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const int TeleportLeftTileId = 30;

    private static (Fixture Fixture, Plus.HabboHotel.Rooms.RoomUser Rider, List<(WiredEventKind Kind, int At, int X, int Y)> Fired)
        TeleportToWalkRoom()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_teleport_to", out var descriptor));
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [0],
            PrimaryItems = [new(TeleportTargetId, false)],
            FurniSourceTypes = [100],
            UserSourceTypes = [0],
            Delay = 0
        };
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (TeleportTargetId, 8, 10, 0.0, 0, "bc_tile_1", null),
            (TeleportLeftTileId, 4, 11, 0.0, 0, "bc_tile_1", null),
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "pulse" })),
            (22, 4, 12, 0.65, 0, "wf_act_teleport_to", "native:" + JsonSerializer.Serialize(native)),
            (23, 7, 12, 0.0, 0, "wf_trg_walks_on_furni", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [100], SelectedItems = [TeleportTargetId] })),
            (24, 9, 12, 0.0, 0, "wf_trg_walks_off_furni", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [100], SelectedItems = [TeleportLeftTileId] }))
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var fired = new List<(WiredEventKind Kind, int At, int X, int Y)>();

        return (f, rider, fired);
    }

    private static void RunTeleportToWalkRoom(Fixture f, Plus.HabboHotel.Rooms.RoomUser rider, List<(WiredEventKind Kind, int At, int X, int Y)> fired,
        Action<int>? each = null)
    {
        var elapsed = 0;
        f.Engine.ObserveEvent = (evt, _) =>
        {
            if (evt.Kind is WiredEventKind.WalkOn or WiredEventKind.WalkOff) {
                fired.Add((evt.Kind, elapsed, rider.X, rider.Y));
            }
        };

        f.Owned(() => rider.OnChat(0, "pulse", false));

        for (; elapsed < 5000; elapsed += 50) {
            f.Advance(50);
            each?.Invoke(elapsed);
        }
    }

    [Fact]
    public void TeleportToFiresWalksOnWithTheRepublishWhileTheAvatarIsStillOnItsOldTile()
    {
        var (f, rider, fired) = TeleportToWalkRoom();

        RunTeleportToWalkRoom(f, rider, fired);

        var hit = Assert.Single(fired, e => e.Kind == WiredEventKind.WalkOn);
        Assert.InRange(hit.At, 1400, 1600);
        Assert.Equal((4, 11), (hit.X, hit.Y));
    }

    [Fact]
    public void TeleportToKeepsTheLandingAndTheGlowTimings()
    {
        var (f, rider, fired) = TeleportToWalkRoom();
        var landedAt = -1;
        var glowEndedAt = -1;

        RunTeleportToWalkRoom(f, rider, fired, elapsed =>
        {
            if (landedAt < 0 && (rider.X, rider.Y) == (8, 10)) {
                landedAt = elapsed;
            }

            if (landedAt >= 0 && glowEndedAt < 0 && rider.CurrentEffect != 4) {
                glowEndedAt = elapsed;
            }
        });

        Assert.InRange(landedAt, 1900, 2100);
        Assert.InRange(glowEndedAt - landedAt, 1400, 1600);
    }

    [Fact]
    public void TeleportToFiresWalksOffOnTheTileTheUserLeavesAtTheLanding()
    {
        var (f, rider, fired) = TeleportToWalkRoom();

        RunTeleportToWalkRoom(f, rider, fired);

        var hit = Assert.Single(fired, e => e.Kind == WiredEventKind.WalkOff);
        Assert.InRange(hit.At, 1900, 2100);
        Assert.Equal((8, 10), (hit.X, hit.Y));
    }
}
