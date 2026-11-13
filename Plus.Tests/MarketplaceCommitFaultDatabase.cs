using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Plus.Database;

namespace Plus.Tests;

internal sealed class MarketplaceCommitFaultDatabase(IDatabase source, bool commit, bool recoveryAvailable = true) : IDatabase
{
    private int _connections;
    public bool IsConnected() => source.IsConnected();
    public IDbConnection Connection()
    {
        if (Interlocked.Increment(ref _connections) > 1) {
            return recoveryAvailable ? source.Connection() : throw new CommitFailure();
        }

        return new ConnectionWrapper(source.Connection(), commit);
    }

    internal sealed class CommitFailure() : DbException("Connection lost during commit.");

    private sealed class ConnectionWrapper(IDbConnection inner, bool commit) : IDbConnection
    {
        [AllowNull] public string ConnectionString { get => inner.ConnectionString; set => inner.ConnectionString = value; }
        public int ConnectionTimeout => inner.ConnectionTimeout;
        public string Database => inner.Database;
        public ConnectionState State => inner.State;
        public IDbTransaction BeginTransaction() => new TransactionWrapper(inner.BeginTransaction(), this, commit);
        public IDbTransaction BeginTransaction(IsolationLevel level) => new TransactionWrapper(inner.BeginTransaction(level), this, commit);
        public void ChangeDatabase(string name) => inner.ChangeDatabase(name);
        public void Close() => inner.Close();
        public IDbCommand CreateCommand() => new CommandWrapper(inner.CreateCommand());
        public void Open() => inner.Open();
        public void Dispose() => inner.Dispose();
    }

    private sealed class TransactionWrapper(IDbTransaction inner, IDbConnection connection, bool commit) : IDbTransaction
    {
        public IDbTransaction Inner => inner;
        public IDbConnection Connection => connection;
        public IsolationLevel IsolationLevel => inner.IsolationLevel;
        public void Commit()
        {
            if (commit) {
                inner.Commit();
            }
            else {
                inner.Rollback();
            }

            throw new CommitFailure();
        }
        public void Rollback() => inner.Rollback();
        public void Dispose() => inner.Dispose();
    }

    private sealed class CommandWrapper(IDbCommand inner) : IDbCommand
    {
        [AllowNull] public string CommandText { get => inner.CommandText; set => inner.CommandText = value; }
        public int CommandTimeout { get => inner.CommandTimeout; set => inner.CommandTimeout = value; }
        public CommandType CommandType { get => inner.CommandType; set => inner.CommandType = value; }
        public IDbConnection? Connection { get => inner.Connection; set => inner.Connection = value; }
        public IDataParameterCollection Parameters => inner.Parameters;
        public IDbTransaction? Transaction { get => inner.Transaction; set => inner.Transaction = value is TransactionWrapper wrapped ? wrapped.Inner : value; }
        public UpdateRowSource UpdatedRowSource { get => inner.UpdatedRowSource; set => inner.UpdatedRowSource = value; }
        public void Cancel() => inner.Cancel();
        public IDbDataParameter CreateParameter() => inner.CreateParameter();
        public int ExecuteNonQuery() => inner.ExecuteNonQuery();
        public IDataReader ExecuteReader() => inner.ExecuteReader();
        public IDataReader ExecuteReader(CommandBehavior behavior) => inner.ExecuteReader(behavior);
        public object? ExecuteScalar() => inner.ExecuteScalar();
        public void Prepare() => inner.Prepare();
        public void Dispose() => inner.Dispose();
    }
}
