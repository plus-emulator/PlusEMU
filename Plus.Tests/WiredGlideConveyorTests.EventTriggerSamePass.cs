using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// A stack whose trigger watches a score, a furni state or a clock flashes in the same millisecond as the change it watches, also
// when a click or the clock itself made the change.
public sealed partial class WiredGlideConveyorTests
{
    private const uint SamePassTileId = 61;
    private const uint SamePassWatchedId = 66;
    private const uint SamePassTriggerId = 64;
    private const uint SamePassEffectId = 65;

    [Fact]
    public void ScoreAchievedFlashesInTheTickOfAGiveScoreFiredByAClick()
    {
        var f = SamePassRoom(
            (62, 4, 12, 0.0, 0, "wf_trg_click_furni", ObservedClick(SamePassTileId)),
            (63, 4, 12, 0.65, 0, "wf_act_give_score_tm", ObservedNativeDefault("wf_act_give_score_tm", [1, 0, 1])),
            (SamePassTriggerId, 6, 12, 0.0, 0, "wf_trg_score_achieved", ObservedNativeDefault("wf_trg_score_achieved", [1, 1])));
        var clicker = f.Walker(1, 4, 10, 4, 11);
        StartGameCounter(f);
        var sent = CaptureTimedFrames(f, clicker);

        new FurnitureUseService(null!, null!).Click(f.Room, clicker.GetClient()!, new(SamePassTileId, false));
        f.Advance(1000);

        Assert.Equal(1, f.Room.GetGameManager().Points[1]);
        var scoreFlash = FirstFlashTime(sent, 63);
        Assert.Equal(scoreFlash, FirstFlashTime(sent, SamePassTriggerId));
        Assert.Equal(scoreFlash, FirstFlashTime(sent, SamePassEffectId));
    }

    [Fact]
    public void StuffStateFlashesInTheBatchOfAToggleFiredByAClick()
    {
        var f = SamePassRoom(
            (62, 4, 12, 0.0, 0, "wf_trg_click_furni", ObservedClick(SamePassTileId)),
            (63, 4, 12, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [SamePassWatchedId], 100)),
            (SamePassTriggerId, 6, 12, 0.0, 0, "wf_trg_stuff_state", SelectedConfig([0, 100], SamePassWatchedId)));
        var clicker = f.Walker(1, 4, 10, 4, 11);
        var sent = CaptureTimedFrames(f, clicker);

        new FurnitureUseService(null!, null!).Click(f.Room, clicker.GetClient()!, new(SamePassTileId, false));
        f.Advance(1000);

        Assert.Equal("1", f.Items[SamePassWatchedId].LegacyDataString);
        var batch = Assert.Single(sent, frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer
            && ReadBatch((frame.Header, frame.Payload)).Any(entry => entry.Id == SamePassWatchedId));
        var ids = ReadBatch((batch.Header, batch.Payload)).Select(entry => entry.Id).ToArray();
        Assert.Contains(SamePassTriggerId, ids);
        Assert.Contains(SamePassEffectId, ids);
    }

    [Fact]
    public void StuffStateFlashesInTheMillisecondOfAUsersUse()
    {
        var f = SamePassRoom(
            (SamePassTriggerId, 6, 12, 0.0, 0, "wf_trg_stuff_state", SelectedConfig([0, 100], SamePassWatchedId)));
        var user = f.Walker(1, 4, 10, 4, 11);
        f.Room.OwnerName = "user1";
        f.Room.Type = "private";
        f.Items[SamePassWatchedId].Attach(f.Room, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);
        f.Advance(500);
        var sent = CaptureTimedFrames(f, user);
        var usedAt = f.Engine.NowMilliseconds;

        new FurnitureUseService(null!, TestItemRuntime.Quests).Use(f.Room, user.GetClient()!, new(SamePassWatchedId, 0));
        f.Advance(1000);

        var state = Assert.Single(sent, frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer);
        Assert.Equal(usedAt, state.At);
        Assert.Equal(usedAt, FirstFlashTime(sent, SamePassTriggerId));
        Assert.Equal(usedAt, FirstFlashTime(sent, SamePassEffectId));
        Assert.True(sent.IndexOf(state) < sent.FindIndex(frame => IsFlashOf(frame, SamePassTriggerId)));
    }

    // A Stuff State stack that switches the furni it watches runs again in the same pass, and the depth limit still ends the chain.
    [Fact]
    public void StuffStateStackThatSwitchesItsOwnFurniStopsAtTheDepthLimit()
    {
        var f = SamePassRoom(
            (SamePassTriggerId, 6, 12, 0.0, 0, "wf_trg_stuff_state", SelectedConfig([0, 100], SamePassWatchedId)),
            (68, 6, 12, 1.3, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [SamePassWatchedId], 100)));
        var user = f.Walker(1, 4, 10, 4, 11);
        f.Room.OwnerName = "user1";
        f.Room.Type = "private";
        f.Items[SamePassWatchedId].Attach(f.Room, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);
        f.Advance(500);
        var before = f.Items[67].StateGeneration;
        var limits = new List<Plus.HabboHotel.Items.Wired.WiredEngineLimit>();
        f.Engine.LimitReached = (limit, _) => limits.Add(limit);

        new FurnitureUseService(null!, TestItemRuntime.Quests).Use(f.Room, user.GetClient()!, new(SamePassWatchedId, 0));
        var sameMillisecond = f.Items[67].StateGeneration - before;
        var limitsInTheUse = limits.ToArray();
        f.Advance(2500);

        Assert.True(sameMillisecond > 2, "the Stuff State stack did not run again in the millisecond of the use");
        Assert.Contains(Plus.HabboHotel.Items.Wired.WiredEngineLimit.Depth, limitsInTheUse);
        Assert.Equal(sameMillisecond, f.Items[67].StateGeneration - before);
        Assert.Equal(0, f.Engine.ReadStats().Pending);
    }

    [Fact]
    public void ClockCounterFlashesInTheTickOfTheClockValue()
    {
        var f = SamePassRoom(
            (SamePassTriggerId, 6, 12, 0.0, 0, "wf_trg_clock_counter", SelectedConfig([0, 2, 100], CounterId)));
        var rider = f.Walker(1, 4, 10, 4, 11);
        var sent = CaptureTimedFrames(f, rider);
        StartGameCounter(f);

        f.Advance(2000);

        var reached = sent.First(frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer
            && ReadSingle((frame.Header, frame.Payload)) == (CounterId, "1"));
        Assert.Equal(reached.At, FirstFlashTime(sent, SamePassTriggerId));
        Assert.Equal(reached.At, FirstFlashTime(sent, SamePassEffectId));
    }

    // A watching stack on (6, 12) whose effect toggles a tile of its own, so its flash shows when the stack ran.
    private static Fixture SamePassRoom(params (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[] stacks)
    {
        var f = new Fixture(
        [
            (CounterId, 8, 12, 0.0, 0, "wf_game_upcounter1", null),
            (SamePassTileId, 9, 9, 0.0, 0, "wf_colortile", null),
            (SamePassWatchedId, 10, 10, 0.0, 0, "wf_colortile", null),
            (67, 11, 10, 0.0, 0, "wf_colortile", null),
            (SamePassEffectId, 6, 12, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [67], 100)),
            .. stacks
        ], live: true);
        f.Room.GetWired().AttachRoomItem(f.Items[CounterId]);

        return f;
    }

    private static string SelectedConfig(int[] parameters, uint selected) => JsonSerializer.Serialize(new WiredConfiguration
    {
        IntParams = [.. parameters],
        SelectedItems = [selected]
    });

    private static void StartGameCounter(Fixture f)
    {
        f.Owned(() => Assert.True(Counters(f).Control(f.Items[CounterId], 0, f.Engine.NowMilliseconds, WiredClockOrigin.ModernWired)));
        f.Advance(50);
        Assert.True(WiredGameState.GameRunning(f.Room));
    }

    private static List<(long At, uint Header, byte[] Payload)> CaptureTimedFrames(Fixture f, RoomUser user)
    {
        var frames = new List<(long, uint, byte[])>();
        user.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            frames.Add((f.Engine.NowMilliseconds, BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]));

            return false;
        };

        return frames;
    }

    private static bool IsFlashOf((long At, uint Header, byte[] Payload) frame, uint box) =>
        frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer && ReadBatch((frame.Header, frame.Payload)).Any(entry => entry.Id == box);

    private static long FirstFlashTime(List<(long At, uint Header, byte[] Payload)> frames, uint box)
    {
        var index = frames.FindIndex(frame => IsFlashOf(frame, box));
        Assert.True(index >= 0, $"box {box} never flashed");

        return frames[index].At;
    }
}
