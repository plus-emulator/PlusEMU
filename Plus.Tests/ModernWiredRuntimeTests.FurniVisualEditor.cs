using System.Text;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Saves of Set Furni Highlight and Set Furni Visibility, answered the way Habbo answers them.
public partial class ModernWiredRuntimeTests
{
    private const string Highlight = "wf_act_set_highlight";
    private const string Visibility = "wf_act_set_visibility";

    [Theory]
    [InlineData(Highlight, new[] { 0, 0, 100, 16771584, 0, 4, 0 })]
    [InlineData(Visibility, new[] { 0, 100, 0, 4, 0 })]
    public void AFreshFrameOpensOnTheDefaultValues(string name, int[] ints)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        Assert.Equal(ints, WiredNativeEditorProjection.DefaultRuntime(1, descriptor)!.Origin!.Native.OwnedIntParams.ToArray());
    }

    [Theory]
    // Missing ints take the default, an extra int is dropped.
    [InlineData(Highlight, new int[0], new[] { 0, 0, 100, 16771584, 0, 4, 0 })]
    [InlineData(Highlight, new[] { 0 }, new[] { 0, 0, 100, 16771584, 0, 4, 0 })]
    [InlineData(Highlight, new[] { 0, 5, 100, 16758528, 1, 3 }, new[] { 0, 5, 100, 16758528, 1, 3, 0 })]
    [InlineData(Highlight, new[] { 0, 5, 100, 16758528, 1, 3, 0, 9 }, new[] { 0, 5, 100, 16758528, 1, 3, 0 })]
    // Animation is 0 or 1, easing 0 to 4, audience and target users 0 or 1.
    [InlineData(Highlight, new[] { 0, 0, 100, 16771584, 2, 4, 0 }, new[] { 0, 0, 100, 16771584, 1, 4, 0 })]
    [InlineData(Highlight, new[] { 0, 0, 100, 16771584, 7, 4, 0 }, new[] { 0, 0, 100, 16771584, 1, 4, 0 })]
    [InlineData(Highlight, new[] { 0, 0, 100, 16771584, -1, 4, 0 }, new[] { 0, 0, 100, 16771584, 0, 4, 0 })]
    [InlineData(Highlight, new[] { 0, 0, 100, 16771584, 0, 5, 0 }, new[] { 0, 0, 100, 16771584, 0, 4, 0 })]
    [InlineData(Highlight, new[] { 0, 0, 100, 16771584, 0, 99, 0 }, new[] { 0, 0, 100, 16771584, 0, 4, 0 })]
    [InlineData(Highlight, new[] { 0, 0, 100, 16771584, 0, -1, 0 }, new[] { 0, 0, 100, 16771584, 0, 0, 0 })]
    [InlineData(Highlight, new[] { 2, 0, 100, 16771584, 0, 4, 0 }, new[] { 1, 0, 100, 16771584, 0, 4, 0 })]
    [InlineData(Highlight, new[] { -1, 0, 100, 16771584, 0, 4, 0 }, new[] { 1, 0, 100, 16771584, 0, 4, 0 })]
    [InlineData(Highlight, new[] { 0, 0, 100, 16771584, 0, 4, 2 }, new[] { 0, 0, 100, 16771584, 0, 4, 1 })]
    [InlineData(Highlight, new[] { 0, 0, 100, 16771584, 0, 4, -1 }, new[] { 0, 0, 100, 16771584, 0, 4, 1 })]
    [InlineData(Visibility, new int[0], new[] { 0, 100, 0, 4, 0 })]
    [InlineData(Visibility, new[] { 0, 100 }, new[] { 0, 100, 0, 4, 0 })]
    [InlineData(Visibility, new[] { 0, 100, 1, 2, 0, 9, 9 }, new[] { 0, 100, 1, 2, 0 })]
    [InlineData(Visibility, new[] { 0, 100, 2, 0, 0 }, new[] { 0, 100, 1, 0, 0 })]
    [InlineData(Visibility, new[] { 0, 100, -1, 0, 0 }, new[] { 0, 100, 1, 0, 0 })]
    [InlineData(Visibility, new[] { 0, 100, 0, 5, 0 }, new[] { 0, 100, 0, 4, 0 })]
    [InlineData(Visibility, new[] { 0, 100, 0, 99, 0 }, new[] { 0, 100, 0, 4, 0 })]
    [InlineData(Visibility, new[] { 0, 100, 0, -1, 0 }, new[] { 0, 100, 0, 0, 0 })]
    [InlineData(Visibility, new[] { 2, 100, 0, 0, 0 }, new[] { 1, 100, 0, 0, 0 })]
    [InlineData(Visibility, new[] { -1, 100, 0, 0, 0 }, new[] { 1, 100, 0, 0, 0 })]
    [InlineData(Visibility, new[] { 0, 100, 0, 0, 2 }, new[] { 0, 100, 0, 0, 1 })]
    [InlineData(Visibility, new[] { 0, 100, 0, 0, -1 }, new[] { 0, 100, 0, 0, 1 })]
    public async Task ASaveStoresTheIntsInTheirNormalisedForm(string name, int[] saved, int[] stored)
    {
        var f = new MovementOracleSession(name);

        await SaveEffect(f, FurniVisualSave(saved));

        Assert.Contains(f.Client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        Assert.Equal(stored, Assert.Single(f.Store.Saves).Origin!.Native.OwnedIntParams.ToArray());
    }

    // The save body: item, ints, text, picks, delay, furni sources, user sources, variables, second picks.
    private static object[] FurniVisualSave(int[] ints, int[]? picks = null, int delay = 0, int[]? furniSources = null, int[]? userSources = null)
    {
        picks ??= [];
        furniSources ??= [100];
        userSources ??= [0, 0];

        return [100, ints.Length, .. ints.Cast<object>(), "", picks.Length, .. picks.Cast<object>(), delay, furniSources.Length,
            .. furniSources.Cast<object>(), userSources.Length, .. userSources.Cast<object>(), 0, 0];
    }

    private static string RefusalKey(MovementOracleSession f)
    {
        var error = Assert.Single(f.Client.Packets, packet => packet.Header == ServerPacketHeader.WiredValidationErrorComposer).Body;
        var length = (error[0] << 8) | error[1];
        Assert.Equal(new byte[4], error[(2 + length)..]);

        return Encoding.UTF8.GetString(error, 2, length);
    }
}
