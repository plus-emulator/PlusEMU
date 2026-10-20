using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    private static readonly DateTimeOffset RoomLoadedAt = new(2040, 4, 5, 6, 0, 0, TimeSpan.Zero);

    // Observed on AIR WIN63-202610151745-318700621: in a room loaded long ago that nobody reset, More Than 1201 passes and Less Than 1201
    // fails on the first check, and @wired_timer counts the half-seconds since the room loaded.
    [Fact]
    public void TheRoomTimerRunsFromRoomLoadBeforeAnyTimeCheck()
    {
        var timer = new RoomTimerWorld { Now = RoomLoadedAt.AddMinutes(11) };

        Assert.True(timer.Check("wf_cnd_time_more_than", 1201));
        Assert.False(timer.Check("wf_cnd_time_less_than", 1201));
        Assert.Equal(1320, timer.WiredTimer());

        timer.Now = RoomLoadedAt.AddMinutes(11).AddMilliseconds(500);
        Assert.Equal(1321, timer.WiredTimer());
    }

    private sealed class RoomTimerWorld
    {
        private readonly Room _room;
        private readonly WiredComponent _wired;

        public RoomTimerWorld()
        {
            var (room, map, _) = World();
            var clock = new SettableClock(() => Now);
            _wired = new WiredComponent(room, TestLogging.Logger, clock, TestRoomSettings.Empty, TestWiredRoomSettingsFactory.Instance, TestWiredConfigurationStore.Instance,
                TestWiredDatabase.Instance, TestWiredRewardService.Instance, TestBotManagementStore.Instance, TestWiredClients.Empty, TestGroupManager.Empty,
                TestWiredDefinitions.Unused, TestWiredCommands.Unused, TestWiredAccess.Unused, TestItemRuntime.Travel);
            room.SetRuntime(map, room.GetRoomItemHandler(), room.GetRoomUserManager(), _wired, new TestRoomUserSnapshots(), clock);
            _room = room;
            Clock = clock;
        }

        public DateTimeOffset Now { get; set; } = RoomLoadedAt;
        public Room Room => _room;
        public TimeProvider Clock { get; }

        public bool Check(string name, int pulses)
        {
            var condition = new WiredModernCondition(_room, MakeItem(101, name), Descriptor(name), TestGroupManager.Empty, _ => null, () => Now);
            Assert.True(WiredNativeTestSupport.TryValidateRuntime(condition, new() { IntParams = [pulses] }, out var configuration, out var error), error);
            condition.ApplyConfiguration(configuration);

            return condition.Execute(Context(_room, new(WiredEventKind.Use), [], []));
        }

        public long? WiredTimer() => _wired.ReadBuiltin(new(WiredVariableTarget.Global, "@wired_timer"), new(WiredVariableTarget.Global, 0, 0),
            new WiredVariableFrame(_room.Id, []));
    }

    private sealed class SettableClock(Func<DateTimeOffset> now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now();
    }
}
