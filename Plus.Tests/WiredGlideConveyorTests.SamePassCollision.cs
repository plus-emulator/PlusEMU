using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610151745-318700621 (r97_chase_collide_clk, r97_flee_corner_clk): User Stuff Collision raised by an effect
// runs in the same pulse as that effect.
public sealed partial class WiredGlideConveyorTests
{
    private const uint CollisionMoverId = 60;
    private const uint CollisionSubjectId = 61;

    [Theory]
    [InlineData("wf_act_chase")]
    [InlineData("wf_act_flee")]
    [InlineData("wf_act_move_to_dir")]
    public void CollisionFromAnEffectRunsInTheSamePass(string effect)
    {
        var (f, rider, subject) = CollisionRoom(effect, loop: false);

        f.Owned(() => rider.OnChat(0, "hit", false));

        // No time passes: the collision stack already toggled the subject, and the mover stayed beside the user.
        Assert.Equal("1", subject.LegacyDataString);
        Assert.Equal(5, f.Items[CollisionMoverId].GetX);
    }

    // A Furni Clicked firing is queued and runs on a fast pass; the collision its chase raises runs on that same pass.
    [Fact]
    public void CollisionFromAQueuedFiringRunsInTheSamePassAsTheEffect()
    {
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (CollisionMoverId, 5, 11, 0.0, 0, "bc_tile_1", null),
            (CollisionSubjectId, 9, 9, 0.0, 0, "wf_colortile", null),
            (62, 9, 10, 0.0, 0, "wf_colortile", null),
            (63, 12, 12, 0.0, 0, "bc_tile_1", null),
            (70, 8, 14, 0.0, 0, "wf_trg_click_furni", ObservedClick(63)),
            (71, 8, 14, 0.65, 0, "wf_act_chase", ObservedNative("wf_act_chase", [], [CollisionMoverId], 100)),
            (75, 8, 14, 1.3, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [62], 100)),
            (72, 9, 14, 0.0, 0, "wf_trg_collision", JsonSerializer.Serialize(new WiredConfiguration())),
            (73, 9, 14, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [CollisionSubjectId], 100))
        };
        var f = new Fixture(layout, live: true);
        var collided = f.Items[CollisionSubjectId];
        var fired = f.Items[62];
        collided.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        fired.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.Advance(1000);

        f.Owned(() => f.Room.GetWired().Dispatch(new WiredRuntimeEvent(WiredEventKind.ClickFurni) { Actor = rider, EventItem = f.Items[63] }));

        for (var pass = 0; pass < 10 && fired.LegacyDataString == "0"; pass++) {
            f.Advance(50);
        }

        Assert.Equal("1", fired.LegacyDataString);
        Assert.Equal("1", collided.LegacyDataString);
    }

    [Fact]
    public void ACollisionThatCollidesAgainStopsAtTheEventDepth()
    {
        var (f, rider, subject) = CollisionRoom("wf_act_chase", loop: true);
        var errors = new List<Exception>();
        f.SetEngineCallback("_error", (Action<Exception>)errors.Add);

        f.Owned(() => rider.OnChat(0, "hit", false));
        f.Advance(1000);

        // The chain ends at the event depth instead of looping, and the mover never moves onto the user.
        Assert.Empty(errors);
        Assert.Equal((5, 11), (f.Items[CollisionMoverId].GetX, f.Items[CollisionMoverId].GetY));
        Assert.False(f.Room.GetWired().NeedsFastCycle && f.Room.GetWired().GameCounterRunning);
    }

    private (Fixture Fixture, RoomUser Rider, Plus.HabboHotel.Items.Item Subject) CollisionRoom(string effect, bool loop)
    {
        var mover = effect == "wf_act_move_to_dir"
            ? JsonSerializer.Serialize(new WiredConfiguration { IntParams = [6, 0, 100, 1], SelectedItems = [CollisionMoverId] })
            : ObservedNative(effect, [], [CollisionMoverId], 100);
        var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>
        {
            (CollisionMoverId, 5, 11, 0.0, 0, "bc_tile_1", null),
            (CollisionSubjectId, 9, 9, 0.0, 0, "wf_colortile", null),
            (70, 8, 14, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "hit" })),
            (71, 8, 14, 0.65, 0, effect, mover),
            (72, 9, 14, 0.0, 0, "wf_trg_collision", JsonSerializer.Serialize(new WiredConfiguration())),
            (73, 9, 14, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [CollisionSubjectId], 100))
        };

        if (loop) {
            layout.Add((74, 9, 14, 1.3, 0, "wf_act_chase", ObservedNative("wf_act_chase", [], [CollisionMoverId], 100)));
        }

        var f = new Fixture(layout.ToArray(), live: true);
        var subject = f.Items[CollisionSubjectId];
        subject.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.Advance(1000);
        Assert.Equal((4, 11), (rider.X, rider.Y));

        return (f, rider, subject);
    }
}
