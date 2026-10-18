using System.Data;
using Dapper;

namespace Plus.Database.Migrator;

/// <summary>
/// The current database's schema, read once from information_schema, plus a connection for data probes.
/// </summary>
public sealed class SchemaSnapshot
{
    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;

    private readonly Dictionary<string, (string Engine, string Collation)> _tables = new(Names);
    private readonly Dictionary<(string, string), ColumnInfo> _columns = new();
    private readonly Dictionary<(string, string), List<string>> _indexes = new();
    private readonly HashSet<(string, string)> _foreignKeys = new();
    private readonly HashSet<string> _routines = new(Names);

    public SchemaSnapshot(IDbConnection connection)
    {
        Connection = connection;

        foreach (var row in connection.Query<(string Name, string? Engine, string? Collation)>(
                     "SELECT TABLE_NAME, ENGINE, TABLE_COLLATION FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE()")) {
            _tables[row.Name] = (row.Engine ?? string.Empty, row.Collation ?? string.Empty);
        }

        foreach (var row in connection.Query<ColumnRow>(
                     "SELECT TABLE_NAME AS TableName, COLUMN_NAME AS ColumnName, DATA_TYPE AS DataType, COLUMN_TYPE AS ColumnType, " +
                     "IS_NULLABLE AS IsNullable FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()")) {
            _columns[Key(row.TableName, row.ColumnName)] = new ColumnInfo(row.DataType.ToLowerInvariant(), row.ColumnType.ToLowerInvariant(), row.IsNullable == "YES");
        }

        foreach (var row in connection.Query<(string Table, string Index, string Column)>(
                     "SELECT TABLE_NAME, INDEX_NAME, COLUMN_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() " +
                     "ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX")) {
            var key = Key(row.Table, row.Index);

            if (!_indexes.TryGetValue(key, out var columns)) {
                _indexes[key] = columns = new List<string>();
            }

            columns.Add(row.Column.ToLowerInvariant());
        }

        foreach (var row in connection.Query<(string Table, string Name)>(
                     "SELECT TABLE_NAME, CONSTRAINT_NAME FROM information_schema.TABLE_CONSTRAINTS " +
                     "WHERE CONSTRAINT_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY'")) {
            _foreignKeys.Add(Key(row.Table, row.Name));
        }

        foreach (var name in connection.Query<string>("SELECT ROUTINE_NAME FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE()")) {
            _routines.Add(name);
        }
    }

    public IDbConnection Connection { get; }

    /// <summary>Every table except the migration ledger itself.</summary>
    public int TableCount => _tables.Keys.Count(t => !Names.Equals(t, SchemaMigrator.LedgerTable));

    public bool HasTable(string table) => _tables.ContainsKey(table);
    public string? TableEngine(string table) => _tables.TryGetValue(table, out var t) ? t.Engine : null;
    public string? TableCollation(string table) => _tables.TryGetValue(table, out var t) ? t.Collation : null;
    public ColumnInfo? Column(string table, string column) => _columns.TryGetValue(Key(table, column), out var c) ? c : null;
    public IReadOnlyList<string>? Index(string table, string index) => _indexes.TryGetValue(Key(table, index), out var c) ? c : null;
    public bool HasForeignKey(string table, string name) => _foreignKeys.Contains(Key(table, name));
    public bool HasAnyForeignKey(string table) => _foreignKeys.Any(k => Names.Equals(k.Item1, table.ToLowerInvariant()));
    public bool HasRoutine(string name) => _routines.Contains(name);

    private static (string, string) Key(string table, string name) => (table.ToLowerInvariant(), name.ToLowerInvariant());

    public sealed record ColumnInfo(string DataType, string ColumnType, bool Nullable);

    private sealed class ColumnRow
    {
        public string TableName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string ColumnType { get; set; } = string.Empty;
        public string IsNullable { get; set; } = string.Empty;
    }
}

/// <summary>
/// A predicate over the database that tells whether a legacy migration has already been applied.
/// </summary>
public abstract class SchemaProbe
{
    public abstract bool Evaluate(SchemaSnapshot schema);

    public static SchemaProbe Table(string table) => new Check($"table {table}", s => s.HasTable(table));
    public static SchemaProbe NoTable(string table) => new Check($"no table {table}", s => !s.HasTable(table));
    public static SchemaProbe Column(string table, string column) => new Check($"column {table}.{column}", s => s.Column(table, column) != null);

    public static SchemaProbe NoColumn(string table, string column) =>
        new Check($"no column {table}.{column}", s => s.HasTable(table) && s.Column(table, column) == null);

    public static SchemaProbe DataType(string table, string column, string dataType) =>
        new Check($"{table}.{column} is {dataType}", s => string.Equals(s.Column(table, column)?.DataType, dataType, StringComparison.OrdinalIgnoreCase));

    public static SchemaProbe ColumnType(string table, string column, string columnType) =>
        new Check($"{table}.{column} is {columnType}", s => string.Equals(s.Column(table, column)?.ColumnType, columnType, StringComparison.OrdinalIgnoreCase));

    public static SchemaProbe Nullable(string table, string column, bool nullable) =>
        new Check($"{table}.{column} nullable={nullable}", s => s.Column(table, column)?.Nullable == nullable);

    public static SchemaProbe Index(string table, string index) => new Check($"index {table}.{index}", s => s.Index(table, index) != null);
    public static SchemaProbe NoIndex(string table, string index) => new Check($"no index {table}.{index}", s => s.HasTable(table) && s.Index(table, index) == null);

    public static SchemaProbe PrimaryKey(string table, params string[] columns) =>
        new Check($"primary key {table}({string.Join(", ", columns)})",
            s => s.Index(table, "PRIMARY") is { } key && key.SequenceEqual(columns.Select(c => c.ToLowerInvariant())));

    public static SchemaProbe ForeignKey(string table, string name) => new Check($"foreign key {table}.{name}", s => s.HasForeignKey(table, name));
    public static SchemaProbe AnyForeignKey(string table) => new Check($"a foreign key on {table}", s => s.HasAnyForeignKey(table));
    public static SchemaProbe Engine(string table, string engine) => new Check($"{table} engine {engine}", s => string.Equals(s.TableEngine(table), engine, StringComparison.OrdinalIgnoreCase));

    public static SchemaProbe TableCollation(string table, string collation) =>
        new Check($"{table} collation {collation}", s => string.Equals(s.TableCollation(table), collation, StringComparison.OrdinalIgnoreCase));

    public static SchemaProbe Routine(string name) => new Check($"routine {name}", s => s.HasRoutine(name));

    /// <summary>A read-only scalar query; true when the first value is neither NULL nor zero.</summary>
    public static SchemaProbe Query(string sql) => new Check(sql, s =>
    {
        var value = s.Connection.ExecuteScalar(sql);

        return value is not null and not DBNull && Convert.ToDecimal(value) != 0;
    });

    public static SchemaProbe All(params SchemaProbe[] probes) => new Check("all", s => probes.All(p => p.Evaluate(s)));
    public static SchemaProbe Any(params SchemaProbe[] probes) => new Check("any", s => probes.Any(p => p.Evaluate(s)));
    public static SchemaProbe Not(SchemaProbe probe) => new Check("not", s => !probe.Evaluate(s));

    private sealed class Check(string description, Func<SchemaSnapshot, bool> predicate) : SchemaProbe
    {
        public override bool Evaluate(SchemaSnapshot schema) => predicate(schema);
        public override string ToString() => description;
    }
}
