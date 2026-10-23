using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A queued firing counts its delays from the pass its stack first runs in; the wake-up is armed for that time, not for the moment
    // the event was queued. A Furni Clicked event put straight in the queue stands for any queued event (a click itself runs at once).
    [Fact]
    public void AQueuedFiringArmsItsWakeUpFromThePassItRunsIn()
    {
        string Toggle(uint target, int delay)
        {
            Assert.True(WiredBoxRegistry.TryGet("wf_act_toggle_state", out var descriptor));

            return "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
            {
                Category = descriptor.Category,
                NativeCode = descriptor.EditorCode,
                OwnedIntParams = [0],
                PrimaryItems = [new(target, false)],
                FurniSourceTypes = [100],
                Delay = delay
            });
        }

        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (90, 9, 9, 0.0, 0, "wf_colortile", null),
            (91, 9, 10, 0.0, 0, "wf_colortile", null),
            (92, 12, 12, 0.0, 0, "bc_tile_1", null),
            (93, 4, 12, 0.0, 0, "wf_trg_click_furni", ObservedClick(92)),
            (94, 4, 12, 0.65, 0, "wf_act_toggle_state", Toggle(90, 0)),
            (95, 4, 12, 1.3, 0, "wf_act_toggle_state", Toggle(91, 2))
        };
        var f = new Fixture(layout, live: true);
        var now = f.Items[90];
        now.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.Advance(500);
        var wakes = new List<long>();
        f.SetEngineCallback("<ActionDue>k__BackingField", (Action<long>)wakes.Add);

        f.Owned(() => f.Engine.Enqueue(new WiredRuntimeEvent(WiredEventKind.ClickFurni) { Actor = rider, EventItem = f.Items[92] }));
        long ranAt = -1;

        for (var pass = 0; pass < 10 && ranAt < 0; pass++) {
            f.Advance(50);

            if (now.LegacyDataString != "0") {
                ranAt = f.Engine.Now;
            }
        }

        Assert.True(ranAt > 0);
        Assert.Contains(ranAt + 1000, wakes);
    }
}
