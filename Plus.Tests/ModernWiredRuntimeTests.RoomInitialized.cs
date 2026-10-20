using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Xunit;

namespace Plus.Tests;

/// <summary>Room Initialized stacks run while the room loads, before anyone is in it.</summary>
public partial class ModernWiredRuntimeTests
{
    // Observed on AIR WIN63-202610201032-107964540: the visual state sent on room entry already holds the effects of the Room
    // Initialized stacks, so they ran during the load and not in a later room tick.
    [Fact]
    public void RoomInitializedStacksStoreTheirEveryoneEffectsBeforeTheFirstRoomTick()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        RoomInitializedStack(f, audience: 1, targetUsers: 0);
        var sent = Capture(f.Client);

        Assert.True(f.Room.GetWired().RoomInitialized());
        // The fixture user is already in the room and sees the live packet; a real load has nobody in it yet.
        sent.Clear();
        f.Room.GetWired().SendVisualizationState(f.Client);

        var state = Reply(sent, ServerPacketHeader.WiredFurniVisualizationStateComposer);
        Assert.Equal((0, 1, 1, 1, 100, 0x123456), (state.Int(), state.Int(), state.Int(), state.Int(), state.Int(), state.Int()));
    }

    // Observed on AIR WIN63-202610201032-107964540: Room Initialized has no triggering user, so an effect for the selected users
    // sends nothing and stores nothing.
    [Fact]
    public void RoomInitializedHasNoTriggeringUserForAnEffectAimedAtSelectedUsers()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        RoomInitializedStack(f, audience: 0, targetUsers: 0);
        var sent = Capture(f.Client);

        f.Room.GetWired().RoomInitialized();
        f.Engine.OnFastCycle();
        f.Room.GetWired().SendVisualizationState(f.Client);

        Assert.Empty(sent);
    }

    // Observed on AIR WIN63-202610201032-107964540: an everyone effect that targets the triggering user does nothing.
    [Fact]
    public void RoomInitializedHasNoTriggeringUserForAnEffectTargetingUsers()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        RoomInitializedStack(f, audience: 1, targetUsers: 1);
        var sent = Capture(f.Client);

        f.Room.GetWired().RoomInitialized();
        f.Engine.OnFastCycle();
        f.Room.GetWired().SendVisualizationState(f.Client);

        Assert.Empty(sent);
    }

    private static void RoomInitializedStack(TeleportFixture f, int audience, int targetUsers)
    {
        OctoberBox<WiredModernTrigger>(f, 120, "wf_trg_room_initializes", 2, 2, native => native);
        // [audience, type, strength, colour, animation, easing, target users]: a 100 percent glow of colour 0x123456 on furni 1.
        OctoberBox<WiredModernAction>(f, 121, "wf_act_set_highlight", 2, 2, native => native with
        {
            OwnedIntParams = [audience, 1, 100, 0x123456, 0, 0, targetUsers],
            PrimaryItems = [new(1, false)],
            FurniSourceTypes = [100],
            UserSourceTypes = [0, 0]
        });
    }
}
