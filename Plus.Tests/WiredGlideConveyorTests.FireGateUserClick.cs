using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rewards;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint FireGateUserTrigger = 65, FireGateUserToggle = 66, FireGateOther = 67;

    // A Click User stack toggles a fire gate in the click's own batch, with the other toggled furni.
    [Fact]
    public async Task ClickUserToggleStateOpensAndClosesAFireGateInTheClicksOwnBatch()
    {
        var (f, clicker, target, gate) = FireGateClickRoom(tileClick: false);
        var frames = CaptureFlashFrames(clicker);

        await ClickTheUser(f, clicker, target);
        var opening = BatchWithTheGate(frames);
        f.Advance(800);
        frames.Clear();
        await ClickTheUser(f, clicker, target);
        var closing = BatchWithTheGate(frames);

        Assert.Contains(opening, entry => entry.Id == FireGate && entry.Text == "1");
        Assert.Contains(opening, entry => entry.Id == FireGateOther);
        Assert.Contains(closing, entry => entry.Id == FireGate && entry.Text == "0");
        Assert.Contains(closing, entry => entry.Id == FireGateOther);
        Assert.Equal("0", gate.LegacyDataString);
        Assert.Equal(0, f.Room.GetGameMap().Gates.PendingCount);
    }

    // A tile click toggles a fire gate in the click's own batch, with the other toggled furni.
    [Fact]
    public void ClickTileToggleStateOpensAndClosesAFireGateInTheClicksOwnBatch()
    {
        var (f, clicker, _, gate) = FireGateClickRoom(tileClick: true);
        var frames = CaptureFlashFrames(clicker);

        ClickTheTile(f, clicker);
        f.Advance(200);
        var opening = BatchWithTheGate(frames);
        f.Advance(800);
        frames.Clear();
        ClickTheTile(f, clicker);
        f.Advance(200);
        var closing = BatchWithTheGate(frames);

        Assert.Contains(opening, entry => entry.Id == FireGate && entry.Text == "1");
        Assert.Contains(opening, entry => entry.Id == FireGateOther);
        Assert.Contains(closing, entry => entry.Id == FireGate && entry.Text == "0");
        Assert.Contains(closing, entry => entry.Id == FireGateOther);
        Assert.Equal("0", gate.LegacyDataString);
        Assert.Equal(0, f.Room.GetGameMap().Gates.PendingCount);
    }

    // An open gate with an avatar on it is left out of the batch, stays open and is not closed later.
    [Fact]
    public async Task ClickUserLeavesAnOccupiedGateOutOfTheBatchAndNeverRetries()
    {
        var (f, clicker, target, gate) = FireGateClickRoom(tileClick: false);
        var frames = CaptureFlashFrames(clicker);
        await ClickTheUser(f, clicker, target);
        f.Advance(1000);
        var onGate = f.Walker(3, 6, 7, 6, 8);
        f.Advance(1000);
        frames.Clear();

        await ClickTheUser(f, clicker, target);
        f.Advance(200);
        var batch = BatchWithTheEntry(frames, FireGateOther);

        Assert.DoesNotContain(batch, entry => entry.Id == FireGate);
        Assert.Equal("1", gate.LegacyDataString);

        onGate.MoveTo(6, 10);
        f.Advance(3000);

        Assert.Equal("1", gate.LegacyDataString);
        Assert.DoesNotContain(frames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer).SelectMany(ReadBatch),
            entry => entry.Id == FireGate);
        Assert.Equal(0, f.Room.GetGameMap().Gates.PendingCount);
    }

    // A tile click from an avatar that still stands on the open gate leaves the gate open.
    [Fact]
    public void ClickTileLeavesTheGateOpenWhileTheClickerStandsOnIt()
    {
        var (f, clicker, _, gate) = FireGateClickRoom(tileClick: true);
        var frames = CaptureFlashFrames(clicker);
        ClickTheTile(f, clicker);
        f.Advance(1000);
        clicker.MoveTo(6, 8);
        f.Advance(1000);
        Assert.Equal((6, 8), (clicker.X, clicker.Y));
        frames.Clear();

        ClickTheTile(f, clicker);
        f.Advance(3000);

        Assert.Equal("1", gate.LegacyDataString);
        Assert.DoesNotContain(frames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer).SelectMany(ReadBatch),
            entry => entry.Id == FireGate);
    }

    private static (Fixture Fixture, RoomUser Clicker, RoomUser Target, Item Gate) FireGateClickRoom(bool tileClick)
    {
        var f = new Fixture(
        [
            (FireGateSubject, 9, 9, 0.0, 0, "bc_tile_1", null),
            (FireGate, 6, 8, 0.0, 0, "wf_firegate", null),
            (FireGateOther, 7, 8, 0.0, 0, "wf_colortile", null),
            (FireGateUserTrigger, 4, 12, 0.0, 0, tileClick ? "wf_trg_click_tile" : "wf_trg_click_user",
                tileClick ? ObservedClick(FireGateSubject) : JsonSerializer.Serialize(new WiredConfiguration { IntParams = [0, 0] })),
            (FireGateUserToggle, 4, 12, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [FireGate, FireGateOther], 100))
        ], live: true);
        var gate = f.Items[FireGate];
        gate.Definition.InteractionType = InteractionType.Gate;
        gate.Definition.Modes = 2;
        gate.Definition.Walkable = false;
        gate.Definition.Height = 0;
        gate.LegacyDataString = "0";
        var clicker = f.Walker(1, 4, 10, 4, 11);
        var target = f.Walker(2, 10, 10, 10, 11);
        f.Advance(1000);

        return (f, clicker, target, gate);
    }

    private static Task ClickTheUser(Fixture f, RoomUser clicker, RoomUser target) =>
        new ClickUserEvent().Parse(f.Room, clicker.GetClient()!, ClickPacket(target.VirtualId));

    private static void ClickTheTile(Fixture f, RoomUser clicker)
    {
        var service = new RoomAvatarActionService(TimeProvider.System, NoOp<IQuestManager>.Create(), NoOp<IRewardTrackManager>.Create());
        service.Move(clicker.GetClient()!, 9, 9);
    }

    private static (uint Id, string Text)[] BatchWithTheEntry(List<(uint Header, byte[] Payload)> frames, uint id) =>
        frames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer).Select(ReadBatch)
            .FirstOrDefault(batch => batch.Any(entry => entry.Id == id)) ?? [];
}
