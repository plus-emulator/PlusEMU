using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Action editor saves answered and stored the way Habbo answers and stores them.
public sealed class WiredActionEditorSaveParityTests
{
    private static readonly string LongName = new('x', 33);

    // Observed on AIR WIN63-202610151745-318700621: Control Clock saves the five offered operations and also an operation the editor
    // does not offer, and refuses a pick that is not a counter.
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    public void ControlClockSavesAnyOperationForACounterPick(int operation)
    {
        using var raw = new WiredRawSave("wf_act_control_clock");
        var counter = raw.Furni("wf_upcounter1");

        var result = raw.Save(ints: [operation], picks: [counter], furni: [100]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([operation], result.Ints);
        Assert.Equal([100], result.Furni);
    }

    [Fact]
    public void ControlClockRefusesAPickThatIsNotACounter()
    {
        using var raw = new WiredRawSave("wf_act_control_clock");
        var plain = raw.Furni();

        var result = raw.Save(ints: [0], picks: [plain], furni: [100]);

        Assert.Equal("wiredfurni.error.require_counter_furni", result.Key);
    }

    // Observed on AIR WIN63-202610201032-107964540: Bot Change Clothes saves an empty bot name with an empty figure, sent as an empty
    // text, for every bot source and reads it back as a single tab.
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(201)]
    public void BotClothesSavesAnEmptyNameAndFigure(int source)
    {
        using var raw = new WiredRawSave("wf_act_bot_clothes");

        var result = raw.Save(text: "", users: [source]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal("\t", result.Text);
        Assert.Equal([source], result.Users);
    }

    // Observed on AIR WIN63-202610151745-318700621: Bot Move saves a 33-character bot name.
    [Fact]
    public void BotMoveSavesABotNameLongerThanTheEditorField()
    {
        using var raw = new WiredRawSave("wf_act_bot_move");

        var result = raw.Save(ints: [], text: LongName, furni: [100], users: [100]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(LongName, result.Text);
    }

    [Theory]
    [InlineData("wf_act_bot_move", "Robbie")]
    [InlineData("wf_act_bot_teleport", "Robbie")]
    [InlineData("wf_act_bot_clothes", "Robbie\thr-100.hd-180-1")]
    public void BotRowsStoredBeforeKeepLoading(string name, string text)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var stored = WiredNativeEditorProjection.DefaultNative(descriptor) with { Text = text };

        Assert.True(WiredNativeEditorProjection.TryCompile(1, descriptor, stored, out var runtime, storedRow: true));
        Assert.Equal(text, runtime.Text);
    }

    [Fact]
    public void AControlClockRowStoredBeforeKeepsLoading()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_control_clock", out var descriptor));
        var stored = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [3] };

        Assert.True(WiredNativeEditorProjection.TryCompile(1, descriptor, stored, out var runtime, storedRow: true));
        Assert.Equal([3, 100], runtime.IntParams.ToArray());
    }
}
