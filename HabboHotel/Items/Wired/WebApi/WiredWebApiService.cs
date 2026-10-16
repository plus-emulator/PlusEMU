using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Plus.Database;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.WebApi;

/// <summary>An HTTP answer: the status and the JSON body (none for 204).</summary>
public sealed record WiredWebApiResult(int Status, object? Body)
{
    public static WiredWebApiResult Ok(object body) => new(200, body);
    public static readonly WiredWebApiResult NoContent = new(204, null);
    public static WiredWebApiResult Error(int status, string error) => new(status, new Dictionary<string, string> { ["error"] = error });
    public string? ErrorCode => Body is Dictionary<string, string> body && body.TryGetValue("error", out var code) ? code : null;
}

/// <summary>The keys a request carries: X-Wired-Read-Key and X-Wired-Write-Key.</summary>
public readonly record struct WiredWebApiCredentials(string? ReadKey, string? WriteKey);

/// <summary>
/// A value of a write body (WiredValueWrite, WiredVariablesPatch): a signed 64-bit decimal string. Habbo answers a body without one
/// 400 wired.variables.invalid_value and one that is not such a string 400 request-failed-non-200 (observed on habbo.com).
/// </summary>
public readonly record struct WiredWebApiInput(long Value, string? Error)
{
    public static WiredWebApiInput Missing => new(0, WiredWebApiErrors.InvalidValue);
    public static WiredWebApiInput Malformed => new(0, WiredWebApiErrors.RequestFailed);
    public static WiredWebApiInput Of(long value) => new(value, null);
}

/// <summary>One entry of a batch request (WiredBatchOperationRequest).</summary>
public sealed record WiredWebApiBatchOperation(string? OpId, string? Method, string? Path, WiredWebApiInput Body);

/// <summary>Query parameters of the paged list.</summary>
public sealed record WiredWebApiPaging(string? OrderBy, string? OrderDir, string? Page, string? Size);

/// <summary>Error bodies of Habbo's public Wired Variables Web API: the spec's examples and what habbo.com answers.</summary>
public static class WiredWebApiErrors
{
    public const string KeyMissing = "wired.variables.key_missing";
    public const string KeyInvalid = "wired.variables.key_invalid";
    public const string ApiDisabled = "wired.variables.api_disabled";
    public const string InvalidTarget = "wired.variables.invalid_target";
    public const string InvalidValue = "wired.variables.invalid_value";
    public const string OperationNotAllowed = "wired.variables.operation_not_allowed";
    public const string UserNotParticipating = "wired.variables.user_not_participating";
    public const string BulkDeleteEmpty = "wired.variables.bulk_delete_empty";
    public const string BulkDeleteInvalidVariable = "wired.variables.bulk_delete_invalid_variable";
    public const string BulkDeleteNotEnabled = "wired.variables.bulk_delete_not_enabled";
    public const string BatchEmpty = "wired.variables.batch_empty";
    public const string BatchLimitExceeded = "wired.variables.batch_limit_exceeded";
    public const string EntityNotFound = "wired.variables.entity_not_found";
    public const string NotFound = "wired.variables.not_found";
    public const string TooManyRequests = "wired.variables.too_many_requests";

    /// <summary>A room the add-on's room is not loaded for (the spec's WiredRoomNotFoundError).</summary>
    public const string RoomNotFound = "room.not_found";

    /// <summary>A room id that names no room, or an entity id that is not a number (observed on habbo.com).</summary>
    public const string Missing = "not-found";

    /// <summary>A write value that is present but not a signed 64-bit decimal string (observed on habbo.com).</summary>
    public const string RequestFailed = "request-failed-non-200";
}

public interface IWiredWebApiService
{
    WiredWebApiResult ListVariables(string roomId, WiredWebApiCredentials keys);
    WiredWebApiResult ReadValue(string roomId, string scope, string name, string kind, string entityId, WiredWebApiCredentials keys);
    WiredWebApiResult WriteValue(string roomId, string scope, string name, string kind, string entityId, WiredWebApiWriteMode mode, WiredWebApiInput body, WiredWebApiCredentials keys);
    WiredWebApiResult DeleteValue(string roomId, string scope, string name, string kind, string entityId, WiredWebApiCredentials keys);
    WiredWebApiResult ListValues(string roomId, string scope, string name, string kind, WiredWebApiPaging paging, WiredWebApiCredentials keys);
    WiredWebApiResult CountValues(string roomId, string scope, string name, string kind, WiredWebApiCredentials keys);
    WiredWebApiResult BulkDelete(string roomId, IReadOnlyList<string>? names, WiredWebApiCredentials keys);
    WiredWebApiResult Batch(string roomId, string scope, string name, IReadOnlyList<WiredWebApiBatchOperation>? operations, WiredWebApiCredentials keys);
    WiredWebApiResult ReadGlobal(string roomId, string name, WiredWebApiCredentials keys);
    WiredWebApiResult WriteGlobal(string roomId, string name, WiredWebApiInput body, WiredWebApiCredentials keys);
    WiredWebApiResult ReadUserProfileByName(string roomId, string? name, string? uniqueId, WiredWebApiCredentials keys);
    WiredWebApiResult ReadProfile(string roomId, string scope, string kind, string entityId, WiredWebApiCredentials keys);
    WiredWebApiResult PatchProfile(string roomId, string scope, string kind, string entityId, IReadOnlyDictionary<string, WiredWebApiInput?>? variables, WiredWebApiCredentials keys);
    WiredWebApiResult DeleteProfile(string roomId, string kind, string entityId, WiredWebApiCredentials keys);
    WiredWebApiResult ReadGlobalProfile(string roomId, WiredWebApiCredentials keys);
    WiredWebApiResult PatchGlobalProfile(string roomId, IReadOnlyDictionary<string, WiredWebApiInput?>? variables, WiredWebApiCredentials keys);
}

/// <summary>
/// Habbo's public Wired Variables Web API (habbo.com /api/public/api-docs/, tag "Wired Variables") over the room's variables, opened by
/// the keys of the room's Variables Web API add-on (VariablesWebApiAddon.as, WIN63-202610131318-860371703).
/// Checks run in habbo.com's order: the room exists, the path and body are well formed, then the key, the rate limit, the loaded room
/// and the add-on; only then is a variable or holder looked up.
/// </summary>
public sealed partial class WiredWebApiService : IWiredWebApiService
{
    public const int BatchLimit = 50;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    private readonly IWiredWebApiStore _store;
    private readonly IWiredWebApiDirectory _directory;
    private readonly WiredWebApiRateLimit _limit;

    public WiredWebApiService(IDatabase database, IRoomManager rooms)
        : this(new DatabaseWiredWebApiStore(database), new DatabaseWiredWebApiDirectory(database, rooms), TimeProvider.System) { }

    internal WiredWebApiService(IWiredWebApiStore store, IWiredWebApiDirectory directory, TimeProvider clock)
    {
        _store = store;
        _directory = directory;
        _limit = new(clock);
    }

    private enum Need
    {
        Read,
        Write,
        Either
    }

    private sealed record Access(IWiredWebApiRoom Room, WiredWebApiKeys? Read, WiredWebApiKeys? Write, string? ReadError, string? WriteError);

    private static WiredWebApiResult Missing() => WiredWebApiResult.Error(404, WiredWebApiErrors.Missing);
    private static WiredWebApiResult Invalid(string error = WiredWebApiErrors.InvalidTarget) => WiredWebApiResult.Error(400, error);
    private static WiredWebApiResult NotFound(string error = WiredWebApiErrors.NotFound) => WiredWebApiResult.Error(404, error);
    private static WiredWebApiResult Forbidden(string error) => WiredWebApiResult.Error(403, error);

    [GeneratedRegex("^[A-Za-z0-9_]{1,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    private static bool ValidName(string name) => NamePattern().IsMatch(name);

    private WiredWebApiResult Run(string roomIdText, Func<WiredWebApiResult?> validate, Need need, WiredWebApiCredentials keys, Func<Access, WiredWebApiResult> handle)
    {
        if (!uint.TryParse(roomIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var roomId) || roomId == 0 || !_directory.RoomExists(roomId)) {
            return Missing();
        }

        if (validate() is { } invalid) {
            return invalid;
        }

        var rows = _store.RoomKeys(roomId);
        var (read, readError) = Match(rows, keys.ReadKey, row => row.ReadHash);
        var (write, writeError) = Match(rows, keys.WriteKey, row => row.WriteHash);

        // A batch needs one of the two keys; each of its operations then needs its own (GET the read key, the others the write key).
        var refused = need switch
        {
            Need.Read => readError,
            Need.Write => writeError,
            _ when read is not null || write is not null => null,
            _ => string.IsNullOrEmpty(keys.WriteKey) || !string.IsNullOrEmpty(keys.ReadKey) ? readError : writeError
        };

        if (refused is not null) {
            return Forbidden(refused);
        }

        var granted = need == Need.Write ? write! : read ?? write!;
        var limitedKey = need != Need.Write && read is not null ? read.ReadHash! : granted.WriteHash!;

        if (!_limit.Admit(roomId, limitedKey)) {
            return WiredWebApiResult.Error(429, WiredWebApiErrors.TooManyRequests);
        }

        if (_directory.LoadedRoom(roomId) is not { } room) {
            return NotFound(WiredWebApiErrors.RoomNotFound);
        }

        return room.HasAddon(granted.ItemId) ? handle(new(room, read, write, readError, writeError)) : Forbidden(WiredWebApiErrors.ApiDisabled);
    }

    // A room without a key of that kind answers key_missing whatever is sent (observed on habbo.com for a room without the add-on).
    private static (WiredWebApiKeys? Row, string? Error) Match(IReadOnlyList<WiredWebApiKeys> rows, string? key, Func<WiredWebApiKeys, string?> hash)
    {
        var configured = rows.Where(row => hash(row) is not null).ToArray();

        if (string.IsNullOrEmpty(key) || configured.Length == 0) {
            return (null, WiredWebApiErrors.KeyMissing);
        }

        var sent = Encoding.ASCII.GetBytes(DatabaseWiredWebApiStore.Hash(key));
        var row = configured.FirstOrDefault(row => CryptographicOperations.FixedTimeEquals(sent, Encoding.ASCII.GetBytes(hash(row)!)));

        return row is null ? (null, WiredWebApiErrors.KeyInvalid) : (row, null);
    }

    private static WiredWebApiVariable? Variable(IWiredWebApiRoom room, WiredVariableTarget target, string name) =>
        room.Variables().FirstOrDefault(x => x.Target == target && x.Name == name);

    private static WiredVariableTarget ScopeTarget(string scope) => scope == "furni" ? WiredVariableTarget.Furni : WiredVariableTarget.User;

    // Path checks before the key: a target kind outside the scope is invalid_target, an entity id that is no number is not-found.
    private static WiredWebApiResult? Target(string scope, string name, string kind, string? entityId, out WiredWebApiTargetKind targetKind, out long id)
    {
        id = 0;

        if (!WiredWebApiTargetKinds.TryParse(scope, kind, out targetKind) || !ValidName(name)) {
            return Invalid();
        }

        return entityId is null || WiredWebApiTargetKinds.TryParseEntityId(entityId, out id) ? null : Missing();
    }

    public WiredWebApiResult ListVariables(string roomId, WiredWebApiCredentials keys) => Run(roomId, () => null, Need.Read, keys, access =>
    {
        var variables = access.Room.Variables();
        string[] Names(WiredVariableTarget target) => variables.Where(x => x.Target == target).Select(x => x.Name).Distinct().ToArray();

        return WiredWebApiResult.Ok(new Dictionary<string, string[]>
        {
            ["users"] = Names(WiredVariableTarget.User),
            ["furni"] = Names(WiredVariableTarget.Furni),
            ["global"] = Names(WiredVariableTarget.Global)
        });
    });

    public WiredWebApiResult ReadValue(string roomId, string scope, string name, string kind, string entityId, WiredWebApiCredentials keys)
    {
        WiredWebApiTargetKind targetKind = default;
        long id = 0;

        return Run(roomId, () => Target(scope, name, kind, entityId, out targetKind, out id), Need.Read, keys,
            access => ReadEntry(access.Room, ScopeTarget(scope), name, targetKind, id));
    }

    private static WiredWebApiResult ReadEntry(IWiredWebApiRoom room, WiredVariableTarget target, string name, WiredWebApiTargetKind kind, long id)
    {
        var variable = Variable(room, target, name);
        // A user who left keeps their permanent values under their user id.
        var entity = room.Find(kind, id);

        if (variable is null || entity is null && kind != WiredWebApiTargetKind.Users) {
            return NotFound();
        }

        return room.Values(variable).TryGetValue(entity?.StorageId ?? id, out var value) ? WiredWebApiResult.Ok(WiredWebApiValue.From(variable, value)) : NotFound();
    }

    public WiredWebApiResult WriteValue(string roomId, string scope, string name, string kind, string entityId, WiredWebApiWriteMode mode,
        WiredWebApiInput body, WiredWebApiCredentials keys)
    {
        WiredWebApiTargetKind targetKind = default;
        long id = 0;

        return Run(roomId, () => Target(scope, name, kind, entityId, out targetKind, out id) ?? (body.Error is { } error ? Invalid(error) : null), Need.Write, keys,
            access => WriteEntry(access.Room, ScopeTarget(scope), name, targetKind, id, mode, body.Value));
    }

    private static WiredWebApiResult WriteEntry(IWiredWebApiRoom room, WiredVariableTarget target, string name, WiredWebApiTargetKind kind, long id,
        WiredWebApiWriteMode mode, long value)
    {
        if (Variable(room, target, name) is not { } variable) {
            return NotFound();
        }

        var entity = room.Find(kind, id);

        if (entity is null && (mode == WiredWebApiWriteMode.Patch || kind != WiredWebApiTargetKind.Users)) {
            return NotFound();
        }

        return room.Write(variable, entity, mode, value) switch
        {
            WiredWebApiWriteOutcome.Written => room.Values(variable).TryGetValue(entity!.StorageId, out var after)
                ? WiredWebApiResult.Ok(WiredWebApiValue.From(variable, after)) : NotFound(),
            WiredWebApiWriteOutcome.NoValue => NotFound(),
            WiredWebApiWriteOutcome.NotPresent => mode == WiredWebApiWriteMode.Put && kind == WiredWebApiTargetKind.Users
                ? Forbidden(WiredWebApiErrors.UserNotParticipating) : NotFound(),
            _ => Forbidden(WiredWebApiErrors.OperationNotAllowed)
        };
    }

    public WiredWebApiResult DeleteValue(string roomId, string scope, string name, string kind, string entityId, WiredWebApiCredentials keys)
    {
        WiredWebApiTargetKind targetKind = default;
        long id = 0;

        return Run(roomId, () => Target(scope, name, kind, entityId, out targetKind, out id), Need.Write, keys,
            access => DeleteEntry(access.Room, ScopeTarget(scope), name, targetKind, id));
    }

    private static WiredWebApiResult DeleteEntry(IWiredWebApiRoom room, WiredVariableTarget target, string name, WiredWebApiTargetKind kind, long id)
    {
        if (Variable(room, target, name) is not { } variable) {
            return NotFound();
        }

        var entity = room.Find(kind, id) ?? (kind == WiredWebApiTargetKind.Users && id > 0 ? new(kind, id, null, id, false) : null);

        if (entity is null || !room.Values(variable).ContainsKey(entity.StorageId)) {
            return NotFound();
        }

        return variable.ReadOnly ? Forbidden(WiredWebApiErrors.OperationNotAllowed) : room.Remove(variable, entity) ? WiredWebApiResult.NoContent : NotFound();
    }

    public WiredWebApiResult ListValues(string roomId, string scope, string name, string kind, WiredWebApiPaging paging, WiredWebApiCredentials keys)
    {
        WiredWebApiTargetKind targetKind = default;

        return Run(roomId, () => Target(scope, name, kind, null, out targetKind, out _), Need.Read, keys, access =>
        {
            if (Variable(access.Room, ScopeTarget(scope), name) is not { } variable) {
                return NotFound();
            }

            var page = Number(paging.Page, 1, 1, int.MaxValue);
            var size = Number(paging.Size, DefaultPageSize, 1, MaxPageSize);
            var descending = paging.OrderDir == "desc";
            var entries = Entries(access.Room, variable, targetKind);
            Func<(WiredWebApiEntity Entity, WiredVariableValue Value), long> key = paging.OrderBy switch
            {
                "value" => x => x.Value.Value,
                "update_time" => x => x.Value.UpdatedAt?.ToUnixTimeMilliseconds() ?? 0,
                _ => x => x.Value.CreatedAt?.ToUnixTimeMilliseconds() ?? 0
            };
            var ordered = (descending ? entries.OrderByDescending(key) : entries.OrderBy(key)).ThenBy(x => x.Entity.Id);
            var items = ordered.Skip((int)Math.Min((long)(page - 1) * size, int.MaxValue)).Take(size)
                .Select(x => Item(x.Entity.Id, WiredWebApiValue.From(variable, x.Value))).ToArray();

            return WiredWebApiResult.Ok(new Dictionary<string, object> { ["items"] = items, ["page"] = page, ["size"] = size });
        });
    }

    // A listed value: the WiredVariable fields with the holder's id.
    private static Dictionary<string, object> Item(long id, WiredWebApiValue value)
    {
        var item = new Dictionary<string, object> { ["id"] = id };

        if (value.Value is { } number) {
            item["value"] = number;
        }

        if (value.CreationTime is { } created) {
            item["creation_time"] = created;
        }

        if (value.UpdateTime is { } updated) {
            item["update_time"] = updated;
        }

        return item;
    }

    private static int Number(string? text, int fallback, int min, int max) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, min, max) : fallback;

    private static IReadOnlyList<(WiredWebApiEntity Entity, WiredVariableValue Value)> Entries(IWiredWebApiRoom room, WiredWebApiVariable variable, WiredWebApiTargetKind kind) =>
        room.Values(variable).Select(x => (Entity: room.FromStorage(kind, x.Key), x.Value)).Where(x => x.Entity is not null).Select(x => (x.Entity!, x.Value)).ToArray();

    public WiredWebApiResult CountValues(string roomId, string scope, string name, string kind, WiredWebApiCredentials keys)
    {
        WiredWebApiTargetKind targetKind = default;

        return Run(roomId, () => Target(scope, name, kind, null, out targetKind, out _), Need.Read, keys,
            access => Variable(access.Room, ScopeTarget(scope), name) is { } variable
                ? WiredWebApiResult.Ok(new Dictionary<string, int> { ["count"] = Entries(access.Room, variable, targetKind).Count })
                : NotFound());
    }

    public WiredWebApiResult BulkDelete(string roomId, IReadOnlyList<string>? names, WiredWebApiCredentials keys) =>
        Run(roomId, () => names is not { Count: > 0 } ? Invalid(WiredWebApiErrors.BulkDeleteEmpty) : null, Need.Write, keys, access =>
        {
            if (access.Write is not { BulkDelete: true }) {
                return Forbidden(WiredWebApiErrors.BulkDeleteNotEnabled);
            }

            var variables = access.Room.Variables().Where(x => x.Target is WiredVariableTarget.User or WiredVariableTarget.Furni).ToArray();
            var matched = names!.Distinct().Select(name => variables.Where(x => x.Name == name).ToArray()).ToArray();

            if (matched.Any(x => x.Length == 0 || x.Any(variable => variable.ReadOnly))) {
                return Invalid(WiredWebApiErrors.BulkDeleteInvalidVariable);
            }

            foreach (var variable in matched.SelectMany(x => x)) {
                access.Room.Clear(variable);
            }

            return WiredWebApiResult.NoContent;
        });

    public WiredWebApiResult Batch(string roomId, string scope, string name, IReadOnlyList<WiredWebApiBatchOperation>? operations, WiredWebApiCredentials keys) =>
        Run(roomId, () => operations is not { Count: > 0 } ? Invalid(WiredWebApiErrors.BatchEmpty)
            : operations.Count > BatchLimit ? Invalid(WiredWebApiErrors.BatchLimitExceeded) : null, Need.Either, keys, access =>
        {
            var target = ScopeTarget(scope);

            if (!ValidName(name) || Variable(access.Room, target, name) is null) {
                return NotFound();
            }

            var results = operations!.Select(operation => BatchResult(operation, Operate(access, scope, name, operation))).ToArray();

            return WiredWebApiResult.Ok(new Dictionary<string, object> { ["results"] = results });
        });

    private static WiredWebApiResult Operate(Access access, string scope, string name, WiredWebApiBatchOperation operation)
    {
        var parts = (operation.Path ?? "").Split('/');

        if (parts.Length != 2 || Target(scope, name, parts[0], parts[1], out var kind, out var id) is { } invalid) {
            return Invalid();
        }

        var write = operation.Method is "PUT" or "PATCH" or "DELETE";

        if (operation.Method is not ("GET" or "PUT" or "PATCH" or "DELETE")) {
            return Invalid();
        }

        if ((write ? access.WriteError : access.ReadError) is { } refused) {
            return Forbidden(refused);
        }

        var target = ScopeTarget(scope);

        return operation.Method switch
        {
            "GET" => ReadEntry(access.Room, target, name, kind, id),
            "DELETE" => DeleteEntry(access.Room, target, name, kind, id),
            _ => operation.Body.Error is { } error ? Invalid(error)
                : WriteEntry(access.Room, target, name, kind, id, operation.Method == "PUT" ? WiredWebApiWriteMode.Put : WiredWebApiWriteMode.Patch, operation.Body.Value)
        };
    }

    // WiredBatchOperationResult: {op_id, status, body} for 200, {op_id, status} for 204, {op_id, status, error: {code, message}} otherwise.
    private static Dictionary<string, object?> BatchResult(WiredWebApiBatchOperation operation, WiredWebApiResult result)
    {
        var entry = new Dictionary<string, object?> { ["op_id"] = operation.OpId, ["status"] = result.Status };

        if (result.ErrorCode is { } code) {
            entry["error"] = new Dictionary<string, string> { ["code"] = code, ["message"] = code };
        }
        else if (result.Body is not null) {
            entry["body"] = result.Body;
        }

        return entry;
    }

    public WiredWebApiResult ReadGlobal(string roomId, string name, WiredWebApiCredentials keys) => Run(roomId, () => null, Need.Read, keys,
        access => Variable(access.Room, WiredVariableTarget.Global, name) is { } variable && access.Room.Values(variable).TryGetValue(0, out var value)
            ? WiredWebApiResult.Ok(WiredWebApiValue.From(variable, value)) : NotFound());

    public WiredWebApiResult WriteGlobal(string roomId, string name, WiredWebApiInput body, WiredWebApiCredentials keys) =>
        Run(roomId, () => body.Error is { } error ? Invalid(error) : null, Need.Write, keys, access =>
        {
            if (Variable(access.Room, WiredVariableTarget.Global, name) is not { } variable) {
                return NotFound();
            }

            return access.Room.Write(variable, null, WiredWebApiWriteMode.Patch, body.Value) == WiredWebApiWriteOutcome.Written
                && access.Room.Values(variable).TryGetValue(0, out var after)
                ? WiredWebApiResult.Ok(WiredWebApiValue.From(variable, after)) : Forbidden(WiredWebApiErrors.OperationNotAllowed);
        });

    public WiredWebApiResult ReadUserProfileByName(string roomId, string? name, string? uniqueId, WiredWebApiCredentials keys) =>
        Run(roomId, () => string.IsNullOrEmpty(name) && string.IsNullOrEmpty(uniqueId) ? Invalid() : null, Need.Read, keys, access =>
        {
            // This hotel has no unique ids; a user is looked up by name.
            if (string.IsNullOrEmpty(name) || _directory.UserByName(name) is not { } user) {
                return NotFound(WiredWebApiErrors.EntityNotFound);
            }

            var entity = access.Room.Find(WiredWebApiTargetKind.Users, user.Id) ?? new(WiredWebApiTargetKind.Users, user.Id, user.Name, user.Id, false);

            return WiredWebApiResult.Ok(Profile(access.Room, entity with { Name = user.Name }));
        });

    // Profile kinds: users, pets and bots under user; the four furni kinds under furni.
    private WiredWebApiEntity? ProfileEntity(IWiredWebApiRoom room, WiredWebApiTargetKind kind, long id)
    {
        if (room.Find(kind, id) is { } entity) {
            return entity;
        }

        return kind == WiredWebApiTargetKind.Users && id > 0 && _directory.UserName(id) is { } name ? new(kind, id, name, id, false) : null;
    }

    private static Dictionary<string, object> Profile(IWiredWebApiRoom room, WiredWebApiEntity entity)
    {
        var target = new Dictionary<string, object> { ["id"] = entity.Id };

        if (entity.Kind is WiredWebApiTargetKind.Users or WiredWebApiTargetKind.Pets or WiredWebApiTargetKind.Bots && entity.Name is { } name) {
            target["name"] = name;
        }

        return new()
        {
            [WiredWebApiTargetKinds.ProfileProperty(entity.Kind)] = target,
            ["variables"] = Held(room, entity)
        };
    }

    private static Dictionary<string, WiredWebApiValue> Held(IWiredWebApiRoom room, WiredWebApiEntity? entity)
    {
        var held = new Dictionary<string, WiredWebApiValue>(StringComparer.Ordinal);
        var target = entity is null ? WiredVariableTarget.Global : WiredWebApiTargetKinds.Scope(entity.Kind);

        foreach (var variable in room.Variables().Where(x => x.Target == target)) {
            if (!held.ContainsKey(variable.Name) && room.Values(variable).TryGetValue(entity?.StorageId ?? 0, out var value)) {
                held[variable.Name] = WiredWebApiValue.From(variable, value);
            }
        }

        return held;
    }

    private static WiredWebApiResult? ProfileTarget(string scope, string kind, string entityId, out WiredWebApiTargetKind targetKind, out long id)
    {
        id = 0;

        if (!WiredWebApiTargetKinds.TryParse(scope, kind, out targetKind)) {
            return Invalid();
        }

        return WiredWebApiTargetKinds.TryParseEntityId(entityId, out id) ? null : Missing();
    }

    public WiredWebApiResult ReadProfile(string roomId, string scope, string kind, string entityId, WiredWebApiCredentials keys)
    {
        WiredWebApiTargetKind targetKind = default;
        long id = 0;

        return Run(roomId, () => ProfileTarget(scope, kind, entityId, out targetKind, out id), Need.Read, keys,
            access => ProfileEntity(access.Room, targetKind, id) is { } entity ? WiredWebApiResult.Ok(Profile(access.Room, entity)) : NotFound(WiredWebApiErrors.EntityNotFound));
    }

    // WiredVariablesPatch maps names to a decimal string, or null to remove (user and furni only); WiredGlobalVariablesPatch has no nulls.
    private static WiredWebApiResult? PatchBody(IReadOnlyDictionary<string, WiredWebApiInput?>? variables, bool allowNull)
    {
        if (variables is null) {
            return Invalid(WiredWebApiErrors.InvalidValue);
        }

        foreach (var input in variables.Values) {
            if (input is null ? !allowNull : input.Value.Error is not null) {
                return Invalid(input?.Error ?? WiredWebApiErrors.RequestFailed);
            }
        }

        return null;
    }

    public WiredWebApiResult PatchProfile(string roomId, string scope, string kind, string entityId, IReadOnlyDictionary<string, WiredWebApiInput?>? variables,
        WiredWebApiCredentials keys)
    {
        WiredWebApiTargetKind targetKind = default;
        long id = 0;

        return Run(roomId, () => ProfileTarget(scope, kind, entityId, out targetKind, out id) ?? PatchBody(variables, true), Need.Write, keys, access =>
        {
            var room = access.Room;

            if (ProfileEntity(room, targetKind, id) is not { } entity) {
                return NotFound(WiredWebApiErrors.EntityNotFound);
            }

            var target = WiredWebApiTargetKinds.Scope(targetKind);
            var changes = new List<(WiredWebApiVariable Variable, WiredWebApiInput? Input)>();

            foreach (var (name, input) in variables!) {
                if (Variable(room, target, name) is not { } variable || input is null && !room.Values(variable).ContainsKey(entity.StorageId)) {
                    return NotFound();
                }

                if (variable.ReadOnly) {
                    return Forbidden(WiredWebApiErrors.OperationNotAllowed);
                }

                changes.Add((variable, input));
            }

            foreach (var (variable, input) in changes) {
                var failure = input is { } value
                    ? room.Write(variable, entity, WiredWebApiWriteMode.Put, value.Value) switch
                    {
                        WiredWebApiWriteOutcome.Written => null,
                        WiredWebApiWriteOutcome.NotPresent => targetKind == WiredWebApiTargetKind.Users ? Forbidden(WiredWebApiErrors.UserNotParticipating) : NotFound(WiredWebApiErrors.EntityNotFound),
                        _ => Forbidden(WiredWebApiErrors.OperationNotAllowed)
                    }
                    : room.Remove(variable, entity) ? null : NotFound();

                if (failure is not null) {
                    return failure;
                }
            }

            return WiredWebApiResult.Ok(Profile(room, entity));
        });
    }

    public WiredWebApiResult DeleteProfile(string roomId, string kind, string entityId, WiredWebApiCredentials keys)
    {
        WiredWebApiTargetKind targetKind = default;
        long id = 0;

        return Run(roomId, () => ProfileTarget("user", kind, entityId, out targetKind, out id), Need.Write, keys, access =>
        {
            var room = access.Room;

            if (ProfileEntity(room, targetKind, id) is not { } entity) {
                return NotFound(WiredWebApiErrors.EntityNotFound);
            }

            var held = room.Variables().Where(x => x.Target == WiredVariableTarget.User && room.Values(x).ContainsKey(entity.StorageId)).ToArray();

            if (held.Length == 0) {
                return NotFound();
            }

            return held.Count(variable => room.Remove(variable, entity)) > 0 ? WiredWebApiResult.NoContent : Forbidden(WiredWebApiErrors.OperationNotAllowed);
        });
    }

    public WiredWebApiResult ReadGlobalProfile(string roomId, WiredWebApiCredentials keys) => Run(roomId, () => null, Need.Read, keys,
        access => WiredWebApiResult.Ok(new Dictionary<string, object> { ["variables"] = Held(access.Room, null) }));

    public WiredWebApiResult PatchGlobalProfile(string roomId, IReadOnlyDictionary<string, WiredWebApiInput?>? variables, WiredWebApiCredentials keys) =>
        Run(roomId, () => PatchBody(variables, false), Need.Write, keys, access =>
        {
            var room = access.Room;
            var changes = new List<(WiredWebApiVariable Variable, long Value)>();

            foreach (var (name, input) in variables!) {
                if (Variable(room, WiredVariableTarget.Global, name) is not { } variable) {
                    return NotFound();
                }

                if (variable.ReadOnly || !variable.HasValue) {
                    return Forbidden(WiredWebApiErrors.OperationNotAllowed);
                }

                changes.Add((variable, input!.Value.Value));
            }

            foreach (var (variable, value) in changes) {
                if (room.Write(variable, null, WiredWebApiWriteMode.Patch, value) != WiredWebApiWriteOutcome.Written) {
                    return Forbidden(WiredWebApiErrors.OperationNotAllowed);
                }
            }

            return WiredWebApiResult.Ok(new Dictionary<string, object> { ["variables"] = Held(room, null) });
        });
}

/// <summary>
/// A fixed window per key: 60 requests every 10 seconds, after which the key is answered 429 wired.variables.too_many_requests until
/// the window ends. Habbo's own numbers are not published.
/// </summary>
internal sealed class WiredWebApiRateLimit(TimeProvider clock)
{
    public const int Requests = 60;
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    private readonly Dictionary<(uint RoomId, string Key), (DateTimeOffset Start, int Count)> _windows = [];
    private readonly object _gate = new();

    public bool Admit(uint roomId, string keyHash)
    {
        var now = clock.GetUtcNow();

        lock (_gate) {
            if (_windows.Count > 4096) {
                foreach (var stale in _windows.Where(x => now - x.Value.Start >= Window).Select(x => x.Key).ToArray()) {
                    _windows.Remove(stale);
                }
            }

            var window = _windows.GetValueOrDefault((roomId, keyHash));

            if (window.Count == 0 || now - window.Start >= Window) {
                window = (now, 0);
            }

            if (window.Count >= Requests) {
                return false;
            }

            _windows[(roomId, keyHash)] = (window.Start, window.Count + 1);

            return true;
        }
    }
}

/// <summary>The rooms table, loaded rooms and user names for the Web API.</summary>
public sealed class DatabaseWiredWebApiDirectory(IDatabase database, IRoomManager rooms) : IWiredWebApiDirectory
{
    public bool RoomExists(uint roomId)
    {
        if (rooms.TryGetRoom(roomId, out _)) {
            return true;
        }

        using var connection = database.Connection();

        return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM rooms WHERE id = @RoomId", new { RoomId = roomId }) > 0;
    }

    public IWiredWebApiRoom? LoadedRoom(uint roomId) => rooms.TryGetRoom(roomId, out var room) ? new WiredWebApiRoom(room) : null;

    public (long Id, string Name)? UserByName(string name)
    {
        using var connection = database.Connection();
        var row = connection.QuerySingleOrDefault<(long Id, string Name)?>("SELECT id, username FROM users WHERE username = @Name LIMIT 1", new { Name = name });

        return row;
    }

    public string? UserName(long id)
    {
        using var connection = database.Connection();

        return connection.QuerySingleOrDefault<string?>("SELECT username FROM users WHERE id = @Id LIMIT 1", new { Id = id });
    }
}
