using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint FireGateSubject = 60, FireGateTrigger = 61, FireGateToggle = 62, FireGate = 63, FireGateTile = 64;

    // A click on a Toggle State stack opens and closes a fire gate in the same batch as the other toggled furni.
    [Fact]
    public async Task ClickToggleStateOpensAndClosesAFireGateInTheClicksOwnBatch()
    {
        var f = new Fixture(
        [
            (FireGateSubject, 9, 9, 0.0, 0, "bc_tile_1", null),
            (FireGate, 6, 8, 0.0, 0, "wf_firegate", null),
            (FireGateTile, 7, 8, 0.0, 0, "wf_colortile", null),
            (FireGateTrigger, 4, 12, 0.0, 0, "wf_trg_click_furni", ObservedClick(FireGateSubject)),
            (FireGateToggle, 4, 12, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [FireGate, FireGateTile], 100))
        ], live: true);
        var gate = f.Items[FireGate];
        gate.Definition.InteractionType = InteractionType.Gate;
        gate.Definition.Modes = 2;
        gate.Definition.Walkable = false;
        gate.Definition.Height = 0;
        gate.LegacyDataString = "0";
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.Advance(1000);
        var frames = CaptureFlashFrames(rider);

        await new ClickFurniEvent(new FurnitureUseService(null!, null!)).Parse(f.Room, rider.GetClient()!, ClickPacket((int)FireGateSubject, 0));
        var opening = BatchWithTheGate(frames);
        f.Advance(1000);
        frames.Clear();
        await new ClickFurniEvent(new FurnitureUseService(null!, null!)).Parse(f.Room, rider.GetClient()!, ClickPacket((int)FireGateSubject, 0));
        var closing = BatchWithTheGate(frames);

        Assert.Contains(opening, entry => entry.Id == FireGate && entry.Text == "1");
        Assert.Contains(opening, entry => entry.Id == FireGateTile);
        Assert.Contains(closing, entry => entry.Id == FireGate && entry.Text == "0");
        Assert.Contains(closing, entry => entry.Id == FireGateTile);
        Assert.Equal("0", gate.LegacyDataString);
        Assert.Equal(0, f.Room.GetGameMap().Gates.PendingCount);
    }

    private static (uint Id, string Text)[] BatchWithTheGate(List<(uint Header, byte[] Payload)> frames) =>
        frames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer).Select(ReadBatch)
            .FirstOrDefault(batch => batch.Any(entry => entry.Id == FireGate)) ?? [];
}
