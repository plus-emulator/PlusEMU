using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items.Wired.Settings;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

// Date Matches and Time Matches read the clock in UTC when the room has no timezone of its own. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredR103ConditionClockTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("Europe/Berlin", 2)]
    public void TheConditionClockIsUtcUnlessTheRoomNamesAZone(string zone, int offsetHours)
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        var wired = new WiredComponent(room, TestLogging.Logger, new SummerClock(), TestRoomSettings.Empty, new Factory(zone),
            TestWiredConfigurationStore.Instance, TestWiredDatabase.Instance, TestWiredRewardService.Instance, TestBotManagementStore.Instance, TestWiredClients.Empty, TestGroupManager.Empty, TestWiredDefinitions.Unused, TestWiredCommands.Unused, TestWiredAccess.Unused, TestItemRuntime.Travel);

        Assert.Equal(TimeSpan.FromHours(offsetHours), wired.ConditionCalendarTime.Offset);
        Assert.Equal(new SummerClock().GetUtcNow(), wired.ConditionCalendarTime);
    }

    private sealed class SummerClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2040, 7, 1, 22, 0, 0, TimeSpan.Zero);
    }

    private sealed class Factory(string zone) : IWiredRoomSettingsFactory, IWiredRoomSettingsStore
    {
        public WiredRoomSettings Create(Room room) => new(room, this);
        public WiredRoomSettingsSnapshot? Load(uint roomId) => new(TimeZoneId: zone);
        public void Save(uint roomId, int actorId, bool staff, WiredRoomSettingsSnapshot? expected, WiredRoomSettingsSnapshot settings) => throw new NotSupportedException();
    }
}
