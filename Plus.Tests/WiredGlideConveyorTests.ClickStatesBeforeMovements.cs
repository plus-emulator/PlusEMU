using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint ClickToggledTile = 50, ClickStateTrigger = 51, ClickStateToggle = 52, ClickStateRotate = 53;

    // A click on a stack that toggles furni and turns the user sends one ObjectsDataUpdate with the box flashes and the new
    // furni states, then one WiredMovements with the turn. The effects run in a random order, so both orders must give the same
    // packets.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OneClickSendsTheToggledStatesWithTheFlashesBeforeTheTurn(bool swapEffects)
    {
        var f = new Fixture(
        [
            (ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null),
            (ClickToggledTile, 6, 8, 0.0, 0, "wf_colortile", null),
            (ClickStateTrigger, 4, 12, 0.0, 0, "wf_trg_click_furni", ObservedClick(ObservedSubjectId)),
            (ClickStateToggle, 4, 12, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [ClickToggledTile], 100)),
            (ClickStateRotate, 4, 12, 1.3, 0, "wf_act_move_rotate_user", ClockwiseTurnOfTheUser())
        ], live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.SetEngineCallback("_effectOrderRandom", new EffectSwapRandom(swapEffects));
        f.Advance(1000);
        var frames = CaptureFlashFrames(rider);

        await new ClickFurniEvent(new FurnitureUseService(null!, null!)).Parse(f.Room, rider.GetClient()!,
            ClickPacket((int)ObservedSubjectId, 0));

        // Sent with the click: the states and flashes first, then the turn.
        Assert.Equal([ServerPacketHeader.ObjectsDataUpdateComposer, ServerPacketHeader.WiredMovementsComposer], DataAndMovementHeaders(frames));
        var batch = frames.Single(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
        Assert.Equal(new uint[] { ClickToggledTile, ClickStateTrigger, ClickStateToggle, ClickStateRotate },
            ReadBatch(batch).Select(entry => entry.Id).Order());
        Assert.Equal("1", ReadBatch(batch).Single(entry => entry.Id == ClickToggledTile).Text);

        f.Advance(1000);

        Assert.Equal([ServerPacketHeader.ObjectsDataUpdateComposer, ServerPacketHeader.WiredMovementsComposer], DataAndMovementHeaders(frames));
        Assert.Equal("1", f.Items[ClickToggledTile].LegacyDataString);
    }

    private static uint[] DataAndMovementHeaders(List<(uint Header, byte[] Payload)> frames) =>
    [
        .. frames.Select(frame => frame.Header)
            .Where(header => header is ServerPacketHeader.ObjectsDataUpdateComposer or ServerPacketHeader.WiredMovementsComposer)
    ];

    private static string ClockwiseTurnOfTheUser()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate_user", out var descriptor));

        return "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [-1, 9],
            FurniSourceTypes = [],
            UserSourceTypes = [0],
            Delay = 0
        });
    }

    // Swaps the two effects of the firing (or keeps their order) instead of picking a random order.
    private sealed class EffectSwapRandom(bool swap) : Random
    {
        public override int Next(int minValue, int maxValue) => minValue;
        public override int Next(int maxValue) => swap ? 0 : maxValue - 1;
    }
}
