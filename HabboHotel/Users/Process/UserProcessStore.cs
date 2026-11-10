using Dapper;
using Plus.Database;

namespace Plus.HabboHotel.Users.Process;

public interface IUserProcessStore
{
    void ResetDailyRespects(int userId, int respects, int petRespects, string day);
    bool SaveCurrency(int userId, int credits, int duckets);
}

public sealed class UserProcessStore(IDatabase database) : IUserProcessStore
{
    public bool SaveCurrency(int userId, int credits, int duckets)
    {
        if (userId <= 0 || credits < 0 || duckets < 0) {
            return false;
        }

        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (connection.QuerySingleOrDefault<int?>("SELECT id FROM users WHERE id=@userId FOR UPDATE", new { userId }, transaction) is null) {
            return false;
        }

        connection.Execute("UPDATE users SET credits=@credits WHERE id=@userId", new { userId, credits }, transaction);
        UserCurrencyStore.Set(connection, userId, ActivityPointType.Duckets, duckets, transaction);
        transaction.Commit();

        return true;
    }

    public void ResetDailyRespects(int userId, int respects, int petRespects, string day)
    {
        using var connection = database.Connection();
        connection.Execute("UPDATE `user_statistics` SET `dailyRespectPoints` = @respects, `dailyPetRespectPoints` = @petRespects, `respectsTimestamp` = @timestamp WHERE `id` = @id",
            new { respects, petRespects, timestamp = day, id = userId });
    }
}
