using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Inventory.Badges;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Notifications;
using Plus.Communication.Packets.Outgoing.Quests;
using Plus.Core;
using Plus.Core.Settings;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Subscriptions;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Badges;

namespace Plus.HabboHotel.Quests.DailyTasks;

public interface IDailyTaskManager
{
    /// <summary>Raised after a claim paid out, outside the user's lock.</summary>
    event Action<GameClient>? TaskClaimed;

    /// <summary>Sends the user's tasks (ActiveDailyTasks), handing out the day's tasks first when needed.</summary>
    void SendActive(GameClient session);

    /// <summary>Advances today's tasks of the action; a task with parameters only moves for one of them.</summary>
    void Progress(GameClient session, string actionType, int amount = 1, string? parameter = null);

    void Claim(GameClient session, int taskId);
}

/// <summary>
/// The daily tasks window. Every user gets dailytasks.count tasks (default 3) per day of the server's time zone, drawn from daily_tasks by weight; completing all
/// of them opens one bonus task. Completed tasks of the last dailytasks.unclaimed_days days (default 7) stay claimable, which the client lists as unclaimed tasks.
/// At the day's end every online user gets the new day's list.
/// </summary>
public sealed class DailyTaskManager(IDailyTaskStore store, ISettingsManager settings, IGameClientManager clients, IBadgeManager badges,
    TimeProvider clock, ILogger<DailyTaskManager> logger) : IDailyTaskManager, IStartable, IDisposable
{
    public const int DefaultCount = 3;
    public const int DefaultUnclaimedDays = 7;

    private readonly ConcurrentDictionary<int, object> _gates = new();
    private readonly ConcurrentDictionary<int, UserState> _users = new();
    private readonly CancellationTokenSource _stopping = new();
    private Definitions _definitions = Definitions.Empty;

    public event Action<GameClient>? TaskClaimed;

    public int StartOrder => 30;

    public Task Start()
    {
        Load();
        _ = Task.Run(() => RunResets(_stopping.Token));

        return Task.CompletedTask;
    }

    // The container may dispose the instance once per registered interface; cancelling twice is harmless.
    public void Dispose() => _stopping.Cancel();

    /// <summary>(Re)reads the task definitions; a definition with a reward the server cannot pay is left out.</summary>
    internal void Load()
    {
        try {
            var usable = new Dictionary<int, DailyTaskDefinition>();

            foreach (var definition in store.LoadDefinitions()) {
                if (definition.Code.Length == 0 || definition.ActionType.Length == 0 || DailyTaskRules.Grant(definition, false) == null) {
                    logger.LogWarning("Daily task {Id} ({Code}) is skipped: it needs a code, an action and rewards of currencies or badges", definition.Id, definition.Code);

                    continue;
                }

                usable[definition.Id] = definition;
            }

            Volatile.Write(ref _definitions, new Definitions(usable));
            _users.Clear();
            logger.LogInformation("Loaded {Count} daily tasks.", usable.Count);
        }
        catch (Exception exception) {
            logger.LogError(exception, "Daily task definitions failed to load.");
        }
    }

    public void SendActive(GameClient session)
    {
        if (session?.GetHabbo() is not { } habbo || !Supports(session, ServerPacketHeader.ActiveDailyTasksComposer)) {
            return;
        }

        lock (Gate(habbo.Id)) {
            var now = clock.GetUtcNow();

            if (Ensure(habbo.Id, now, out _) is { } state) {
                session.Send(new ActiveDailyTasksComposer(Visible(state, now)));
            }
        }
    }

    public void Progress(GameClient session, string actionType, int amount = 1, string? parameter = null)
    {
        if (session?.GetHabbo() is not { } habbo || amount < 1 || string.IsNullOrEmpty(actionType)) {
            return;
        }

        var definitions = Volatile.Read(ref _definitions);

        // Most actions (every chat line) have no task: answer those without a lock or a query.
        if (!definitions.Actions.Contains(actionType) || !Supports(session, ServerPacketHeader.DailyTaskUpdatedComposer)) {
            return;
        }

        lock (Gate(habbo.Id)) {
            var now = clock.GetUtcNow();

            if (Ensure(habbo.Id, now, out var reloaded) is not { } state) {
                return;
            }

            if (reloaded) {
                session.Send(new ActiveDailyTasksComposer(Visible(state, now)));
            }

            var completed = false;

            foreach (var task in state.Tasks.Where(task => task.Day == state.Day && task.Status == DailyTaskStatus.InProgress).ToList()) {
                if (!definitions.ById.TryGetValue(task.DefinitionId, out var definition) || !definition.Matches(actionType, parameter)
                    || DailyTaskRules.Plan(task, definition, amount) is not { } plan) {
                    continue;
                }

                if (!Try(() => store.SaveProgress(habbo.Id, task.Id, plan.Progress, plan.Status, now), "saving daily task progress", habbo.Id)) {
                    continue;
                }

                task.Progress = plan.Progress;
                task.Status = plan.Status;
                completed |= plan.Status == DailyTaskStatus.Completed;
                session.Send(new DailyTaskUpdatedComposer(task.Id, task.Progress, task.Status, DailyTaskRules.SecondsLeft(task, now)));
            }

            if (completed && OpenBonus(habbo.Id, state) is { Count: > 0 } bonus) {
                session.Send(new DailyTasksAddedComposer(bonus.Select(task => DailyTaskRules.Wire(task, definitions.ById[task.DefinitionId], now)).ToList()));
            }
        }
    }

    public void Claim(GameClient session, int taskId)
    {
        if (session?.GetHabbo() is not { } habbo || !Supports(session, ServerPacketHeader.DailyTaskUpdatedComposer)) {
            return;
        }

        var claimed = false;

        lock (Gate(habbo.Id)) {
            var now = clock.GetUtcNow();

            if (Ensure(habbo.Id, now, out var reloaded) is not { } state) {
                return;
            }

            var task = state.Tasks.FirstOrDefault(task => task.Id == taskId);

            if (task == null || reloaded) {
                // The client's list is stale: the new list puts its claim button right again.
                session.Send(new ActiveDailyTasksComposer(Visible(state, now)));

                if (task == null) {
                    return;
                }
            }

            var definitions = Volatile.Read(ref _definitions);

            if (task.Status != DailyTaskStatus.Completed || !definitions.ById.TryGetValue(task.DefinitionId, out var definition)
                || DailyTaskRules.Grant(definition, ClubAccess.LevelFor(habbo.Access) > 0) is not { } grant || !TryResolveBadges(habbo, grant, out grant)) {
                SendUpdate(session, task, now);

                return;
            }

            DailyTaskClaimOutcome outcome;

            lock (habbo.WalletSync) {
                if (habbo.WalletClosed) {
                    return;
                }

                outcome = store.Claim(habbo.Id, task.Id, grant, now);

                if (outcome == DailyTaskClaimOutcome.Claimed) {
                    habbo.Credits += grant.Credits;

                    foreach (var (type, amount) in grant.Points) {
                        habbo.Currencies[type] += amount;
                    }
                }
            }

            logger.LogInformation("Daily task claim user {UserId} task {TaskId} ({Code}) outcome {Outcome}", habbo.Id, task.Id, definition.Code, outcome);

            if (outcome == DailyTaskClaimOutcome.Failed) {
                // Still claimable; the update enables the claim button again (DailyTaskView.updateStatusAndRepeatsUI).
                SendUpdate(session, task, now);

                return;
            }

            // NotClaimable: the row was claimed already (a racing claim); show it claimed.
            task.Status = DailyTaskStatus.Claimed;
            SendUpdate(session, task, now);

            if (outcome == DailyTaskClaimOutcome.Claimed) {
                SendGrant(session, habbo, grant);
                claimed = true;
            }
        }

        if (claimed) {
            TaskClaimed?.Invoke(session);
        }
    }

    /// <summary>Sends the new day's list to everyone online and forgets the users who left.</summary>
    internal void RefreshOnline()
    {
        var online = clients.GetClients.Where(client => client.GetHabbo() != null).ToList();
        var ids = online.Select(client => client.GetHabbo()!.Id).ToHashSet();

        foreach (var userId in _users.Keys.Where(id => !ids.Contains(id)).ToList()) {
            _users.TryRemove(userId, out _);
        }

        if (Volatile.Read(ref _definitions).ById.Count == 0) {
            return;
        }

        foreach (var client in online) {
            try {
                SendActive(client);
            }
            catch (Exception exception) {
                logger.LogError(exception, "Sending the new daily tasks failed for user {UserId}", client.GetHabbo()?.Id);
            }
        }
    }

    private async Task RunResets(CancellationToken token)
    {
        while (!token.IsCancellationRequested) {
            var now = clock.GetUtcNow();
            var zone = clock.LocalTimeZone;
            var wait = DailyTaskRules.ResetAfter(DailyTaskRules.DayOf(now, zone), zone) - now;

            try {
                // A second past the reset, so the new day has surely begun.
                await Task.Delay(wait + TimeSpan.FromSeconds(1), clock, token);
                RefreshOnline();
            }
            catch (OperationCanceledException) {
                return;
            }
            catch (Exception exception) {
                logger.LogError(exception, "The daily task reset failed.");
            }
        }
    }

    /// <summary>The user's state for today; loads it (and hands out the day's tasks) when it is missing or from an earlier day.</summary>
    private UserState? Ensure(int userId, DateTimeOffset now, out bool reloaded)
    {
        reloaded = false;
        var zone = clock.LocalTimeZone;
        var today = DailyTaskRules.DayOf(now, zone);

        if (_users.TryGetValue(userId, out var cached) && cached.Day == today) {
            return cached;
        }

        try {
            var definitions = Volatile.Read(ref _definitions);
            var tasks = store.LoadUser(userId, today, today.AddDays(-UnclaimedDays())).ToList();
            var hasRegular = tasks.Any(task => task.Day == today && definitions.ById.TryGetValue(task.DefinitionId, out var definition) && !definition.Bonus);

            if (!hasRegular) {
                var picks = DailyTaskRules.Pick(definitions.ById.Values.Where(definition => !definition.Bonus), Count(), new Random(DailyTaskRules.Seed(userId, today)));
                Add(tasks, store.Assign(userId, today, DailyTaskRules.ResetAfter(today, zone), picks.Select(definition => definition.Id).ToList()));
            }

            var state = new UserState(today, tasks);
            // A bonus that was due but never handed out (for example the server stopped in between) comes with the list.
            OpenBonus(userId, state);
            _users[userId] = state;
            reloaded = true;

            return state;
        }
        catch (Exception exception) {
            logger.LogError(exception, "Loading the daily tasks of user {UserId} failed", userId);

            return null;
        }
    }

    private List<UserDailyTask> OpenBonus(int userId, UserState state)
    {
        var definitions = Volatile.Read(ref _definitions);
        var today = state.Tasks.Where(task => task.Day == state.Day && definitions.ById.ContainsKey(task.DefinitionId))
            .Select(task => (task, definitions.ById[task.DefinitionId]));

        if (!DailyTaskRules.BonusDue(today)) {
            return [];
        }

        var picks = DailyTaskRules.Pick(definitions.ById.Values.Where(definition => definition.Bonus), 1, new Random(~DailyTaskRules.Seed(userId, state.Day)));

        if (picks.Count == 0) {
            return [];
        }

        try {
            var zone = clock.LocalTimeZone;

            return Add(state.Tasks, store.Assign(userId, state.Day, DailyTaskRules.ResetAfter(state.Day, zone), [picks[0].Id]));
        }
        catch (Exception exception) {
            logger.LogError(exception, "Opening the bonus daily task of user {UserId} failed", userId);

            return [];
        }
    }

    /// <summary>Today's tasks (regular first, then the bonus) and the completed tasks of earlier days.</summary>
    private List<DailyTaskWire> Visible(UserState state, DateTimeOffset now)
    {
        var definitions = Volatile.Read(ref _definitions);

        return state.Tasks
            .Where(task => definitions.ById.ContainsKey(task.DefinitionId)
                           && (task.Day == state.Day || task.Status == DailyTaskStatus.Completed && task.Day < state.Day))
            .OrderBy(task => task.Day == state.Day ? 0 : 1)
            .ThenBy(task => definitions.ById[task.DefinitionId].Bonus)
            .ThenByDescending(task => task.Day)
            .ThenBy(task => task.Id)
            .Select(task => DailyTaskRules.Wire(task, definitions.ById[task.DefinitionId], now))
            .ToList();
    }

    private void SendGrant(GameClient session, Habbo habbo, DailyTaskGrant grant)
    {
        if (grant.Credits != 0) {
            session.Send(new CreditBalanceComposer(habbo.Credits));
        }

        foreach (var (type, amount) in grant.Points) {
            session.Send(new HabboActivityPointNotificationComposer(habbo.Currencies[type], amount, type));
        }

        if (grant.Badges.IsEmpty || habbo.Inventory is not { } inventory) {
            return;
        }

        var added = false;

        foreach (var badge in grant.Badges.Where(badge => !inventory.Badges.HasBadge(badge))) {
            inventory.Badges.AddBadge(new Badge(badge, 0));
            added = true;
        }

        if (added) {
            session.SendBadgeInventory(habbo.Id, inventory.Badges.Badges.Values);
            session.Send(new FurniListNotificationComposer(1, 4));
        }
    }

    /// <summary>Badge rewards take the defined badge's code; a badge that is not defined, or needs a right the user lacks, is not paid.</summary>
    private bool TryResolveBadges(Habbo habbo, DailyTaskGrant grant, out DailyTaskGrant resolved)
    {
        resolved = grant;

        if (grant.Badges.IsEmpty) {
            return true;
        }

        var codes = ImmutableArray.CreateBuilder<string>();

        foreach (var code in grant.Badges) {
            if (habbo.Inventory == null || !badges.Badges.TryGetValue(code.ToUpperInvariant(), out var definition)
                || definition.RequiredRight.Length > 0 && !habbo.Access.Can(definition.RequiredRight)) {
                logger.LogError("Daily task badge {Badge} has no grantable definition", code);

                return false;
            }

            codes.Add(definition.Code);
        }

        resolved = grant with { Badges = codes.ToImmutable() };

        return true;
    }

    private void SendUpdate(GameClient session, UserDailyTask task, DateTimeOffset now) =>
        session.Send(new DailyTaskUpdatedComposer(task.Id, task.Progress, task.Status, DailyTaskRules.SecondsLeft(task, now)));

    private bool Try(Func<bool> write, string what, int userId)
    {
        try {
            return write();
        }
        catch (Exception exception) {
            logger.LogError(exception, "Failed {What} for user {UserId}", what, userId);

            return false;
        }
    }

    private static List<UserDailyTask> Add(List<UserDailyTask> tasks, IEnumerable<UserDailyTask> rows)
    {
        var added = rows.Where(row => tasks.All(task => task.Id != row.Id)).ToList();
        tasks.AddRange(added);

        return added;
    }

    private int Count() => Setting("dailytasks.count", DefaultCount, 0, 20);

    private int UnclaimedDays() => Setting("dailytasks.unclaimed_days", DefaultUnclaimedDays, 0, 365);

    private int Setting(string key, int fallback, int min, int max) =>
        int.TryParse(settings.GetOptionalValue(key), out var value) ? Math.Clamp(value, min, max) : fallback;

    // Only revisions that know the daily task packets get them; sending an unmapped packet would throw.
    private static bool Supports(GameClient session, uint header) => session.Revision?.InternalIdToOutgoingIdMapping?.ContainsKey(header) == true;

    private object Gate(int userId) => _gates.GetOrAdd(userId, _ => new object());

    private sealed class UserState(DateOnly day, List<UserDailyTask> tasks)
    {
        public DateOnly Day { get; } = day;
        public List<UserDailyTask> Tasks { get; } = tasks;
    }

    private sealed class Definitions(IReadOnlyDictionary<int, DailyTaskDefinition> byId)
    {
        public static Definitions Empty { get; } = new(new Dictionary<int, DailyTaskDefinition>());

        public IReadOnlyDictionary<int, DailyTaskDefinition> ById { get; } = byId;
        public IReadOnlySet<string> Actions { get; } = byId.Values.Select(definition => definition.ActionType).ToHashSet(StringComparer.Ordinal);
    }
}
