using System.Collections.Concurrent;
using Plus.Core.FigureData;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms.Chat.Pets.Locale;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

public class StressCommand : IChatCommand
{
    private readonly IFigureDataManager _figureDataManager;
    private readonly IPetLocale _petLocale;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<int, DateTimeOffset> _lastSpawn = new();

    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    public StressCommand(IFigureDataManager figureDataManager, IPetLocale petLocale, TimeProvider clock)
    {
        _figureDataManager = figureDataManager;
        _petLocale = petLocale;
        _clock = clock;
    }

    public string Key => "stress";
    public string Parameters => "bots <amount|clear> | players <amount> [heavy] | players clear";
    public string Description => "Creates temporary bots or players for room stress testing. Heavy players ride horses on one roller.";

    // A permission alone is not enough: grants and wildcards can reach any user, and only hotel owners may spawn this many bots.
    internal static bool CanExecute(UserAccess permissions) =>
        permissions.Roles.Any(role => role.Slug == "owner") && permissions.Can(PermissionKeys.ModerationTool) && permissions.Can(PermissionKeys.CommandStress);

    // Spawning only works in private rooms: the caller's own, or any room for a user who may own any room.
    internal static string? RefuseSpawn(string roomType, int roomOwnerId, int userId, bool ownsAnyRoom)
    {
        if (roomType != "private" || roomOwnerId <= 0) {
            return "Stress spawning is not allowed in public rooms.";
        }

        return roomOwnerId == userId || ownsAnyRoom ? null : "Stress spawning is only allowed in your own rooms.";
    }

    internal bool TryStartCooldown(int userId)
    {
        var now = _clock.GetUtcNow();
        var allowed = false;
        _lastSpawn.AddOrUpdate(userId, _ =>
        {
            allowed = true;

            return now;
        }, (_, last) =>
        {
            allowed = now - last >= Cooldown;

            return allowed ? now : last;
        });

        return allowed;
    }

    private bool MaySpawn(GameClient session, Room room, int amount)
    {
        if (amount == 0) {
            return true;
        }

        var habbo = session.GetHabbo();
        var refusal = RefuseSpawn(room.Type, room.OwnerId, habbo.Id, habbo.Access.Can(PermissionKeys.RoomOwnerAny));

        if (refusal == null && !TryStartCooldown(habbo.Id)) {
            refusal = $"Wait {Cooldown.TotalSeconds:0} seconds between stress requests.";
        }

        if (refusal != null) {
            session.SendWhisper(refusal);
        }

        return refusal == null;
    }

    internal static bool TryParse(string[] parameters, out int amount)
    {
        amount = 0;

        if (parameters.Length != 2 || !parameters[0].Equals("bots", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        if (parameters[1].Equals("clear", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return int.TryParse(parameters[1], out amount) && amount > 0 && amount <= RoomUserManager.MaxStressBots;
    }

    // players <amount> [heavy] or players clear; clear is amount 0.
    internal static bool TryParsePlayers(string[] parameters, out int amount, out bool heavy)
    {
        amount = 0;
        heavy = parameters.Length == 3 && parameters[2].Equals("heavy", StringComparison.OrdinalIgnoreCase);

        if (parameters.Length is < 2 or > 3 || !parameters[0].Equals("players", StringComparison.OrdinalIgnoreCase) || parameters.Length == 3 && !heavy) {
            return false;
        }

        if (parameters[1].Equals("clear", StringComparison.OrdinalIgnoreCase)) {
            return !heavy;
        }

        return int.TryParse(parameters[1], out amount) && amount > 0 && amount <= RoomUserManager.MaxStressPlayers;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (!CanExecute(session.GetHabbo().Access)) {
            session.SendWhisper("Only hotel owners can use :stress.");

            return;
        }

        if (parameters.Length > 0 && parameters[0].Equals("players", StringComparison.OrdinalIgnoreCase)) {
            ExecutePlayers(session, room, parameters);

            return;
        }

        if (!TryParse(parameters, out var amount)) {
            session.SendWhisper($"Usage: :stress bots <1-{RoomUserManager.MaxStressBots}>, :stress bots clear, :stress players <1-{RoomUserManager.MaxStressPlayers}> [heavy] or :stress players clear.");

            return;
        }

        if (!MaySpawn(session, room, amount)) {
            return;
        }

        if (!room.GetRoomUserManager().QueueStressBots(amount, session.GetHabbo().Id, message => session.SendWhisper(message),
                gender => _figureDataManager.GenerateRandomFigure(gender, Random.Shared))) {
            session.SendWhisper("Stress request unavailable: the room is closing or its request queue is full. Try again shortly.");
        }
    }

    private void ExecutePlayers(GameClient session, Room room, string[] parameters)
    {
        if (!TryParsePlayers(parameters, out var amount, out var heavy)) {
            session.SendWhisper($"Usage: :stress players <1-{RoomUserManager.MaxStressPlayers}> [heavy] or :stress players clear.");

            return;
        }

        if (!MaySpawn(session, room, amount)) {
            return;
        }

        var request = new StressPlayerRequest(amount, heavy, session.GetHabbo().Id, message => session.SendWhisper(message),
            gender => _figureDataManager.GenerateRandomFigure(gender, Random.Shared), _petLocale);

        if (!room.GetRoomUserManager().QueueStressPlayers(request)) {
            session.SendWhisper("Stress request unavailable: the room is closing or its request queue is full. Try again shortly.");
        }
    }
}
