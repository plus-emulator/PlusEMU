using Dapper;
using Microsoft.Extensions.Logging;
using Plus.Database;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.GameClients;
using Plus.Utilities.DependencyInjection;

namespace Plus.HabboHotel.Recycler;

public sealed record RecyclerAchievementProgress(int Items, int Boxes, int RecycledRewards, int Rare, int Grand)
{
    public IReadOnlyDictionary<string, int> Groups => new Dictionary<string, int>
    {
        ["ACH_RecycledItems"] = Items,
        ["ACH_RecycleBoxOpener"] = Boxes,
        ["ACH_RecycleEcoprizes"] = RecycledRewards,
        ["ACH_RecyclerRarePrize"] = Rare,
        ["ACH_RecyclerGrandPrize"] = Grand
    };
}

[Singleton]
public interface IRecyclerAchievementStore
{
    RecyclerAchievementProgress? Read(int userId, RecyclerAchievementProgress initial);
}

public sealed class RecyclerAchievementStore(IDatabase database) : IRecyclerAchievementStore
{
    public RecyclerAchievementProgress? Read(int userId, RecyclerAchievementProgress initial)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (connection.ExecuteScalar<int?>("SELECT id FROM users WHERE id=@userId FOR UPDATE", new { userId }, transaction) is null) {
            return null;
        }

        connection.Execute("""
            UPDATE recycler_progress SET initial_recycled_items=COALESCE(initial_recycled_items,@Items),
                initial_boxes_opened=COALESCE(initial_boxes_opened,@Boxes),initial_recycled_rewards=COALESCE(initial_recycled_rewards,@RecycledRewards),
                initial_rare_prizes=COALESCE(initial_rare_prizes,@Rare),initial_grand_prizes=COALESCE(initial_grand_prizes,@Grand)
            WHERE user_id=@userId
            """, new { userId, initial.Items, initial.Boxes, initial.RecycledRewards, initial.Rare, initial.Grand }, transaction);
        var totals = connection.QuerySingleOrDefault<ProgressRow>("""
            SELECT LEAST(2147483647,recycled_items+initial_recycled_items) AS Items,
                LEAST(2147483647,boxes_opened+initial_boxes_opened) AS Boxes,
                LEAST(2147483647,recycled_rewards+initial_recycled_rewards) AS RecycledRewards,
                LEAST(2147483647,rare_prizes+initial_rare_prizes) AS Rare,
                LEAST(2147483647,grand_prizes+initial_grand_prizes) AS Grand
            FROM recycler_progress WHERE user_id=@userId
            """, new { userId }, transaction);
        transaction.Commit();

        return totals is null ? null : new(totals.Items, totals.Boxes, totals.RecycledRewards, totals.Rare, totals.Grand);
    }

    private sealed class ProgressRow
    {
        public int Items { get; set; }
        public int Boxes { get; set; }
        public int RecycledRewards { get; set; }
        public int Rare { get; set; }
        public int Grand { get; set; }
    }
}

[Singleton]
public interface IRecyclerAchievementService
{
    void Sync(GameClient session);
}

public sealed class RecyclerAchievementService(IRecyclerAchievementStore store, IAchievementManager achievements,
    ILogger<RecyclerAchievementService> logger) : IRecyclerAchievementService
{
    public void Sync(GameClient session)
    {
        if (session.GetHabbo() is not { } habbo || habbo.Inventory is null || habbo.HabboStats is null) {
            return;
        }

        try {
            RecyclerAchievementProgress? totals;

            lock (habbo.WalletSync) {
                if (habbo.WalletClosed) {
                    return;
                }

                int Initial(string group) => Math.Max(0, habbo.GetAchievementData(group)?.Progress ?? 0);
                totals = store.Read(habbo.Id, new(Initial("ACH_RecycledItems"), Initial("ACH_RecycleBoxOpener"),
                    Initial("ACH_RecycleEcoprizes"), Initial("ACH_RecyclerRarePrize"), Initial("ACH_RecyclerGrandPrize")));
            }

            if (totals is null) {
                return;
            }

            foreach (var (group, total) in totals.Groups) {
                achievements.ProgressAchievement(session, group, Math.Max(total, habbo.GetAchievementData(group)?.Progress ?? 0), fromBeginning: true);
            }
        }
        catch (Exception exception) {
            logger.LogError(exception, "Could not synchronize recycler achievements for user {UserId}", habbo.Id);
        }
    }
}
