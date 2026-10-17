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

        f.Advance(1600);

        Assert.True(Counters(f).IsRunning(counter));
        Assert.True(WiredGameState.GameRunning(f.Room));
        Assert.Equal([WiredEventKind.GameStart], events);
        var scores = new List<WiredRuntimeEvent>();
        Assert.True(WiredGameState.For(f.Room).GiveScore(f.Room, 22, 1, Team.Red, 1, null, scores.Add));
        Assert.Equal((1, 0L, 1L), (scores.Single().Team, scores.Single().PreviousValue, scores.Single().Value));

        // Drop the periodic Start so the Stop is the last word.
        Assert.True(f.Engine.Remove(21));
        Assert.True(Counters(f).Control(counter, 1, 1600, WiredClockOrigin.ModernWired));
        f.Advance(500);

        Assert.False(WiredGameState.GameRunning(f.Room));
        Assert.Equal([WiredEventKind.GameStart, WiredEventKind.GameEnd], events);
        Assert.False(WiredGameState.For(f.Room).GiveScore(f.Room, 22, 1, Team.Red, 1, null, scores.Add));
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
}
