using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

// The state a user entering the room is sent for Set Furni Highlight and Set Furni Visibility.
// Observed on AIR WIN63-202610201032-107964540.
public partial class ModernWiredRuntimeTests
{
    [Fact]
    public void AHighlightSwitchedOffForEveryoneStaysInTheEntryStateAtStrengthZero()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        OctoberBox<WiredModernAction>(f, 110, "wf_act_set_highlight", 0, 0, native => native with
        {
            OwnedIntParams = [1, 0, 0, 0xFFEA00, 0, 4, 0],
            PrimaryItems = [new(1, false)],
            FurniSourceTypes = [100],
            UserSourceTypes = [0, 0]
        });
        f.Fire(land: false);
        var sent = Capture(f.Client);

        f.Room.GetWired().SendVisualizationState(f.Client);

        var state = Reply(sent, ServerPacketHeader.WiredFurniVisualizationStateComposer);
        Assert.Equal((0, 1, 1, 0, 0, 0xFFEA00, 0, 4), (state.Int(), state.Int(), state.Int(), state.Int(), state.Int(), state.Int(), state.Int(), state.Int()));
        state.End();
    }

    [Fact]
    public void AFurniMadeVisibleAgainForEveryoneStaysInTheEntryState()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        OctoberBox<WiredModernAction>(f, 110, "wf_act_set_visibility", 0, 0, native => native with
        {
            OwnedIntParams = [1, 0, 0, 0, 0],
            PrimaryItems = [new(1, false)],
            FurniSourceTypes = [100],
            UserSourceTypes = [0, 0]
        });
        f.Fire(land: false);
        var sent = Capture(f.Client);

        f.Room.GetWired().SendVisualizationState(f.Client);

        var state = Reply(sent, ServerPacketHeader.WiredFurniVisualizationStateComposer);
        Assert.Equal((1, 1, 0, false, 0, 0), (state.Int(), state.Int(), state.Int(), state.Bool(), state.Int(), state.Int()));
        state.End();
    }

    [Theory]
    [InlineData("wf_act_set_visibility", new[] { 1, 100, 1, 4, 1 })]
    [InlineData("wf_act_set_highlight", new[] { 1, 0, 100, 0xFFEA00, 0, 4, 1 })]
    public void EffectsOnUsersAreNeverPartOfTheEntryState(string name, int[] ints)
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        OctoberBox<WiredModernAction>(f, 110, name, 0, 0, native => native with
        {
            OwnedIntParams = [.. ints],
            FurniSourceTypes = [100],
            UserSourceTypes = [0, 0]
        });
        f.Fire(land: false);
        var sent = Capture(f.Client);

        f.Room.GetWired().SendVisualizationState(f.Client);

        Assert.Empty(sent);
    }
}
