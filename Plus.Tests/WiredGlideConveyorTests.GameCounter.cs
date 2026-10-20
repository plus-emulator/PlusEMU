using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    [Fact]
    public void WiredStartAndStopOfAGameCounterRaiseGameStartsAndGameEndsOnce()
    {
        // Observed on AIR WIN63-202610131318-860371703: control_clock Start on wf_game_upcounter1 fires Game Starts in the Start tick, Stop fires
        // Game Ends in the Stop tick, and a give score in between reaches Score Achieved.
        var f = ClockFixture("wf_game_upcounter1", "wf_act_control_clock", [0, 100]);
        f.Walker(1, 4, 10, 4, 11);
        var events = new List<WiredEventKind>();
        f.Engine.ObserveEvent = (evt, _) =>
        {
            if (evt.Kind is WiredEventKind.GameStart or WiredEventKind.GameEnd) {
                events.Add(evt.Kind);
            }
        };
        var counter = f.Items[CounterId];
        Assert.False(WiredGameState.GameRunning(f.Room));

        f.Advance(250);
        Assert.True(f.Engine.Remove(21));
        f.Advance(1350);

        Assert.True(Counters(f).IsRunning(counter));
        Assert.True(WiredGameState.GameRunning(f.Room));
        Assert.Equal([WiredEventKind.GameStart], events);
        var scores = new List<WiredRuntimeEvent>();
        Assert.True(WiredGameState.For(f.Room).GiveScore(f.Room, 22, 1, Team.Red, 1, null, scores.Add));
        Assert.Equal((1, 0L, 1L), (scores.Single().Team, scores.Single().PreviousValue, scores.Single().Value));

        Assert.True(Counters(f).Control(counter, 1, 1600, WiredClockOrigin.ModernWired));
        f.Advance(500);

        Assert.False(WiredGameState.GameRunning(f.Room));
        Assert.Equal([WiredEventKind.GameStart, WiredEventKind.GameEnd], events);
        Assert.False(WiredGameState.For(f.Room).GiveScore(f.Room, 22, 1, Team.Red, 1, null, scores.Add));
    }

    [Fact]
    public void WiredStartOfARunningGameCounterRestartsItsGameAndElapsedTime()
    {
        // Observed on AIR WIN63-202610151745-318700621: another Start resets the running counter and fires both game triggers.
        var f = ClockFixture("wf_game_upcounter1", "wf_act_control_clock", [0, 100]);
        f.Walker(1, 4, 10, 4, 11);
        var events = new List<WiredEventKind>();
        f.Engine.ObserveEvent = (evt, _) =>
        {
            if (evt.Kind is WiredEventKind.GameStart or WiredEventKind.GameEnd) {
                events.Add(evt.Kind);
            }
        };
        f.Advance(250);
        Assert.True(f.Engine.Remove(21));
        f.Advance(1350);
        var counter = f.Items[CounterId];
        var clocks = Counters(f);
        f.Advance(2000);
        Assert.True(clocks.ReadMilliseconds(counter) > 0);
        Assert.True(WiredGameState.For(f.Room).GiveScore(f.Room, 22, 1, Team.Red, 7, null, _ => { }));
        Assert.Equal(7, f.Room.GetGameManager().Points[(int)Team.Red]);

        f.Owned(() => Assert.True(clocks.Control(counter, 0, 3600, WiredClockOrigin.ModernWired)));
        f.Advance(50);

        Assert.Equal([WiredEventKind.GameStart, WiredEventKind.GameEnd, WiredEventKind.GameStart], events);
        Assert.True(clocks.IsRunning(counter));
        Assert.True(WiredGameState.GameRunning(f.Room));
        Assert.Equal(0, clocks.ReadMilliseconds(counter));
        Assert.Equal(0, f.Room.GetGameManager().Points[(int)Team.Red]);
        f.Advance(1000);
        Assert.Equal("1", counter.LegacyDataString);
        f.Owned(() => Assert.True(clocks.Control(counter, 1, 4600, WiredClockOrigin.ModernWired)));
        f.Owned(() => Assert.True(clocks.Control(counter, 1, 4600, WiredClockOrigin.ModernWired)));
        f.Advance(50);
        Assert.Equal(2, events.Count(kind => kind == WiredEventKind.GameEnd));
        Assert.False(clocks.IsRunning(counter));
    }

    [Theory]
    [InlineData("wf_game_upcounter1")]
    [InlineData("wf_game_upcounter2")]
    public void RepeatedGameCounterStartPublishesZeroAndReschedulesItsFirstTick(string name)
    {
        var item = ClockItem(name, 204);
        var transitions = new List<WiredClockTransition>();
        var clocks = new WiredCounterController(gameTransition: transitions.Add);
        clocks.Attach(item);
        Assert.True(clocks.Control(item, 0, 0, WiredClockOrigin.ModernWired));

        for (var now = 500; now <= 4000; now += 500) {
            clocks.Poll(now);
        }

        Assert.Equal("4", item.LegacyDataString);
        Assert.True(clocks.Control(item, 0, 4200, WiredClockOrigin.ModernWired));
        Assert.Equal([WiredClockReason.Start, WiredClockReason.Stop, WiredClockReason.Start], transitions.Select(t => t.Reason));
        var zero = Assert.Single(clocks.TakeChanges());
        Assert.Equal((WiredCounterPublication.BatchData, "0"), (zero.Publication, zero.Text));
        Assert.Equal(0, clocks.ReadMilliseconds(item));
        clocks.Poll(4500);
        Assert.Equal("0", item.LegacyDataString);
        Assert.Equal("1", Assert.Single(clocks.Poll(5000), change => change.DisplayChanged).Text);
    }

    [Fact]
    public void DeniedRepeatedGameCounterStartKeepsTheCurrentRoundRunning()
    {
        var item = ClockItem("wf_game_upcounter1", 205);
        var allowed = true;
        var transitions = new List<WiredClockTransition>();
        var clocks = new WiredCounterController(gameTransition: transitions.Add, canBegin: (_, _, _) => allowed);
        clocks.Attach(item);
        Assert.True(clocks.Control(item, 0, 0, WiredClockOrigin.ModernWired));
        clocks.Poll(500);
        clocks.Poll(1000);
        allowed = false;

        Assert.False(clocks.Control(item, 0, 1200, WiredClockOrigin.ModernWired));
        Assert.True(clocks.IsRunning(item));
        Assert.Equal(1000, clocks.ReadMilliseconds(item));
        Assert.Equal([WiredClockReason.Start], transitions.Select(t => t.Reason));
    }

    [Fact]
    public void PausingAGameCounterDoesNotEndTheGameAndPlainCountersNeverStartOne()
    {
        var starts = new List<WiredClockTransition>();
        var game = ClockItem("wf_game_upcounter1", 201);
        var plain = ClockItem("wf_upcounter1", 202);
        var clocks = new WiredCounterController(gameTransition: starts.Add);
        clocks.Attach(game);
        clocks.Attach(plain);

        Assert.True(clocks.Control(plain, 0, 0, WiredClockOrigin.ModernWired));
        Assert.True(clocks.Control(plain, 1, 0, WiredClockOrigin.ModernWired));
        Assert.Empty(starts);
        Assert.False(clocks.HasRunningGameCounter);

        Assert.True(clocks.Control(game, 0, 0, WiredClockOrigin.ModernWired));
        Assert.True(clocks.HasRunningGameCounter);
        Assert.True(clocks.Control(game, 3, 0, WiredClockOrigin.ModernWired));
        Assert.True(clocks.Control(game, 4, 0, WiredClockOrigin.ModernWired));
        Assert.True(clocks.Control(game, 1, 0, WiredClockOrigin.ModernWired));

        Assert.Equal([WiredClockReason.Start, WiredClockReason.Stop], starts.Select(transition => transition.Reason));
        Assert.False(clocks.HasRunningGameCounter);
    }

    [Fact]
    public void WiredStartOfAStoppedNonZeroGameCounterBatchesItsZeroAndCountsFromOne()
    {
        // Observed on AIR WIN63-202610131318-860371703 (wf_game_upcounter1, 8/8): a Start on a stopped non-zero counter puts its "0" in the
        // Start's own batch and counts from 1; a Start on a counter already at 0 has no counter entry.
        var item = ClockItem("wf_game_upcounter1", 203);
        var clocks = new WiredCounterController();
        clocks.Attach(item);
        Assert.True(clocks.Control(item, 0, 0, WiredClockOrigin.ModernWired));
        Assert.Empty(clocks.TakeChanges());

        for (var now = 500; now <= 4000; now += 500) {
            clocks.Poll(now);
        }

        Assert.Equal("4", item.LegacyDataString);
        Assert.True(clocks.Control(item, 1, 4000, WiredClockOrigin.ModernWired));
        clocks.TakeChanges();

        Assert.True(clocks.Control(item, 0, 6000, WiredClockOrigin.ModernWired));
        var zero = Assert.Single(clocks.TakeChanges());
        Assert.Equal((WiredCounterPublication.BatchData, "0"), (zero.Publication, zero.Text));
        Assert.Equal(0, clocks.ReadMilliseconds(item));
        clocks.Poll(6500);
        var first = clocks.Poll(7000).Single(change => change.DisplayChanged);
        Assert.Equal("1", first.Text);
    }

    [Fact]
    public void GameCountersKeepCountingPastNinetyNineMinutesWithoutEndingTheGame()
    {
        // Observed on AIR WIN63-202610151745-318700621: a game counter set to 99:59.5 and started shows 6000, 6001 and 6002, and the other
        // started game counters keep counting. No upper limit is visible.
        var transitions = new List<WiredClockTransition>();
        var clocks = new WiredCounterController(gameTransition: transitions.Add);
        var high = ClockItem("wf_game_upcounter1", 206);
        var other = ClockItem("wf_game_upcounter1", 207);
        clocks.Attach(high);
        clocks.Attach(other);
        Assert.True(clocks.Adjust(high, 0, 99, 119, WiredClockOrigin.ModernWired));
        Assert.Equal("5999", high.LegacyDataString);
        Assert.True(clocks.Control(high, 0, 0, WiredClockOrigin.ModernWired));
        Assert.True(clocks.Control(other, 0, 0, WiredClockOrigin.ModernWired));

        for (var now = 500; now <= 6000; now += 500) {
            clocks.Poll(now);
        }

        Assert.Equal("6005", high.LegacyDataString);
        Assert.Equal("6", other.LegacyDataString);
        Assert.True(clocks.IsRunning(high));
        Assert.True(clocks.IsRunning(other));
        Assert.All(transitions, transition => Assert.Equal(WiredClockReason.Start, transition.Reason));
        Assert.True(clocks.Adjust(high, 0, 0, 10, WiredClockOrigin.ModernWired));
        Assert.Equal("6010", high.LegacyDataString);
    }

    [Fact]
    public void ARestoredCounterPastNinetyNineMinutesKeepsItsValue()
    {
        var item = ClockItem("wf_upcounter1", 208);
        item.LegacyDataString = "6002";
        var clocks = new WiredCounterController();
        clocks.Attach(item);

        Assert.Equal(6002000, clocks.ReadMilliseconds(item));
    }
}
