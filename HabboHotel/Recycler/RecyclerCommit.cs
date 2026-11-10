using System.Data;
using System.Data.Common;

namespace Plus.HabboHotel.Recycler;

internal sealed class RecyclerOutcomeUnknownException(Exception cause) : Exception("Recycler transaction outcome could not be recovered.", cause);

internal static class RecyclerCommit
{
    public static bool Complete(IDbConnection connection, IDbTransaction transaction, Func<bool> recover)
    {
        try {
            transaction.Commit();

            return true;
        }
        catch (DbException commitError) {
            try {
                transaction.Rollback();
            }
            catch (Exception) { }

            try {
                connection.Close();
            }
            catch (Exception) { }

            try {
                return recover();
            }
            catch (DbException recoveryError) {
                throw new RecyclerOutcomeUnknownException(new AggregateException(commitError, recoveryError));
            }
        }
    }
}
