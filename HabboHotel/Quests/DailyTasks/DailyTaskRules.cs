using System.Collections.Immutable;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Quests.DailyTasks;

/// <summary>What a claim pays out: credits, activity points by type and badge codes.</summary>
public sealed record DailyTaskGrant(int Credits, ImmutableSortedDictionary<int, int> Points, ImmutableArray<string> Badges);

/// <summary>The daily task decisions, free of storage and sessions.</summary>
public static class DailyTaskRules
{
    /// <summary>The day a moment belongs to, in the server's time zone.</summary>
    public static DateOnly DayOf(DateTimeOffset now, TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

    /// <summary>The first moment of the next day in the server's time zone; a local midnight skipped by a clock change moves to the first valid local time.</summary>
    public static DateTimeOffset ResetAfter(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        while (zone.IsInvalidTime(local)) {
            local = local.AddMinutes(15);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    public static int Seed(int userId, DateOnly day) => unchecked(userId * 486187739 + day.DayNumber);

    /// <summary>
    /// Draws up to <paramref name="count"/> different tasks by weight. Tasks of an action already drawn wait until no other
    /// action is left, so a day mixes actions when the pool allows it.
    /// </summary>
    public static IReadOnlyList<DailyTaskDefinition> Pick(IEnumerable<DailyTaskDefinition> pool, int count, Random random)
    {
        var remaining = pool.Where(definition => definition.Weight > 0).OrderBy(definition => definition.Id).ToList();
        var picked = new List<DailyTaskDefinition>();

        while (picked.Count < count && remaining.Count > 0) {
            var fresh = remaining.Where(definition => picked.All(other => other.ActionType != definition.ActionType)).ToList();
            var candidates = fresh.Count > 0 ? fresh : remaining;
            var roll = random.Next(candidates.Sum(definition => definition.Weight));
            var index = 0;

            while (roll >= candidates[index].Weight) {
                roll -= candidates[index].Weight;
                index++;
            }

            picked.Add(candidates[index]);
            remaining.Remove(candidates[index]);
        }

        return picked;
    }

    /// <summary>The progress and status after <paramref name="amount"/> more repeats, or null when the task does not move.</summary>
    public static (int Progress, DailyTaskStatus Status)? Plan(UserDailyTask task, DailyTaskDefinition definition, int amount)
    {
        if (task.Status != DailyTaskStatus.InProgress || amount < 1) {
            return null;
        }

        var required = Math.Max(1, definition.RequiredRepeats);
        var progress = (int)Math.Min(required, (long)task.Progress + amount);

        return (progress, progress >= required ? DailyTaskStatus.Completed : DailyTaskStatus.InProgress);
    }

    /// <summary>
    /// Seconds until the task's day ends. A task of an earlier day reports -1: the client lists it under unclaimed tasks
    /// when it is completed (DailyTasksController.as:171-186, class_2919.isExpired).
    /// </summary>
    public static int SecondsLeft(UserDailyTask task, DateTimeOffset now)
    {
        if (task.ExpiresAt <= now) {
            return -1;
        }

        return (int)Math.Min(int.MaxValue, Math.Ceiling((task.ExpiresAt - now).TotalSeconds));
    }

    public static DailyTaskWire Wire(UserDailyTask task, DailyTaskDefinition definition, DateTimeOffset now) => new(
        task.Id,
        definition.Code,
        definition.QuestTypeCode,
        definition.Bonus,
        definition.ImageVersion,
        definition.CatalogName,
        Math.Max(1, definition.RequiredRepeats),
        task.Progress,
        task.Status,
        SecondsLeft(task, now),
        definition.Rewards);

    /// <summary>The bonus task opens once every regular task of the day is completed (text dailytasks.bonus_available).</summary>
    public static bool BonusDue(IEnumerable<(UserDailyTask Task, DailyTaskDefinition Definition)> today)
    {
        var tasks = today.ToList();
        var regular = tasks.Where(entry => !entry.Definition.Bonus).ToList();

        return regular.Count > 0 && regular.All(entry => entry.Task.Status != DailyTaskStatus.InProgress) && tasks.All(entry => !entry.Definition.Bonus);
    }

    /// <summary>
    /// What claiming the task pays, or null when a reward cannot be granted. Duckets are doubled for club members, as the
    /// window's footer says (hc.has.double_duckets.info, DailyTasksView.setHcDoubleDuckets).
    /// </summary>
    public static DailyTaskGrant? Grant(DailyTaskDefinition definition, bool clubMember)
    {
        var credits = 0L;
        var points = new SortedDictionary<int, long>();
        var badges = ImmutableArray.CreateBuilder<string>();

        foreach (var reward in definition.Rewards) {
            if (reward.Amount < 1) {
                return null;
            }

            switch (reward.ProductItemTypeId) {
                case DailyTaskReward.Currency when int.TryParse(reward.RewardTypeId, out var type):
                    if (type == DailyTaskReward.Credits) {
                        credits += reward.Amount;
                    }
                    else if (ActivityPointType.IsValid(type)) {
                        points[type] = points.GetValueOrDefault(type) + reward.Amount * (type == ActivityPointType.Duckets && clubMember ? 2L : 1L);
                    }
                    else {
                        return null;
                    }

                    break;
                case DailyTaskReward.Badge when reward.RewardTypeId.Length > 0:
                    badges.Add(reward.RewardTypeId);
                    break;
                default:
                    return null;
            }
        }

        if (credits > int.MaxValue || points.Values.Any(amount => amount > int.MaxValue)) {
            return null;
        }

        return new((int)credits, points.ToImmutableSortedDictionary(entry => entry.Key, entry => (int)entry.Value), badges.ToImmutable());
    }
}
