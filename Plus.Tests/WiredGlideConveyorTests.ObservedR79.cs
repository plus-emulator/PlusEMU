using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Native cards saved through the real editor shape, then fired by the real triggers.
public sealed partial class WiredGlideConveyorTests
{
    private const uint ObservedSubjectId = 40;
    private const uint ObservedTriggerId = 41;
    private const uint ObservedActionId = 42;

    private static string ObservedNative(string name, int[] owned, uint[] picks, int furniSource)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        return "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [.. owned],
            PrimaryItems = [.. picks.Select(id => new WiredNativeItemReference(id, false))],
            FurniSourceTypes = [furniSource],
            Delay = 0
        });
    }

    private static string ObservedClick(uint pick) =>
        JsonSerializer.Serialize(new WiredConfiguration { IntParams = [100], SelectedItems = [pick] });

    private static (Fixture Fixture, RoomUser Rider, Plus.HabboHotel.Items.Item Subject) ObservedClickRoom(string name, int[] owned, int furniSource,
        string subjectName = "bc_tile_1", double subjectZ = 0.0)
    {
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (ObservedSubjectId, 9, 9, subjectZ, 0, subjectName, null),
            (ObservedTriggerId, 4, 12, 0.0, 0, "wf_trg_click_furni", ObservedClick(ObservedSubjectId)),
            (ObservedActionId, 4, 12, 0.65, 0, name, ObservedNative(name, owned, [ObservedSubjectId], furniSource))
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);

        return (f, rider, f.Items[ObservedSubjectId]);
    }

    private static void ObservedClickSubject(Fixture f, RoomUser rider, Plus.HabboHotel.Items.Item subject) =>
        f.Owned(() => f.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.ClickFurni) { Actor = rider, EventItem = subject }));

    // The saved ints [-2, 0] with furniture source 0 move the clicked tile from (6,1) to (4,1); the picked furniture stays put.
    [Fact]
    public void NativeRelativeMoveWithSourceZeroMovesTheClickedFurniture()
    {
        var picked = ObservedSubjectId;
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null),
            (43, 12, 12, 0.0, 0, "bc_tile_1", null),
            (ObservedTriggerId, 4, 12, 0.0, 0, "wf_trg_click_furni", ObservedClick(43)),
            (ObservedActionId, 4, 12, 0.65, 0, "wf_act_rel_mov", ObservedNative("wf_act_rel_mov", [-2, 0], [picked], 0))
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var clicked = f.Items[43];

        ObservedClickSubject(f, rider, clicked);
        f.Advance(500);

        Assert.Equal((10, 12), (clicked.GetX, clicked.GetY));
        Assert.Equal((9, 9), (f.Items[ObservedSubjectId].GetX, f.Items[ObservedSubjectId].GetY));
    }

    // Operator 0 adds (value 100 raises a marker from 7.24 to 8.24), 1 subtracts and 2 sets.
    [Theory]
    [InlineData(0, 150, 3.5)]
    [InlineData(1, 150, 0.5)]
    [InlineData(2, 150, 1.5)]
    public void NativeSetAltitudeAppliesTheSavedOperatorAndHundredths(int op, int value, double expected)
    {
        var (f, rider, subject) = ObservedClickRoom("wf_act_set_altitude", [value, op], 100, subjectZ: 2.0);
        Assert.Equal(2.0, subject.GetZ);

        ObservedClickSubject(f, rider, subject);
        f.Advance(500);

        Assert.Equal(expected, subject.GetZ, 2);
    }

    // The toggle cycles the marker state forward on every fire.
    [Fact]
    public void NativeToggleStateCyclesTheStateForward()
    {
        var (f, rider, subject) = ObservedClickRoom("wf_act_toggle_state", [0], 100, "wf_colortile");
        var seen = new List<string>();

        for (var i = 0; i < 3; i++) {
            ObservedClickSubject(f, rider, subject);
            f.Advance(500);
            seen.Add(subject.LegacyDataString);
        }

        Assert.Equal(["1", "2", "3"], seen);
    }

    // The stack boxes flip 1 then 0 on consecutive fires, and the says-something trigger flips like any other trigger.
    [Fact]
    public void SaysTriggerAndActionFlashOnEveryFiring()
    {
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null),
            // Runtime shape [match, hide, owner-only]; exact match, nothing hidden.
            (ObservedTriggerId, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "alpha" })),
            (ObservedActionId, 4, 12, 0.65, 0, "wf_act_rel_mov", ObservedNative("wf_act_rel_mov", [1, 0], [ObservedSubjectId], 100))
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var states = new List<(string Trigger, string Action)>();

        for (var i = 0; i < 3; i++) {
            f.Owned(() => rider.OnChat(0, "alpha", false));
            f.Advance(500);
            states.Add((f.Items[ObservedTriggerId].LegacyDataString, f.Items[ObservedActionId].LegacyDataString));
        }

        Assert.Equal([("1", "1"), ("0", "0"), ("1", "1")], states);
    }
}
