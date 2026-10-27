using Microsoft.Extensions.Time.Testing;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.Communication.Packets.Outgoing.Notifications;
using Plus.Communication.RCON.Commands.Hotel;
using Plus.Core;
using Xunit;

namespace Plus.Tests;

public sealed class PlannedShutdownTests
{
    [Fact]
    public void MaintenanceStatusWritesHabboLayout()
    {
        var composer = new MaintenanceStatusComposer(true, 5, 15);
        var packet = new HabbiconTestSupport.RecordingPacket();

        composer.Compose(packet);

        Assert.Equal(2747u, composer.MessageId);
        Assert.Equal(ServerPacketHeader.MaintenanceStatusComposer, composer.MessageId);
        Assert.Equal(new object[] { true, 5, 15 }, packet.Writes);
    }

    [Fact]
    public void WarnsAtOnceAndAtOneMinuteThenShutsDown()
    {
        var world = new World();

        Assert.True(world.Shutdown.TrySchedule(5, 20));
        Assert.Equal([[true, 5, 20]], world.Warnings());
        Assert.Equal(0, world.ShutdownCalls);

        world.Clock.Advance(TimeSpan.FromMinutes(4) - TimeSpan.FromSeconds(1));
        Assert.Single(world.Warnings());

        world.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal([[true, 5, 20], [true, 1, 20]], world.Warnings());
        Assert.Equal(0, world.ShutdownCalls);

        world.Clock.Advance(TimeSpan.FromMinutes(1) - TimeSpan.FromSeconds(1));
        Assert.Equal(0, world.ShutdownCalls);

        world.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, world.ShutdownCalls);
        Assert.Equal(2, world.Warnings().Count);
    }

    [Fact]
    public void OneMinuteNoticeWarnsOnce()
    {
        var world = new World();

        Assert.True(world.Shutdown.TrySchedule(1, 15));
        world.Clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal([[true, 1, 15]], world.Warnings());
        Assert.Equal(1, world.ShutdownCalls);
    }

    [Fact]
    public void LongestNoticeIsAccepted()
    {
        var world = new World();

        Assert.True(world.Shutdown.TrySchedule(PlannedShutdown.MaxMinutes, 15));
        world.Clock.Advance(TimeSpan.FromMinutes(PlannedShutdown.MaxMinutes));

        Assert.Equal([[true, PlannedShutdown.MaxMinutes, 15], [true, 1, 15]], world.Warnings());
        Assert.Equal(1, world.ShutdownCalls);
    }

    [Fact]
    public void SecondScheduleIsRefusedWhileOneIsPending()
    {
        var world = new World();
        Assert.True(world.Shutdown.TrySchedule(10, 15));

        Assert.False(world.Shutdown.TrySchedule(2, 15));
        world.Clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Single(world.Warnings());
        Assert.Equal(0, world.ShutdownCalls);
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(-1, 15)]
    [InlineData(5, -1)]
    [InlineData(PlannedShutdown.MaxMinutes + 1, 15)]
    public void InvalidScheduleSendsNothing(int minutes, int duration)
    {
        var world = new World();

        Assert.False(world.Shutdown.TrySchedule(minutes, duration));
        world.Clock.Advance(TimeSpan.FromHours(1));

        Assert.Empty(world.Broadcasts);
        Assert.Equal(0, world.ShutdownCalls);
    }

    [Fact]
    public void ShutdownIsPendingOnlyOnceAScheduleIsAccepted()
    {
        var world = new World();
        Assert.False(world.Shutdown.IsPending);

        Assert.False(world.Shutdown.TrySchedule(0, 15));
        Assert.False(world.Shutdown.IsPending);

        Assert.True(world.Shutdown.TrySchedule(5, 15));
        Assert.True(world.Shutdown.IsPending);
    }

    [Fact]
    public void AcceptedScheduleTellsEveryUserTheHotelIsShuttingDown()
    {
        var world = new World();

        Assert.True(world.Shutdown.TrySchedule(5, 20));
        Assert.Equal([[true, true, true]], world.Writes(ServerPacketHeader.AvailabilityStatusComposer));
        Assert.Equal([[true, 5, 20]], world.Warnings());

        Assert.False(world.Shutdown.TrySchedule(2, 20));
        Assert.Single(world.Writes(ServerPacketHeader.AvailabilityStatusComposer));
    }

    [Fact]
    public void ShutdownStateGoesOutOnceNotWithEveryWarning()
    {
        var world = new World();

        Assert.True(world.Shutdown.TrySchedule(5, 20));
        world.Clock.Advance(TimeSpan.FromMinutes(4));

        Assert.Equal(2, world.Warnings().Count);
        Assert.Single(world.Writes(ServerPacketHeader.AvailabilityStatusComposer));
    }

    [Fact]
    public void AvailabilityStatusWritesHabboLayout()
    {
        var composer = new AvailabilityStatusComposer();
        var open = new HabbiconTestSupport.RecordingPacket();
        var shuttingDown = new HabbiconTestSupport.RecordingPacket();

        composer.Compose(open);
        new AvailabilityStatusComposer(true).Compose(shuttingDown);

        Assert.Equal(1182u, composer.MessageId);
        Assert.Equal(ServerPacketHeader.AvailabilityStatusComposer, composer.MessageId);
        Assert.Equal(new object[] { true, false, true }, open.Writes);
        Assert.Equal(new object[] { true, true, true }, shuttingDown.Writes);
    }

    [Theory]
    [InlineData("5|30", 5, 30)]
    [InlineData("5", 5, PlannedShutdown.DefaultDurationMinutes)]
    [InlineData("5|", 5, PlannedShutdown.DefaultDurationMinutes)]
    public async Task RconShutdownSchedulesTheGivenNotice(string arguments, int minutes, int duration)
    {
        var parameters = arguments.Split('|');
        var planned = new RecordingPlannedShutdown(true);
        var command = new ShutdownCommand(planned);

        Assert.Equal("shutdown", command.Key);
        Assert.True(await command.TryExecute(parameters));
        Assert.Equal([(minutes, duration)], planned.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("5|long")]
    public async Task RconShutdownRejectsUnreadableMinutes(string arguments)
    {
        var parameters = arguments.Split('|');
        var planned = new RecordingPlannedShutdown(true);

        Assert.False(await new ShutdownCommand(planned).TryExecute(parameters));
        Assert.Empty(planned.Requests);
    }

    [Fact]
    public async Task RconShutdownWithoutParametersIsRejected()
    {
        var planned = new RecordingPlannedShutdown(true);

        Assert.False(await new ShutdownCommand(planned).TryExecute([]));
        Assert.Empty(planned.Requests);
    }

    [Fact]
    public async Task RconShutdownReportsARefusedSchedule()
    {
        var planned = new RecordingPlannedShutdown(false);

        Assert.False(await new ShutdownCommand(planned).TryExecute(["5", "15"]));
        Assert.Equal([(5, 15)], planned.Requests);
    }

    private sealed class World
    {
        private readonly List<IServerPacket> _broadcasts = [];
        public FakeTimeProvider Clock { get; } = new();
        public int ShutdownCalls;
        public PlannedShutdown Shutdown { get; }
        public IReadOnlyList<IServerPacket> Broadcasts => _broadcasts;

        public World() => Shutdown = new(new TestGameClientManager(_ => null, _broadcasts), Clock, () => ShutdownCalls++);

        public List<object[]> Warnings() => Writes(ServerPacketHeader.MaintenanceStatusComposer);

        public List<object[]> Writes(uint header) => _broadcasts.Where(packet => packet.MessageId == header).Select(packet =>
        {
            var recorded = new HabbiconTestSupport.RecordingPacket();
            packet.Compose(recorded);

            return recorded.Writes.ToArray();
        }).ToList();
    }

    private sealed class RecordingPlannedShutdown(bool accept) : IPlannedShutdown
    {
        public List<(int Minutes, int Duration)> Requests { get; } = [];
        public bool IsPending => false;

        public bool TrySchedule(int minutes, int durationMinutes)
        {
            Requests.Add((minutes, durationMinutes));

            return accept;
        }
    }
}
