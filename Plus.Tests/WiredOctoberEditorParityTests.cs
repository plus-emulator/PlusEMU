using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>Editor payloads of the WIN63-202610121709 AIR client, checked against its decompiled editors.</summary>
public sealed class WiredOctoberEditorParityTests
{
    [Theory]
    [InlineData("wf_act_bot_talk", 4)]
    [InlineData("wf_act_bot_talk_to_avatar", 5)]
    public void BotTalkEditorsSaveTheHideBotNameField(string name, int runtimeLength)
    {
        // actiontypes BOT_TALK / BOT_TALK_DIRECT_TO_AVTR send [mode, bubble width, hide bot name ? 1 : 0].
        var runtime = Compile(name, native => native with { OwnedIntParams = [0, 2, 1], Text = "Frank\tHello" });

        Assert.Equal(runtimeLength, runtime.IntParams.Length);
        Assert.Equal(1, runtime.IntParams[^1]);
        Assert.False(TryCompile(name, native => native with { OwnedIntParams = [0, 2, 2], Text = "Frank\tHello" }));
        Assert.True(TryCompile(name, native => native with { OwnedIntParams = [0, 2], Text = "Frank\tHello" }));
    }

    [Fact]
    public void ShowMessageTakesFourHundredCharacters()
    {
        // The Show Message editor is TextAreaParam(40, -1, 8, -1, 400).
        Assert.True(TryCompile("wf_act_show_message", native => native with { Text = new string('a', 400) }));
        Assert.False(TryCompile("wf_act_show_message", native => native with { Text = new string('a', 401) }));
    }

    [Theory]
    [InlineData(120, 60_000L)]
    [InlineData(-38_800, 38_800L)]
    [InlineData(-60_000, 60_000L)]
    [InlineData(-50, 50L)]
    public void ActionDelaysTakeStandardPulsesAndPreciseMilliseconds(int encoded, long milliseconds)
    {
        // ActionDelaySection.encodedDelay: precise mode is -(steps * 50) for 0..1200 steps, standard mode 0..120 pulses.
        var runtime = Compile("wf_act_toggle_state", native => native with { Delay = encoded });

        Assert.Equal(encoded, runtime.Delay);
        Assert.Equal(milliseconds, WiredConfigurationLimits.DelayMilliseconds(runtime.Delay));
    }

    [Theory]
    [InlineData(120)]
    [InlineData(-60_000)]
    public void MovementEffectsTakeTheSharedDelayRange(int encoded)
    {
        var runtime = Compile("wf_act_rel_mov", native => native with { OwnedIntParams = [1, 1], Delay = encoded });

        Assert.True(Plus.HabboHotel.Items.Wired.Modern.Actions.WiredMovementConfiguration.TryValidate("wf_act_rel_mov", runtime, out _, out _));
    }

    [Theory]
    [InlineData(-38_801)]
    [InlineData(-60_050)]
    public void ActionDelaysRefuseOffStepPreciseValues(int encoded) =>
        Assert.False(TryCompile("wf_act_toggle_state", native => native with { Delay = encoded }));

    internal static WiredConfiguration Compile(string name, Func<WiredNativeEditorConfiguration, WiredNativeEditorConfiguration> edit)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        Assert.True(WiredNativeEditorProjection.TryCompile(1, descriptor, edit(WiredNativeEditorProjection.DefaultNative(descriptor)), out var runtime));

        return runtime;
    }

    internal static bool TryCompile(string name, Func<WiredNativeEditorConfiguration, WiredNativeEditorConfiguration> edit) =>
        WiredBoxRegistry.TryGet(name, out var descriptor)
        && WiredNativeEditorProjection.TryCompile(1, descriptor, edit(WiredNativeEditorProjection.DefaultNative(descriptor)), out _);

    internal static ImmutableArray<int> Ints(params int[] values) => [.. values];
}
