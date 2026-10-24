using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

// An action delay is 0 to 120 pulses or a precise delay of -50 to -60000 in steps of 50. Anything else is refused with the delay key.
public partial class ModernWiredRuntimeTests
{
    private static async Task<string> SaveWithDelay(string name, int delay)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var d = WiredNativeEditorProjection.DefaultNative(descriptor);
        var f = new MovementOracleSession(name);
        var ints = name == Highlight ? ValidInts(name) : d.OwnedIntParams.ToArray();

        await SaveEffect(f, ActionSave(ints, d.Text ?? "", delay: delay, furniSources: d.FurniSourceTypes.ToArray(), userSources: d.UserSourceTypes.ToArray()));

        if (f.Client.Packets.Any(p => p.Header == Plus.Communication.Packets.Outgoing.ServerPacketHeader.WiredValidationErrorComposer)) {
            return RefusalKey(f);
        }

        return f.Store.Saves.Count == 1 ? "saved" : "dropped";
    }

    [Theory]
    [InlineData("wf_act_toggle_state")]
    [InlineData("wf_act_show_message")]
    [InlineData("wf_act_move_rotate")]
    [InlineData("wf_act_teleport_to")]
    [InlineData("wf_act_reset_timers")]
    [InlineData("wf_act_set_highlight")]
    [InlineData("wf_act_set_visibility")]
    public async Task ADelayOutsideTheEditorSetIsRefusedWithTheDelayKey(string name)
    {
        foreach (var delay in new[] { 121, 3600, 3601, 100000, -1, -49, -60050, -100000 }) {
            Assert.Equal(DelayKey, await SaveWithDelay(name, delay));
        }
    }

    [Theory]
    [InlineData("wf_act_toggle_state")]
    [InlineData("wf_act_show_message")]
    [InlineData("wf_act_move_rotate")]
    [InlineData("wf_act_teleport_to")]
    [InlineData("wf_act_reset_timers")]
    [InlineData("wf_act_set_highlight")]
    public async Task ADelayInTheEditorSetIsSaved(string name)
    {
        foreach (var delay in new[] { 0, 1, 120, -50, -500, -60000 }) {
            Assert.Equal("saved", await SaveWithDelay(name, delay));
        }
    }

    [Theory]
    [InlineData("wf_act_toggle_state", 500)]
    [InlineData("wf_act_toggle_state", 3600)]
    [InlineData("wf_act_move_rotate", 200)]
    [InlineData("wf_act_teleport_to", 121)]
    public void AStoredRowWithALongDelayLoadsAndRunsWithADelayOf120(string name, int delay)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var row = WiredNativeEditorProjection.DefaultNative(descriptor) with { Delay = delay, PrimaryItems = [new(8, false)] };
        var f = new MovementOracleSession(name, row: new StoredRuntimeRow(100, name, 2, JsonSerializer.Serialize(row)));

        Assert.Equal(120, f.Action.Configuration.Delay);
        Assert.True(f.Action.TryValidateConfiguration(f.Action.Configuration, out _, out var error), error);
    }

    [Fact]
    public void AToggleStateRowStoredWithADelayOf200Runs()
    {
        const string name = "wf_act_toggle_state";
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var row = WiredNativeEditorProjection.DefaultNative(descriptor) with { Delay = 200, PrimaryItems = [new(8, false)] };
        var f = new MovementOracleSession(name, row: new StoredRuntimeRow(100, name, 2, JsonSerializer.Serialize(row)));

        Assert.True(f.Action.Execute(Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [])));
    }

    [Fact]
    public async Task AReopenedStoredBoxSavesWithoutAnError()
    {
        const string name = "wf_act_toggle_state";
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var row = WiredNativeEditorProjection.DefaultNative(descriptor) with { Delay = 500, PrimaryItems = [new(8, false)] };
        var f = new MovementOracleSession(name, row: new StoredRuntimeRow(100, name, 2, JsonSerializer.Serialize(row)));
        var opened = Assert.IsType<WiredNativeEditorConfiguration>(WiredEditorSnapshot.Capture(Assert.IsAssignableFrom<IWiredConfiguredItem>(f.Box)).Native);

        await SaveEffect(f, ActionSave(opened.OwnedIntParams.ToArray(), opened.Text ?? "", picks: [8], delay: opened.Delay ?? 0,
            furniSources: opened.FurniSourceTypes.ToArray(), userSources: opened.UserSourceTypes.ToArray()));

        Assert.Single(f.Store.Saves);
        Assert.DoesNotContain(f.Client.Packets, p => p.Header == Plus.Communication.Packets.Outgoing.ServerPacketHeader.WiredValidationErrorComposer);
    }
}
