using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

/// <summary>Observed on hhus build WIN63-202610131318, r75 batch 01.</summary>
public sealed class WiredObservedBatch01Tests
{
    [Theory]
    [InlineData(0, 1, 223)]
    [InlineData(1, 1, 33)]
    [InlineData(2, 1, 40)]
    public void PlainWiredTeamOneWearsEffect223(int type, int team, int effect) =>
        Assert.Equal(effect, WiredGameState.TeamEffect(type, (Team)team));

    [Fact]
    public void RemoveFurniOffersZeroTwoHundredAndTwoHundredOneWithTwoHundredSelected()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_remove_furni", out var descriptor));
        var metadata = WiredNativeActionEditor.Metadata("wf_act_remove_furni");
        Assert.Equal([0, 200, 201], Assert.Single(metadata.FurniAllowed).ToArray());
        Assert.Equal([200], metadata.FurniDefaults.ToArray());
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        Assert.Equal([200], native.FurniSourceTypes.ToArray());
        Assert.Null(WiredNativeEditorProjection.SourceRejectionKey(descriptor, native));
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out _));
    }

    [Fact]
    public void RemoveFurniRefusesTheSelectedFurniSourceWithTheObservedKey()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_remove_furni", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { FurniSourceTypes = [100] };
        Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out _));
        Assert.Equal("wiredfurni.error.invalid_furni_source", WiredNativeEditorProjection.SourceRejectionKey(descriptor, native));
    }

    [Theory]
    [InlineData("wf_act_join_team", new[] { 1, 0 })]
    [InlineData("wf_act_leave_team", new int[0])]
    [InlineData("wf_trg_game_starts", new int[0])]
    [InlineData("wf_trg_game_ends", new int[0])]
    [InlineData("wf_trg_score_achieved", new[] { 100, 0 })]
    public void ObservedEditorDefaultsRoundTrip(string name, int[] owned)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        Assert.Equal(owned, native.OwnedIntParams.ToArray());
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out _));
    }
}
