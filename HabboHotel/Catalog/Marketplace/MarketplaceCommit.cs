using System.Data;
using System.Data.Common;
using Dapper;
using Plus.Database;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Catalog.Marketplace;

internal sealed class MarketplaceOutcomeUnknownException(Exception cause) : Exception("Marketplace outcome requires a fresh session.", cause);
internal sealed class MarketplaceCommitFailedException(Exception cause) : Exception("Marketplace operation was rolled back.", cause);

internal static class MarketplaceCommit
{
    public static T Guard<T>(Habbo habbo, Func<T> action)
    {
        try {
            return action();
        }
        catch (MarketplaceOutcomeUnknownException) {
            habbo.CurrencyPersistenceUncertain = true;
            habbo.Client?.Disconnect();
            throw;
        }
    }

    public static void LockUser(IDbConnection connection, IDbTransaction transaction, int userId)
    {
        if (connection.QuerySingleOrDefault<int?>("SELECT id FROM users WHERE id=@userId FOR UPDATE", new { userId }, transaction) == null) {
            throw new InvalidOperationException("Marketplace user no longer exists.");
        }
    }

    public static void Complete(IDatabase database, IDbConnection connection, IDbTransaction transaction, int userId, string kind)
    {
        var reference = Guid.NewGuid().ToString();
        connection.Execute("INSERT INTO marketplace_operations(operation_ref,user_id,kind,created_at) VALUES(@reference,@userId,@kind,UTC_TIMESTAMP(6))",
            new { reference, userId, kind }, transaction);

        try {
            transaction.Commit();
        }
        catch (DbException error) {
            Rollback(transaction);

            try {
                connection.Close();
            }
            catch (Exception) { }

            bool committed;

            try {
                using var recovery = database.Connection();
                recovery.Open();
                using var recovered = recovery.BeginTransaction();
                LockUser(recovery, recovered, userId);
                committed = recovery.ExecuteScalar<int>("SELECT COUNT(*) FROM marketplace_operations WHERE operation_ref=@reference AND user_id=@userId AND kind=@kind",
                    new { reference, userId, kind }, recovered) == 1;
            }
            catch (Exception recoveryError) {
                throw new MarketplaceOutcomeUnknownException(new AggregateException(error, recoveryError));
            }

            if (!committed) {
                throw new MarketplaceCommitFailedException(error);
            }
        }
    }

    public static void Rollback(IDbTransaction transaction)
    {
        try {
            transaction.Rollback();
        }
        catch (Exception) { }
    }
}
