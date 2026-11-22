using System.Data;
using Dapper;
using MySqlConnector;
using Plus.Core;
using Plus.Database;

namespace Plus.HabboHotel.Rooms.Chat.Logs;

public sealed class ChatlogManager : IChatlogManager
{
    private readonly IDatabase _database;
    private const int FlushOnCount = 10;

    private const string InsertQuery = """
        INSERT INTO chatlogs (user_id, room_id, `timestamp`, message)
        VALUES (@PlayerId, @RoomId, @CreatedAt, @Message)
        """;

    private readonly List<ChatlogEntry> _chatlogs;
    private readonly ReaderWriterLockSlim _lock;

    public ChatlogManager(IDatabase database)
    {
        _database = database;
        _chatlogs = new();
        _lock = new(LockRecursionPolicy.NoRecursion);
    }

    public void StoreChatlog(ChatlogEntry entry)
    {
        _lock.EnterUpgradeableReadLock();

        try {
            _chatlogs.Add(entry);
            OnChatlogStore();
        }
        finally {
            _lock.ExitUpgradeableReadLock();
        }
    }

    private void OnChatlogStore()
    {
        if (_chatlogs.Count >= FlushOnCount) {
            FlushAndSave();
        }
    }

    public void FlushAndSave()
    {
        _lock.EnterWriteLock();

        try {
            if (_chatlogs.Count == 0) {
                return;
            }

            using var connection = _database.Connection();
            connection.Open();

            try {
                using var transaction = connection.BeginTransaction();

                foreach (var entry in _chatlogs) {
                    Insert(connection, entry, transaction);
                }

                transaction.Commit();
            }
            catch (Exception) {
                // One refused entry fails the whole batch, so each entry is tried once on its own.
                foreach (var entry in _chatlogs) {
                    TryInsert(connection, entry);
                }
            }
        }
        catch (Exception exception) {
            ExceptionLogger.LogCriticalException(exception);
        }
        finally {
            _chatlogs.Clear();
            _lock.ExitWriteLock();
        }
    }

    private static void TryInsert(IDbConnection connection, ChatlogEntry entry)
    {
        try {
            Insert(connection, entry);
        }
        catch (Exception exception) {
            // The exception is not logged, because its message can quote the refused text.
            var reason = exception is MySqlException mysql ? mysql.ErrorCode.ToString() : exception.GetType().Name;
            ExceptionLogger.LogRefusedQuery(InsertQuery, $"{reason}, user {entry.PlayerId}, room {entry.RoomId}");
        }
    }

    private static void Insert(IDbConnection connection, ChatlogEntry entry, IDbTransaction? transaction = null)
    {
        connection.Execute(InsertQuery, new
        {
            entry.PlayerId,
            entry.RoomId,
            CreatedAt = entry.CreatedAt.UtcDateTime,
            entry.Message
        }, transaction);
    }
}
