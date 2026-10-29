using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.AI.Speech;
using Plus.HabboHotel.Rooms.Chat.Pets.Locale;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Rooms;

/// <summary>Amount 0 clears. Outfit maps a gender to a look.</summary>
public sealed record StressPlayerRequest(int Amount, bool Heavy, int OwnerId, Action<string> Reply, Func<string, string> Outfit, IPetLocale Locale);

public partial class RoomUserManager
{
    public const int MaxStressPlayers = 2000;
    public const int MaxStressSpawnPerCycle = 100;
    public const int MaxStressMovesPerCycle = 50;
    public const int MaxStressChatsPerCycle = 5;
    private const int MaxStressBacklog = 8;
    public const int MaxStressClearPerCycle = 200;

    // The horse body dyes, as HorseCustomizationService maps them.
    private static readonly int[] HorseRaces = [2, 6, 10, 14, 18, 22, 26, 30, 34, 38, 42, 46, 50, 54, 58, 61, 65, 69, 73];

    private static readonly string[] StressLines =
    [
        "Hello everyone!", "Anyone want to trade?", "This room is so busy!", "Follow me!", "Who wants to play a game?",
        "Nice room!", "Where is the party?", "I love this hotel", "Can I have rights?", "brb", "lol", "hi :)",
        "Look at my outfit!", "Wait for me!", "Race you to the door!", "Is anyone here?", "So many people!", "Hotel full?"
    ];

    private readonly Queue<StressPlayerRequest> _stressPlayerRequests = new();
    private readonly List<RoomUser> _stressPlayers = [];
    private readonly List<RoomUser> _stressHorses = [];
    private readonly HashSet<int> _stressOwners = [];
    private StressSpawn? _stressSpawn;
    private bool _stressClearRequested;
    private bool _stressClearing;
    private bool _stressClearBots;
    private int _stressClearTotal;
    private Action<string>? _stressClearReply;
    private int _stressCursor;
    private int _nextStressPlayerId;

    private sealed class StressSpawn(StressPlayerRequest request, int x, int y, double z, int rotation)
    {
        public StressPlayerRequest Request { get; } = request;
        public int X { get; } = x;
        public int Y { get; } = y;
        public double Z { get; } = z;
        public int Rotation { get; } = rotation;
        public int Remaining { get; set; } = request.Amount;
        public int Entities => Remaining * (Request.Heavy ? 2 : 1);
    }

    // Players, horses and temporary bots all count against the room and hotel limits.
    internal int StressEntityCount => _stressPlayers.Count + _stressHorses.Count + _bots.Values.Count(user => user.BotData?.IsTemporary == true);

    // Requests are bounded and applied on the cycle thread, like the stress bots.
    public bool QueueStressPlayers(StressPlayerRequest request)
    {
        if (request.Amount < 0 || request.Amount > MaxStressPlayers) {
            return false;
        }

        lock (_stressSync) {
            if (_disposed || _stressPlayerRequests.Count >= MaxStressBacklog) {
                return false;
            }

            _stressPlayerRequests.Enqueue(request);

            return true;
        }
    }

    // Removes every stress entity when someone who spawned them leaves the room.
    internal void ClearStressFor(int userId)
    {
        lock (_stressSync) {
            if (_stressOwners.Contains(userId)) {
                _stressClearRequested = true;
            }
        }
    }

    private void ReportStress() => StressBudget.Hold(this, StressEntityCount + (_stressSpawn?.Entities ?? 0));

    internal void ProcessStressPlayers()
    {
        lock (_stressSync) {
            if (_disposed) {
                return;
            }

            if (_stressClearRequested) {
                _stressClearRequested = false;
                _stressPlayerRequests.Clear();
                _stressSpawn = null;
                BeginStressClear(true, null);
            }

            if (_stressSpawn != null && _stressPlayerRequests.TryPeek(out var next) && next.Amount == 0) {
                _stressSpawn = null;
            }

            if (!_stressClearing && _stressSpawn == null) {
                StartStressRequest();
            }

            if (_stressClearing) {
                RemoveStressBatch();
            }
            else if (_stressSpawn != null) {
                SpawnStressBatch(_stressSpawn);
            }

            ReportStress();
        }
    }

    private void BeginStressClear(bool includeBots, Action<string>? reply)
    {
        _stressClearing = true;
        _stressClearBots = includeBots;
        _stressClearReply = reply;
        _stressClearTotal = _stressPlayers.Count;
    }

    private void StartStressRequest()
    {
        if (!_stressPlayerRequests.TryDequeue(out var request)) {
            return;
        }

        if (request.Amount == 0) {
            BeginStressClear(false, request.Reply);

            return;
        }

        var entities = request.Amount * (request.Heavy ? 2 : 1);

        if (StressEntityCount + entities > MaxStressPlayers) {
            request.Reply($"This room has {StressEntityCount} stress entities; the limit is {MaxStressPlayers}. Use :stress players clear first.");

            return;
        }

        if (!StressBudget.TryHold(this, StressEntityCount + entities)) {
            request.Reply($"The hotel-wide stress limit of {StressBudget.GlobalMax} is reached. Clear stress players in other rooms first.");

            return;
        }

        if (request.Heavy) {
            if (StressRoller(request.OwnerId) is not { } roller) {
                request.Reply("Heavy mode needs a roller in the room. Stand on one or place one first.");

                return;
            }

            _stressSpawn = new(request, roller.GetX, roller.GetY, _room.GetGameMap().SqAbsoluteHeight(roller.GetX, roller.GetY), roller.Rotation);
        }
        else {
            var model = _room.GetGameMap().Model;
            _stressSpawn = new(request, model.DoorX, model.DoorY, model.DoorZ, model.DoorOrientation);
        }

        _stressOwners.Add(request.OwnerId);
    }

    // Removal is spread over cycles like spawning, so a large clear never stalls a tick.
    private void RemoveStressBatch()
    {
        var map = _room.GetGameMap();

        for (var n = 0; n < MaxStressClearPerCycle; n++) {
            if (_stressHorses.Count > 0) {
                var horse = _stressHorses[^1];
                _stressHorses.RemoveAt(_stressHorses.Count - 1);
                map.RemoveUserFromMap(horse, horse.Coordinate);
                RemoveBot(horse.VirtualId, false);
            }
            else if (_stressPlayers.Count > 0) {
                var player = _stressPlayers[^1];
                _stressPlayers.RemoveAt(_stressPlayers.Count - 1);
                RemoveRoomUser(player, true);
            }
            else if (_stressClearBots && _bots.Values.FirstOrDefault(user => user.BotData?.IsTemporary == true) is { } bot) {
                map.RemoveUserFromMap(bot, bot.Coordinate);
                RemoveBot(bot.VirtualId, false);
            }
            else {
                break;
            }
        }

        if (_stressHorses.Count > 0 || _stressPlayers.Count > 0 || _stressClearBots && _bots.Values.Any(user => user.BotData?.IsTemporary == true)) {
            return;
        }

        _stressClearing = false;
        _stressOwners.Clear();
        _stressClearReply?.Invoke($"Cleared {_stressClearTotal} stress players.");
        _stressClearReply = null;
    }

    private void SpawnStressBatch(StressSpawn spawn)
    {
        var batch = Math.Min(spawn.Remaining, MaxStressSpawnPerCycle);
        var request = spawn.Request;

        for (var i = 0; i < batch; i++) {
            var rider = SpawnStressPlayer(request, spawn.X, spawn.Y, spawn.Z, spawn.Rotation);

            if (request.Heavy) {
                var horse = SpawnStressHorse(request.OwnerId, spawn.X, spawn.Y, spawn.Z, spawn.Rotation);
                _room.GetGameMap().Navigation!.Mounts.Ride(rider, horse, true, request.Locale);
            }
        }

        spawn.Remaining -= batch;

        if (spawn.Remaining > 0) {
            return;
        }

        _stressSpawn = null;
        request.Reply(request.Heavy
            ? $"Created {request.Amount} stress players riding horses on the roller at {spawn.X},{spawn.Y} ({_stressPlayers.Count} players). Use :stress players clear to remove them."
            : $"Created {request.Amount} roaming stress players ({_stressPlayers.Count} players). Use :stress players clear to remove them.");
    }

    // Roaming players pick a new spot as soon as they stop and talk now and then. Moves and chats per cycle are capped.
    internal void CycleStressPlayers()
    {
        var count = _stressPlayers.Count;

        if (count == 0) {
            return;
        }

        var map = _room.GetGameMap();
        var moves = 0;
        var chats = 0;
        var next = _stressCursor % count;

        for (var n = 0; n < count; n++) {
            var index = (_stressCursor + n) % count;
            var player = _stressPlayers[index];

            // A stress player never falls asleep.
            player.IdleTime = 0;

            if (player.Stress is not { Heavy: false }) {
                continue;
            }

            if (moves < MaxStressMovesPerCycle && !player.IsWalking && map.TryGetRandomWalkableSquare(true, out var square)) {
                player.MoveTo(square.X, square.Y, MoveOrigin.Bot);
                moves++;
                next = (index + 1) % count;
            }

            if (chats < MaxStressChatsPerCycle && Random.Shared.Next(40) == 0) {
                player.Chat(StressLines[Random.Shared.Next(StressLines.Length)]);
                chats++;
            }
        }

        _stressCursor = next;
    }

    // The roller under the caller, else the room's first roller.
    private Item? StressRoller(int ownerId)
    {
        var rollers = _room.GetRoomItemHandler().GetFloor.Where(item => item.Definition.InteractionType == InteractionType.Roller).OrderBy(item => item.Id).ToList();
        var caller = GetRoomUserByHabbo(ownerId);

        return rollers.FirstOrDefault(item => caller != null && item.GetX == caller.X && item.GetY == caller.Y) ?? rollers.FirstOrDefault();
    }

    private RoomUser SpawnStressPlayer(StressPlayerRequest request, int x, int y, double z, int rotation)
    {
        // Negative ids cannot collide with real users or pets.
        var id = --_nextStressPlayerId;
        var gender = Random.Shared.Next(2) == 0 ? "M" : "F";
        var user = new RoomUser(id, _room.RoomId, Interlocked.Increment(ref _primaryPrivateUserId) - 1, _room, null, _chatEmotions, _rewards)
        {
            Stress = new($"Player{-id}", request.Outfit(gender), gender, request.Heavy)
        };
        user.InternalRoomId = user.VirtualId;
        user.InitializePosition(x, y, z);
        user.SetRot(rotation, false);
        user.AllowOverride = true;
        user.UpdateNeeded = true;
        PublishSpawn(user);
        _room.SendUser(user);
        _stressPlayers.Add(user);

        return user;
    }

    private RoomUser SpawnStressHorse(int ownerId, int x, int y, double z, int rotation)
    {
        var id = --_nextStressPlayerId;
        var pet = new Pet(id, ownerId, _room.RoomId, $"Horse{-id}", 15, HorseRaces[Random.Shared.Next(HorseRaces.Length)].ToString(), "FFFFFF",
            0, 100, 100, 0, null, x, y, z, Random.Shared.Next(9, 11), 1, 1, -1, "", "");
        var speeches = new List<RandomSpeech>();
        var bot = new RoomBot(id, _room.RoomId, "pet", "freeroam", pet.Name, "", pet.Look, x, y, z, rotation,
            0, 0, 0, 0, ref speeches, "", 0, ownerId, false, 0, false, 0)
        { IsTemporary = true };
        var horse = DeployBot(bot, pet);
        _stressHorses.Add(horse);

        return horse;
    }

    private void ReleaseStressOnDispose()
    {
        _stressPlayerRequests.Clear();
        _stressSpawn = null;
        _stressClearing = false;
        _stressClearReply = null;
        _stressHorses.Clear();
        _stressPlayers.Clear();
        _stressOwners.Clear();
        StressBudget.Hold(this, 0);
    }
}
