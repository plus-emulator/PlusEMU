using System.Collections.Immutable;
using Dapper;
using Microsoft.Extensions.Logging;
using Plus.Database;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Quests.DailyTasks;

public enum DailyTaskClaimOutcome
{
    Claimed,
    NotClaimable,
    Failed
}

public interface IDailyTaskStore
{
    IReadOnlyList<DailyTaskDefinition> LoadDefinitions();

    /// <summary>The user's tasks of <paramref name="today"/>, and completed unclaimed tasks of the days since <paramref name="unclaimedSince"/>.</summary>
    IReadOnlyList<UserDailyTask> LoadUser(int userId, DateOnly today, DateOnly unclaimedSince);

    /// <summary>Hands the tasks to the user for the day (a task already handed out that day stays as it is) and returns their rows.</summary>
    IReadOnlyList<UserDailyTask> Assign(int userId, DateOnly day, DateTimeOffset expiresAt, IReadOnlyList<int> definitionIds);

    /// <summary>Stores progress of a task still in progress; false when nothing was written.</summary>
    bool SaveProgress(int userId, long taskId, int progress, DailyTaskStatus status, DateTimeOffset now);

    /// <summary>Marks a completed task claimed and pays the grant in one transaction.</summary>
    DailyTaskClaimOutcome Claim(int userId, long taskId, DailyTaskGrant grant, DateTimeOffset now);
}

public sealed class DailyTaskStore(IDatabase database, ILogger<DailyTaskStore> logger) : IDailyTaskStore
{
    private const string UserColumns = "id AS Id, task_id AS TaskId, day AS Day, expires_at AS ExpiresAt, progress AS Progress, status AS Status";

    public IReadOnlyList<DailyTaskDefinition> LoadDefinitions()
    {
        using var connection = database.Connection();
        var tasks = connection.Query<TaskRow>(
            """
            SELECT id AS Id, code AS Code, action_type AS ActionType, parameters AS Parameters, quest_type_code AS QuestTypeCode,
                   required_repeats AS RequiredRepeats, image_version AS ImageVersion, catalog_name AS CatalogName,
                   bonus AS Bonus, weight AS Weight
            FROM daily_tasks WHERE enabled <> 0 ORDER BY id
            """).ToList();
        var rewards = connection.Query<RewardRow>(
                """
                SELECT task_id AS TaskId, product_item_type_id AS ProductItemTypeId, reward_type_id AS RewardTypeId,
                       extra_params AS ExtraParams, amount AS Amount
                FROM daily_task_rewards ORDER BY task_id, sort_order, id
                """)
            .GroupBy(row => row.TaskId)
            .ToDictionary(group => group.Key, group => group.Select(row => new DailyTaskReward(row.ProductItemTypeId, row.RewardTypeId ?? "", row.ExtraParams ?? "", row.Amount)).ToImmutableArray());

        return tasks.Select(row => new DailyTaskDefinition(row.Id, row.Code, row.ActionType, DailyTaskDefinition.ParseParameters(row.Parameters),
            row.QuestTypeCode ?? "", row.RequiredRepeats, row.ImageVersion ?? "", row.CatalogName ?? "", row.Bonus != 0, row.Weight,
            rewards.GetValueOrDefault(row.Id, ImmutableArray<DailyTaskReward>.Empty))).ToList();
    }

    public IReadOnlyList<UserDailyTask> LoadUser(int userId, DateOnly today, DateOnly unclaimedSince)
    {
        using var connection = database.Connection();

        return connection.Query<UserRow>(
                $"SELECT {UserColumns} FROM user_daily_tasks WHERE user_id = @userId AND (day = @today OR (status = @completed AND day >= @since AND day < @today)) ORDER BY day DESC, id",
                new { userId, today = Date(today), since = Date(unclaimedSince), completed = (int)DailyTaskStatus.Completed })
            .Select(row => row.ToTask()).ToList();
    }

    public IReadOnlyList<UserDailyTask> Assign(int userId, DateOnly day, DateTimeOffset expiresAt, IReadOnlyList<int> definitionIds)
    {
        if (definitionIds.Count == 0) {
            return [];
        }

        using var connection = database.Connection();
        connection.Execute(
            "INSERT IGNORE INTO user_daily_tasks (user_id, task_id, day, expires_at, progress, status) VALUES (@userId, @taskId, @day, @expiresAt, 0, 0)",
            definitionIds.Select(taskId => new { userId, taskId, day = Date(day), expiresAt = expiresAt.UtcDateTime }));

        return connection.Query<UserRow>($"SELECT {UserColumns} FROM user_daily_tasks WHERE user_id = @userId AND day = @day AND task_id IN @definitionIds ORDER BY id",
                new { userId, day = Date(day), definitionIds })
            .Select(row => row.ToTask()).ToList();
    }

    public bool SaveProgress(int userId, long taskId, int progress, DailyTaskStatus status, DateTimeOffset now)
    {
        using var connection = database.Connection();

        return connection.Execute(
            """
            UPDATE user_daily_tasks SET progress = @progress, status = @status,
                   completed_at = CASE WHEN @status = 1 THEN @now ELSE completed_at END
            WHERE id = @taskId AND user_id = @userId AND status = 0
            """,
            new { userId, taskId, progress, status = (int)status, now = now.UtcDateTime }) == 1;
    }

    public DailyTaskClaimOutcome Claim(int userId, long taskId, DailyTaskGrant grant, DateTimeOffset now)
    {
        try {
            using var connection = database.Connection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            // The status guard makes a second claim (or a double click racing the first) change nothing.
            if (connection.Execute("UPDATE user_daily_tasks SET status = 2, claimed_at = @now WHERE id = @taskId AND user_id = @userId AND status = 1",
                    new { userId, taskId, now = now.UtcDateTime }, transaction) != 1) {
                transaction.Rollback();

                return DailyTaskClaimOutcome.NotClaimable;
            }

            if (grant.Credits != 0) {
                connection.Execute("UPDATE users SET credits = credits + @credits WHERE id = @userId", new { userId, credits = grant.Credits }, transaction);
            }

            foreach (var (type, amount) in grant.Points) {
                UserCurrencyStore.Add(connection, userId, type, amount, transaction);
            }

            foreach (var badge in grant.Badges) {
                // An owned badge keeps its slot.
                connection.Execute("INSERT INTO user_badges (user_id, badge_id, badge_slot) VALUES (@userId, @badge, 0) ON DUPLICATE KEY UPDATE badge_slot = badge_slot",
                    new { userId, badge }, transaction);
            }

            transaction.Commit();

            return DailyTaskClaimOutcome.Claimed;
        }
        catch (Exception exception) {
            logger.LogError(exception, "Claiming daily task {TaskId} for user {UserId} failed", taskId, userId);

            return DailyTaskClaimOutcome.Failed;
        }
    }

    private static DateTime Date(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

    private sealed class TaskRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string ActionType { get; set; } = "";
        public string? Parameters { get; set; }
        public string? QuestTypeCode { get; set; }
        public int RequiredRepeats { get; set; }
        public string? ImageVersion { get; set; }
        public string? CatalogName { get; set; }
        public int Bonus { get; set; }
        public int Weight { get; set; }
    }

    private sealed class RewardRow
    {
        public int TaskId { get; set; }
        public short ProductItemTypeId { get; set; }
        public string? RewardTypeId { get; set; }
        public string? ExtraParams { get; set; }
        public int Amount { get; set; }
    }

    private sealed class UserRow
    {
        public long Id { get; set; }
        public int TaskId { get; set; }
        public DateTime Day { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int Progress { get; set; }
        public int Status { get; set; }

        public UserDailyTask ToTask() => new(Id, TaskId, DateOnly.FromDateTime(Day), new DateTimeOffset(DateTime.SpecifyKind(ExpiresAt, DateTimeKind.Utc)),
            Progress, (DailyTaskStatus)Status);
    }
}
