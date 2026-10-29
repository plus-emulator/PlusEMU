using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.Chat.Commands.Moderator;
using Plus.Core.FigureData;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Plus.Tests;

[Collection("StressBudget")]
public class StressPlayerTests
{
    public StressPlayerTests() => StressBudget.Reset();

    private static StressPlayerRequest Request(int amount, List<string> replies, int owner = 7, bool heavy = false) =>
        new(amount, heavy, owner, replies.Add, gender => $"hd-180-1.{gender}", new StressBotTests.NoPetLocale());

    private static void Drain(RoomUserManager manager, int cycles = 40)
    {
        for (var i = 0; i < cycles; i++) {
            manager.ProcessStressPlayers();
        }
    }

    [Fact]
    public void ASpawnIsSpreadOverCyclesAndRepliesOnce()
    {
        var (manager, _) = StressBotTests.CreateRoom();
        var replies = new List<string>();
        Assert.True(manager.QueueStressPlayers(Request(250, replies)));

        manager.ProcessStressPlayers();
        Assert.Equal(RoomUserManager.MaxStressSpawnPerCycle, manager.GetUserList().Count);
        Assert.Empty(replies);

        Drain(manager);
        Assert.Equal(250, manager.GetUserList().Count);
        Assert.Contains("Created 250", Assert.Single(replies));
    }

    [Fact]
    public void StressPlayersAreNeverRealUsersBotsOrPets()
    {
        var store = new RecordingStore();
        var (manager, _) = StressBotTests.CreateRoom(store);
        Assert.True(manager.QueueStressPlayers(Request(30, [])));
        Drain(manager);

        Assert.Empty(manager.GetRoomUsers());
        Assert.Equal(0, manager.PetCount);
        Assert.False(manager.TryGetBot(-1, out _));
        Assert.All(manager.GetUserList(), user =>
        {
            Assert.True(user.HabboId < 0);
            Assert.NotNull(user.Stress);
            Assert.Null(user.GetClient());
            Assert.False(user.IsBot);
        });

        manager.QueueStressPlayers(Request(0, []));
        Drain(manager);
        manager.UpdatePets();
        manager.Dispose();
        Assert.Empty(store.Calls);
    }

    [Fact]
    public void PlayersAndBotsShareOneRoomLimit()
    {
        var (manager, _) = StressBotTests.CreateRoom();
        var replies = new List<string>();
        manager.QueueStressPlayers(Request(1500, replies));
        Drain(manager);
        manager.QueueStressBots(600, 7, replies.Add);
        manager.ProcessStressBots();
        Assert.Equal(1500, manager.GetUserList().Count);
        Assert.Contains("limit is 2000", replies.Last());

        manager.QueueStressPlayers(Request(501, replies));
        Drain(manager);
        Assert.Equal(1500, manager.GetUserList().Count);
        Assert.Contains("limit is 2000", replies.Last());

        manager.QueueStressPlayers(Request(500, replies));
        Drain(manager, 10);
        Assert.Equal(2000, manager.GetUserList().Count);
    }

    [Fact]
    public void TheHotelWideLimitCoversAllRoomsAndIsFreedOnDispose()
    {
        var rooms = Enumerable.Range(0, 3).Select(_ => StressBotTests.CreateRoom().Manager).ToArray();
        var replies = new List<string>();

        try {
            for (var i = 0; i < 2; i++) {
                rooms[i].QueueStressPlayers(Request(2000, replies));
                Drain(rooms[i], 25);
            }

            rooms[2].QueueStressPlayers(Request(1, replies));
            Drain(rooms[2]);
            Assert.Empty(rooms[2].GetUserList());
            Assert.Contains("hotel", replies.Last());

            rooms[0].Dispose();
            rooms[2].QueueStressPlayers(Request(1, replies));
            Drain(rooms[2]);
            Assert.Single(rooms[2].GetUserList());
        }
        finally {
            foreach (var room in rooms) {
                room.Dispose();
            }
        }
    }

    [Fact]
    public void AClearIsSpreadOverCyclesAndRepliesOnce()
    {
        var (manager, _) = StressBotTests.CreateRoom();
        var replies = new List<string>();
        manager.QueueStressPlayers(Request(500, []));
        Drain(manager);
        manager.QueueStressPlayers(Request(0, replies));

        manager.ProcessStressPlayers();
        Assert.Equal(500 - RoomUserManager.MaxStressClearPerCycle, manager.GetUserList().Count);
        Assert.Empty(replies);

        Drain(manager, 5);
        Assert.Empty(manager.GetUserList());
        Assert.Equal("Cleared 500 stress players.", Assert.Single(replies));
    }

    [Fact]
    public void ClearCancelsASpawnThatIsStillRunning()
    {
        var (manager, _) = StressBotTests.CreateRoom();
        manager.QueueStressPlayers(Request(500, []));
        manager.ProcessStressPlayers();
        manager.QueueStressPlayers(Request(0, []));
        manager.ProcessStressPlayers();
        Assert.Empty(manager.GetUserList());
        Drain(manager);
        Assert.Empty(manager.GetUserList());
        Assert.Equal(0, manager.StressEntityCount);
    }

    [Fact]
    public void LeavingTheCallerClearsTheirStressPlayersButAnotherUserDoesNot()
    {
        var (manager, _) = StressBotTests.CreateRoom();
        manager.QueueStressPlayers(Request(10, [], owner: 7));
        Drain(manager);

        manager.ClearStressFor(8);
        manager.ProcessStressPlayers();
        Assert.Equal(10, manager.GetUserList().Count);

        manager.ClearStressFor(7);
        manager.ProcessStressPlayers();
        Assert.Empty(manager.GetUserList());
    }

    [Fact]
    public void LeavingTheCallerAlsoClearsTheirStressBots()
    {
        var (manager, _) = StressBotTests.CreateRoom();
        manager.QueueStressBots(30, 7, _ => { });
        manager.ProcessStressBots();
        Assert.Equal(30, manager.GetUserList().Count);

        manager.ClearStressFor(7);
        manager.ProcessStressPlayers();
        Assert.Empty(manager.GetUserList());
    }

    [Fact]
    public void ADisposedRoomRefusesAndIgnoresStressWork()
    {
        var (manager, _) = StressBotTests.CreateRoom();
        manager.QueueStressPlayers(Request(10, []));
        Drain(manager);
        manager.Dispose();

        Assert.False(manager.QueueStressPlayers(Request(10, [])));
        manager.ProcessStressPlayers();
        manager.CycleStressPlayers();
    }

    [Fact]
    public void OneCycleStartsAtMostAFewMovesAndChats()
    {
        var (manager, _) = StressBotTests.CreateRoom();
        manager.QueueStressPlayers(Request(1000, []));
        Drain(manager, 15);

        manager.CycleStressPlayers();

        var moving = manager.GetUserList().Count(user => user.Movement.Commands.Read() != null);
        Assert.InRange(moving, 1, RoomUserManager.MaxStressMovesPerCycle);
    }

    [Fact]
    public void HeavyModeWithoutAHorseAreaIsRefusedBeforeAnythingSpawns()
    {
        var (manager, _) = StressBotTests.CreateRoom();
        var replies = new List<string>();
        manager.QueueStressPlayers(Request(1500, replies, heavy: true));
        manager.ProcessStressPlayers();

        Assert.Empty(manager.GetUserList());
        Assert.Contains("limit", Assert.Single(replies));
    }

    [Fact]
    public async Task RequestsFromManyThreadsNeverCorruptTheQueue()
    {
        var (manager, _) = StressBotTests.CreateRoom();
        using var stop = new CancellationTokenSource();
        var producers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested) {
                manager.QueueStressPlayers(Request(1, []));
            }
        })).ToArray();

        for (var i = 0; i < 3000; i++) {
            manager.ProcessStressPlayers();
        }

        stop.Cancel();
        await Task.WhenAll(producers);
        Assert.True(manager.StressEntityCount <= RoomUserManager.MaxStressPlayers);
    }

    [Theory]
    [InlineData("private", 7, 7, false, true)]
    [InlineData("private", 9, 7, false, false)]
    [InlineData("private", 9, 7, true, true)]
    [InlineData("public", 7, 7, true, false)]
    [InlineData("", 7, 7, true, false)]
    [InlineData("private", 0, 7, false, false)]
    public void OnlyPrivateRoomsOfTheCallerOrOfAnyOwnerAllowSpawning(string type, int roomOwner, int user, bool ownsAnyRoom, bool allowed)
    {
        Assert.Equal(allowed, StressCommand.RefuseSpawn(type, roomOwner, user, ownsAnyRoom) == null);
    }

    [Fact]
    public void SpawningHasACooldownPerUser()
    {
        var clock = new ManualClock();
        var command = new StressCommand(new FigureDataManager(NullLogger<FigureDataManager>.Instance), new StressBotTests.NoPetLocale(), clock);

        Assert.True(command.TryStartCooldown(7));
        Assert.False(command.TryStartCooldown(7));
        Assert.True(command.TryStartCooldown(8));
        clock.Advance(StressCommand.Cooldown - TimeSpan.FromMilliseconds(1));
        Assert.False(command.TryStartCooldown(7));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(command.TryStartCooldown(7));
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class RecordingStore : IRoomUserStore
    {
        public List<string> Calls { get; } = [];
        public void UpdateUserCount(uint roomId, int count) { }
        public void SavePet(RoomPetSave pet) => Calls.Add("pet");
        public void SaveBot(RoomBotSave bot) => Calls.Add("bot");
        public void RecordExit(uint roomId, int userId, DateTimeOffset exitedAt, int usersNow) => Calls.Add("exit");
    }
}
