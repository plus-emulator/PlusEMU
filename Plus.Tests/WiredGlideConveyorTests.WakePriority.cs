using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A delayed effect about to fall due lets the room loop skip its 50 ms step, so the wake-up gets the room at the due time.
    [Fact]
    public void AnArmedDelayedEffectIsReportedAsDueWithinItsDelay()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_toggle_state", out var descriptor));
        var toggle = "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [0],
            PrimaryItems = [new(ObservedSubjectId, false)],
            FurniSourceTypes = [100],
            Delay = 2
        });
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (ObservedSubjectId, 9, 9, 0.0, 0, "wf_colortile", null),
            (ObservedTriggerId, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "later" })),
            (ObservedActionId, 4, 12, 0.65, 0, "wf_act_toggle_state", toggle)
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.Advance(500);

        f.Owned(() => rider.OnChat(0, "later", false));

        Assert.True(f.Room.GetWired().WakeDueWithin(1000));
        Assert.False(f.Room.GetWired().WakeDueWithin(900));
    }
}
