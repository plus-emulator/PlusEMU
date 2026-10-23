using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610151745-318700621: an At Given Time box placed and saved in a room whose timer had already passed its
// time did not fire; its first flash came after the next timer reset passed the time.
public sealed class WiredAtGivenTimePlacementTests : IDisposable
{
    private readonly G1World _world = new();
    private readonly G1Player _player;
    private readonly SaveWiredTriggerConfigEvent _save = new(new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!,
        new FakeWordFilter(), TestLogging.For<WiredConfigurationService>()));

    public WiredAtGivenTimePlacementTests()
    {
        _player = _world.AddPlayer(1);
        _world.Room.OwnerName = "user1";
        _world.Room.Type = "private";
        _world.Clock.Now = new(2040, 6, 1, 12, 0, 0, TimeSpan.Zero);
        _world.Room.LastTimerResetAt = _world.Clock.Now;
    }

    public void Dispose() => _world.Dispose();

    [Theory]
    [InlineData("wf_trg_at_given_time")]
    [InlineData("wf_trg_at_time_long")]
    public void ABoxPlacedAfterTheRoomTimerPassedItsTimeWaitsForTheNextReset(string name)
    {
        _world.Wired.RoomInitialized();
        Run(10_000);
        var box = Place(name);
        Assert.Equal(ServerPacketHeader.HideWiredConfigComposer, Save(box, 1));

        Assert.Empty(Flashes(box, 5_000));

        ResetTimers();
        Assert.Single(Flashes(box, 5_000));
    }

    [Fact]
    public void AnExistingBoxSavedAfterTheRoomTimerPassedItsTimeWaitsForTheNextReset()
    {
        var box = Place("wf_trg_at_given_time");
        _world.Wired.RoomInitialized();
        Assert.Single(Flashes(box, 3_000));

        Assert.Equal(ServerPacketHeader.HideWiredConfigComposer, Save(box, 2));
        Assert.Empty(Flashes(box, 5_000));

        ResetTimers();
        Assert.Single(Flashes(box, 5_000));
    }

    [Fact]
    public void ABoxLoadedWithASlowRoomLoadFiresAtItsTimeAfterTheRoomLoads()
    {
        _world.Clock.Advance(TimeSpan.FromSeconds(2));
        var box = Place("wf_trg_at_given_time");
        _world.Wired.RoomInitialized();

        Assert.Single(Flashes(box, 3_000));
    }

    [Fact]
    public void ABoxPlacedBeforeItsTimeStillFires()
    {
        _world.Wired.RoomInitialized();
        var box = Place("wf_trg_at_given_time");
        Assert.Equal(ServerPacketHeader.HideWiredConfigComposer, Save(box, 4));

        Assert.Single(Flashes(box, 5_000));
    }

    [Fact]
    public void ABoxPlacedBeforeItsTimeFiresAtItsTimeAfterTheLastReset()
    {
        _world.Wired.RoomInitialized();
        Run(1_000);
        ResetTimers();
        Run(5_200);
        var box = Place("wf_trg_at_given_time");
        Assert.Equal(ServerPacketHeader.HideWiredConfigComposer, Save(box, 20));

        var flashes = Flashes(box, 8_000);

        var flash = Assert.Single(flashes);
        Assert.InRange(flash, 5_250, 5_450);

        ResetTimers();
        Assert.Single(Flashes(box, 11_000));
    }

    [Theory]
    [InlineData(20, 7_400, 7_600)]
    [InlineData(24, 9_400, 9_600)]
    public void ABoxSavedAgainBeforeItsTimeFiresAtItsTimeAfterTheLastReset(int pulses, int earliest, int latest)
    {
        var box = Place("wf_trg_at_given_time");
        _world.Wired.RoomInitialized();
        Run(1_000);
        ResetTimers();
        Run(3_000);
        Assert.Equal(ServerPacketHeader.HideWiredConfigComposer, Save(box, pulses));

        var flash = Assert.Single(Flashes(box, 11_000));
        Assert.InRange(flash, earliest, latest);
    }

    // The engine clock and the room timer are separate clocks cut to whole milliseconds, so a save never lines up with the reset tick.
    [Theory]
    [InlineData(0.6, 0.5)]
    [InlineData(0.1, 0.9)]
    [InlineData(0.99, 0.01)]
    public void ASavedBoxStillFiresOnTheRightTickWhenTheClocksAreOffByFractionsOfAMillisecond(double beforeReset, double beforeSave)
    {
        var box = Place("wf_trg_at_given_time");
        _world.Wired.RoomInitialized();
        Run(1_000);
        _world.Clock.Advance(TimeSpan.FromMilliseconds(beforeReset));
        ResetTimers();
        Run(3_000);
        _world.Clock.Advance(TimeSpan.FromMilliseconds(beforeSave));
        Assert.Equal(ServerPacketHeader.HideWiredConfigComposer, Save(box, 20));

        var flash = Assert.Single(Flashes(box, 11_000));
        Assert.InRange(flash, 7_400, 7_650);
    }

    // As the placement service does: the furni joins the room, then its Wired box loads with the stored or default settings.
    private Item Place(string name)
    {
        var item = _world.AddItem(name, InteractionType.WiredTrigger, 1, 1);
        Assert.NotNull(_world.Wired.LoadWiredBox(item));

        return item;
    }

    private uint Save(Item box, int pulses)
    {
        _player.Packets.Clear();
        _save.Parse(_player.Client, EditorTestSupport.Incoming((int)box.Id, 1, pulses, "", 0, 0, 0, 0, 0)).GetAwaiter().GetResult();

        return _player.Packets.Select(packet => packet.Header)
            .Single(header => header is ServerPacketHeader.HideWiredConfigComposer or ServerPacketHeader.WiredValidationErrorComposer);
    }

    // What Reset Timers does: the room timer restarts on the next room tick and every timed trigger starts over.
    private void ResetTimers()
    {
        var now = _world.Clock.Now;
        var engineNow = _world.Clock.Milliseconds;
        _world.Room.RestartTimerAt(now, now.AddMilliseconds(_world.Wired.FirstRoomTickAfter(engineNow) - engineNow));
        _world.Wired.ResetTimers(_world.Floor.Values);
    }

    // The times, in ms from the start of the run, at which the room was sent a data update for the box.
    private int[] Flashes(Item box, int milliseconds)
    {
        _player.Packets.Clear();
        var flashes = new List<int>();

        for (var at = 50; at <= milliseconds; at += 50) {
            var sent = _player.Packets.Count;
            Run(50);

            if (_player.Packets.Skip(sent).Any(packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer && Updates(packet.Payload).Contains(box.Id))) {
                flashes.Add(at);
            }
        }

        return [.. flashes];
    }

    private void Run(int milliseconds)
    {
        for (var end = _world.Clock.Milliseconds + milliseconds; _world.Clock.Milliseconds < end;) {
            _world.Clock.Advance(TimeSpan.FromMilliseconds(50));
            using var owner = RoomOwnerScope.Enter(_world.Room);

            if (_world.Clock.Milliseconds % 500 == 0) {
                _world.Wired.OnRoomTick();
            }

            _world.Wired.OnCycle();
            _world.Wired.OnFastCycle();
        }
    }

    private static uint[] Updates(byte[] payload)
    {
        var packet = new FlashIncomingPacket { Buffer = payload };
        var count = packet.ReadInt();
        var ids = new uint[count];

        for (var index = 0; index < count; index++) {
            ids[index] = (uint)packet.ReadInt();
            Assert.Equal(0, packet.ReadInt());
            packet.ReadString();
        }

        return ids;
    }
}
