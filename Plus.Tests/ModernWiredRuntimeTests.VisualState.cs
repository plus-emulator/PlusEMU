using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

// The state a user entering the room is sent for Set Highlight and Set Visibility.
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

    private void SelectBots(TeleportFixture f)
    {
        var item = MakeItem(102, "wf_slc_users_bytype");
        var selector = Plus.HabboHotel.Items.Wired.Modern.Selectors.WiredSelectorFactory.Create(f.Room, item, new(), TestGroupManager.Empty);
        Assert.NotNull(selector);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(selector, new() { IntParams = [4, 0, 0] }, out var config, out var error), error);
        selector.ApplyConfiguration(config);
        f.Items[102] = item;
        Assert.True(f.Engine.Add(selector));
    }

    [Theory]
    [InlineData("wf_act_set_visibility", new[] { 1, 100, 1, 4, 1 })]
    [InlineData("wf_act_set_highlight", new[] { 1, 0, 100, 0xFFB700, 1, 0, 1 })]
    public void AnEveryoneEffectOnAUserIsPartOfTheUserEntryState(string name, int[] ints)
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

        var state = Reply(sent, ServerPacketHeader.WiredUserVisualizationStateComposer);

        if (name == "wf_act_set_visibility") {
            Assert.Equal((1, 7, 100, true, 4, 0), (state.Int(), state.Int(), state.Int(), state.Bool(), state.Int(), state.Int()));
        }
        else {
            Assert.Equal((0, 1, 7, 0, 100, 0xFFB700, 1, 0), (state.Int(), state.Int(), state.Int(), state.Int(), state.Int(), state.Int(), state.Int(), state.Int()));
        }

        state.End();
    }

    [Fact]
    public void AnEveryoneHighlightOnABotIsUserEntryStateUntilTheBotIsPickedUp()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        var bot = AddNamedBot(f, 9, "Robbie", 2, 2);
        SelectBots(f);
        OctoberBox<WiredModernAction>(f, 110, "wf_act_set_highlight", 0, 0, native => native with
        {
            OwnedIntParams = [1, 0, 100, 0xFFB700, 1, 0, 1],
            FurniSourceTypes = [100],
            UserSourceTypes = [0, 200]
        });
        f.Fire(land: false);

        var sent = Capture(f.Client);
        f.Room.GetWired().SendVisualizationState(f.Client);
        var state = Reply(sent, ServerPacketHeader.WiredUserVisualizationStateComposer);
        Assert.Equal((0, 1, 9), (state.Int(), state.Int(), state.Int()));

        using (RoomOwnerScope.Enter(f.Room)) {
            f.Room.GetWired().BeforeActorLeaves(bot);
        }

        var after = Capture(f.Client);
        f.Room.GetWired().SendVisualizationState(f.Client);
        Assert.DoesNotContain(after, reply => reply.Header == ServerPacketHeader.WiredUserVisualizationStateComposer);
    }

    [Fact]
    public void SetHighlightOnABotFromASelectorSendsTheUserPacketWithTheBotIndex()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        AddNamedBot(f, 9, "Robbie", 2, 2);
        SelectBots(f);
        OctoberBox<WiredModernAction>(f, 110, "wf_act_set_highlight", 0, 0, native => native with
        {
            OwnedIntParams = [0, 1, 100, 0xFFEA00, 0, 4, 1],
            FurniSourceTypes = [100],
            UserSourceTypes = [0, 200]
        });
        var live = Capture(f.Client);
        f.Fire(land: false);
        var packet = Reply(live, ServerPacketHeader.WiredUserHighlightComposer);
        Assert.Equal((1, 9, 1, 100, 0xFFEA00, 0, 4), (packet.Int(), packet.Int(), packet.Int(), packet.Int(), packet.Int(), packet.Int(), packet.Int()));
        packet.End();
    }

    [Fact]
    public void ASelectedAudienceEffectOnAUserIsNotReplayed()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        OctoberBox<WiredModernAction>(f, 110, "wf_act_set_highlight", 0, 0, native => native with
        {
            OwnedIntParams = [0, 1, 100, 0xFFB700, 1, 0, 1],
            FurniSourceTypes = [100],
            UserSourceTypes = [0, 0]
        });
        f.Fire(land: false);
        var sent = Capture(f.Client);

        f.Room.GetWired().SendVisualizationState(f.Client);

        Assert.Empty(sent);
    }

    [Fact]
    public void ALaterEveryoneEffectReplacesTheStoredUserState()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        var wired = f.Room.GetWired();
        wired.RememberUserHighlight([7], 1, 100, 0xFFB700, 1, 0);
        wired.RememberUserHighlight([7], 3, 50, 0x00FF00, 0, 2);
        var sent = Capture(f.Client);

        wired.SendVisualizationState(f.Client);

        var state = Reply(sent, ServerPacketHeader.WiredUserVisualizationStateComposer);
        Assert.Equal((0, 1, 7, 3, 50, 0x00FF00, 0, 2), (state.Int(), state.Int(), state.Int(), state.Int(), state.Int(), state.Int(), state.Int(), state.Int()));
        state.End();
    }

    [Fact]
    public void AUserVisibilityOfZeroStaysInTheUserState()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        OctoberBox<WiredModernAction>(f, 110, "wf_act_set_visibility", 0, 0, native => native with
        {
            OwnedIntParams = [1, 0, 0, 0, 1],
            FurniSourceTypes = [100],
            UserSourceTypes = [0, 0]
        });
        f.Fire(land: false);
        var sent = Capture(f.Client);

        f.Room.GetWired().SendVisualizationState(f.Client);

        var state = Reply(sent, ServerPacketHeader.WiredUserVisualizationStateComposer);
        Assert.Equal((1, 7, 0, false, 0, 0), (state.Int(), state.Int(), state.Int(), state.Bool(), state.Int(), state.Int()));
        state.End();
    }
}
