using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

public sealed class WiredGameTimerPortTests
{
    [Theory]
    [InlineData("bb_counter", InteractionType.Banzaicounter)]
    [InlineData("fball_counter", InteractionType.Counter)]
    [InlineData("es_counter", InteractionType.Freezetimer)]
    public void NativeGameTimersCountDownWholeSecondsAndAdjustWithoutTheWiredCounterCeiling(string name, InteractionType type)
    {
        var item = Timer(name, type, "10");
        var clocks = new WiredCounterController();
        Assert.True(clocks.Attach(item));
        Assert.True(clocks.Control(item, 0, 0));
        clocks.Poll(500);
        Assert.Equal(10000, clocks.ReadMilliseconds(item));
        clocks.Poll(1000);
        Assert.Equal(9000, clocks.ReadMilliseconds(item));
        Assert.True(clocks.Adjust(item, 2, 99, 119));
        Assert.Equal(5_999_000, clocks.ReadMilliseconds(item));
        Assert.True(clocks.Adjust(item, 0, 1, 1));
        Assert.Equal(6_059_000, clocks.ReadMilliseconds(item));
        Assert.True(clocks.Control(item, 3, 1500));
        clocks.Poll(2000);
        Assert.Equal(6_059_000, clocks.ReadMilliseconds(item));
        Assert.True(clocks.Control(item, 4, 2000));
        clocks.Poll(3000);
        Assert.Equal(6_058_000, clocks.ReadMilliseconds(item));
        Assert.True(clocks.Control(item, 2, 3000));
        Assert.False(clocks.IsRunning(item));
        Assert.Equal(10000, clocks.ReadMilliseconds(item));
    }

    [Theory]
    [InlineData("bb_counter", InteractionType.Banzaicounter)]
    [InlineData("fball_counter", InteractionType.Counter)]
    [InlineData("es_counter", InteractionType.Freezetimer)]
    public void NativeGameTimersRestartAtTheirSavedDurationAfterRunningOut(string name, InteractionType type)
    {
        var item = Timer(name, type, "2");
        var clocks = new WiredCounterController();
        Assert.True(clocks.Attach(item));
        Assert.True(clocks.Control(item, 0, 0));
        clocks.Poll(1000);
        clocks.Poll(2000);
        Assert.False(clocks.IsRunning(item));
        Assert.Equal(0, clocks.ReadMilliseconds(item));

        Assert.True(clocks.Control(item, 0, 3000));

        Assert.True(clocks.IsRunning(item));
        Assert.Equal(2000, clocks.ReadMilliseconds(item));
        Assert.Equal("2", item.LegacyDataString);
    }

    [Fact]
    public void ResetReturnsANativeGameTimerToItsSavedDuration()
    {
        var item = Timer("bb_counter", InteractionType.Banzaicounter, "180");
        var clocks = new WiredCounterController();
        Assert.True(clocks.Attach(item));
        clocks.Control(item, 0, 0);
        clocks.Poll(1000);
        clocks.Poll(2000);
        Assert.Equal(178000, clocks.ReadMilliseconds(item));

        Assert.True(clocks.Control(item, 2, 2500));

        Assert.False(clocks.IsRunning(item));
        Assert.Equal(180000, clocks.ReadMilliseconds(item));
        Assert.Equal("180", item.LegacyDataString);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("")]
    public void NativeGameTimersWithoutASavedDurationUseThirtySeconds(string state)
    {
        var item = Timer("bb_counter", InteractionType.Banzaicounter, state);
        var clocks = new WiredCounterController();
        Assert.True(clocks.Attach(item));

        Assert.True(clocks.Control(item, 0, 0));
        Assert.Equal(30000, clocks.ReadMilliseconds(item));
        Assert.True(clocks.Control(item, 2, 500));
        Assert.Equal(30000, clocks.ReadMilliseconds(item));
    }

    [Fact]
    public void EndingTheGameStopsNativeTimersWithoutDiscardingTheirRemainingValue()
    {
        var item = Timer("bb_counter", InteractionType.Banzaicounter, "10");
        var clocks = new WiredCounterController();
        Assert.True(clocks.Attach(item));
        clocks.Control(item, 0, 0);
        clocks.Poll(1000);
        clocks.OnGameEnded();
        clocks.Poll(2000);
        Assert.False(clocks.IsRunning(item));
        Assert.Equal(9000, clocks.ReadMilliseconds(item));
    }

    // The host picks a timer's duration with the reset button, a new timer starts at 30 seconds, and the saved duration survives
    // a run, a room unload and a Wired reload.
    private static readonly int[] Cycle = [60, 120, 180, 300, 600, 30];

    [Theory]
    [InlineData("bb_counter", InteractionType.Banzaicounter)]
    [InlineData("fball_counter", InteractionType.Counter)]
    [InlineData("es_counter", InteractionType.Freezetimer)]
    public void ResetOnAnIdleTimerCyclesTheSavedDuration(string name, InteractionType type)
    {
        var item = Timer(name, type, "");
        var clocks = new WiredCounterController();
        Assert.True(clocks.Attach(item));
        Assert.Equal(30000, clocks.ReadMilliseconds(item));

        foreach (var seconds in Cycle) {
            Assert.True(clocks.Use(item, 2, 0));
            Assert.Equal(seconds * 1000L, clocks.ReadMilliseconds(item));
            Assert.Equal(seconds.ToString(), item.LegacyDataString);
            Assert.Equal(seconds.ToString(), WiredCounterController.StoredState(item));
        }
    }

    [Fact]
    public void ResetAfterARunReturnsToTheSavedDurationWithoutCycling()
    {
        var item = Timer("bb_counter", InteractionType.Banzaicounter, "120");
        var clocks = new WiredCounterController();
        clocks.Attach(item);
        clocks.Use(item, 1, 0);
        clocks.Poll(1000);
        clocks.Poll(2000);
        clocks.Use(item, 1, 2500);
        Assert.False(clocks.IsRunning(item));
        Assert.Equal(118000, clocks.ReadMilliseconds(item));

        clocks.Use(item, 2, 3000);
        Assert.Equal(120000, clocks.ReadMilliseconds(item));
        clocks.Use(item, 2, 3500);
        Assert.Equal(180000, clocks.ReadMilliseconds(item));
    }

    [Fact]
    public void ResetAfterTheCountdownEndedReturnsToTheSavedDurationWithoutCycling()
    {
        var item = Timer("es_counter", InteractionType.Freezetimer, "2");
        var clocks = new WiredCounterController();
        clocks.Attach(item);
        clocks.Use(item, 1, 0);
        clocks.Poll(1000);
        clocks.Poll(2000);
        Assert.Equal(0, clocks.ReadMilliseconds(item));

        clocks.Use(item, 2, 3000);

        Assert.Equal(2000, clocks.ReadMilliseconds(item));
        Assert.Equal("2", WiredCounterController.StoredState(item));
    }

    [Fact]
    public void ResetWhileTheTimerRunsDoesNothing()
    {
        var item = Timer("bb_counter", InteractionType.Banzaicounter, "60");
        var clocks = new WiredCounterController();
        clocks.Attach(item);
        clocks.Use(item, 1, 0);
        clocks.Poll(1000);

        clocks.Use(item, 2, 1500);

        Assert.True(clocks.IsRunning(item));
        Assert.Equal(59000, clocks.ReadMilliseconds(item));
        Assert.Equal("60", WiredCounterController.StoredState(item));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(-1)]
    public void OnlyUseStatesOneAndTwoControlANativeTimer(int state)
    {
        var item = Timer("bb_counter", InteractionType.Banzaicounter, "60");
        var clocks = new WiredCounterController();
        clocks.Attach(item);

        clocks.Use(item, state, 0);

        Assert.False(clocks.IsRunning(item));
        Assert.Equal(60000, clocks.ReadMilliseconds(item));
    }

    [Fact]
    public void AdjustingTheClockChangesOnlyTheShownValue()
    {
        var item = Timer("bb_counter", InteractionType.Banzaicounter, "60");
        var clocks = new WiredCounterController();
        clocks.Attach(item);
        Assert.True(clocks.Adjust(item, 2, 0, 90));
        Assert.Equal(45000, clocks.ReadMilliseconds(item));
        Assert.Equal("60", WiredCounterController.StoredState(item));

        clocks.Use(item, 1, 0);
        clocks.Poll(1000);
        Assert.Equal(44000, clocks.ReadMilliseconds(item));
        Assert.True(clocks.Adjust(item, 2, 0, 4));
        clocks.Poll(2000);
        clocks.Poll(3000);
        Assert.Equal(0, clocks.ReadMilliseconds(item));

        clocks.Use(item, 1, 9000);
        Assert.Equal(60000, clocks.ReadMilliseconds(item));
    }

    [Fact]
    public void AdjustedIdleTimerStillCyclesFromItsSavedDuration()
    {
        var item = Timer("bb_counter", InteractionType.Banzaicounter, "300");
        var clocks = new WiredCounterController();
        clocks.Attach(item);
        clocks.Adjust(item, 2, 0, 90);

        clocks.Use(item, 2, 0);

        Assert.Equal(600000, clocks.ReadMilliseconds(item));
    }

    [Fact]
    public void ReloadKeepsTheSavedDurationAndStopsTheClockEvenMidGame()
    {
        var item = Timer("bb_counter", InteractionType.Banzaicounter, "180");
        var clocks = new WiredCounterController();
        clocks.Attach(item);
        clocks.Use(item, 1, 0);
        clocks.Poll(1000);
        clocks.Poll(2000);
        Assert.Equal(178000, clocks.ReadMilliseconds(item));

        var reloaded = Timer("bb_counter", InteractionType.Banzaicounter, WiredCounterController.StoredState(item)!);
        var after = new WiredCounterController();
        after.Attach(reloaded);

        Assert.False(after.IsRunning(reloaded));
        Assert.Equal(180000, after.ReadMilliseconds(reloaded));
        Assert.Equal("180", reloaded.LegacyDataString);
        after.Use(reloaded, 2, 0);
        Assert.Equal(300000, after.ReadMilliseconds(reloaded));
    }

    [Fact]
    public void AnEndedTimerSavedAsZeroReloadsAtThirtySeconds()
    {
        var item = Timer("bb_counter", InteractionType.Banzaicounter, "0");
        var clocks = new WiredCounterController();
        clocks.Attach(item);

        Assert.Equal(30000, clocks.ReadMilliseconds(item));
        Assert.Equal("30", item.LegacyDataString);
    }

    [Fact]
    public void PlacingATimerAgainGivesAFreshThirtySeconds()
    {
        var item = Timer("bb_counter", InteractionType.Banzaicounter, "120");
        item.TimerDurationSeconds = 120;

        WiredCounterController.PlacedNew(item);
        var clocks = new WiredCounterController();
        clocks.Attach(item);

        Assert.Equal(30000, clocks.ReadMilliseconds(item));
        Assert.Equal("30", WiredCounterController.StoredState(item));
    }

    [Fact]
    public void OtherItemsSaveNoTimerState()
    {
        var item = new Item
        {
            Id = 2,
            Definition = new() { ItemName = "wf_upcounter1", InteractionName = "wf_upcounter1", InteractionType = InteractionType.Counter },
            ExtraData = new LegacyDataFormat { Data = "5" }
        };
        Assert.Null(WiredCounterController.StoredState(item));
    }

    private static Item Timer(string name, InteractionType type, string state) => new()
    {
        Id = 1,
        Definition = new() { ItemName = name, InteractionName = "game_timer", InteractionType = type },
        ExtraData = new LegacyDataFormat { Data = state }
    };
}
