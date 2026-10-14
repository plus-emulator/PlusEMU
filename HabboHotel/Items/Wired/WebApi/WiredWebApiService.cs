using Plus.Database;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.WebApi;

/// <summary>An HTTP answer: the status, an error code and message for failures, or the body.</summary>
public sealed record WiredWebApiResult(int Status, object? Body, string? Code = null, string? Error = null)
{
    public static WiredWebApiResult Ok(object body) => new(200, body);
    public static WiredWebApiResult Fail(int status, string code, string error) => new(status, null, code, error);
}

public interface IWiredWebApiService
{
    WiredWebApiResult Read(string key);
    WiredWebApiResult Write(string key, string variableId, long value, int holderId);
    WiredWebApiResult Clear(string key, string variableId);
}

/// <summary>
/// The room variables a Variables Web API add-on opens to HTTP clients. Either key reads; the write key sets values; only a write key
/// whose add-on allows bulk delete clears every holder of a variable. The add-on must still stand in its room and the room must be
/// loaded, since live values are the room's own.
/// </summary>
public sealed class WiredWebApiService(IDatabase database, IRoomManager rooms) : IWiredWebApiService
{
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string RoomNotLoaded = "room_not_loaded";
    public const string UnknownVariable = "unknown_variable";
    public const string Rejected = "rejected";

    private readonly IWiredWebApiStore _store = new DatabaseWiredWebApiStore(database);

    internal IWiredWebApiStore? StoreOverride { get; init; }
    private IWiredWebApiStore Store => StoreOverride ?? _store;

    private WiredWebApiResult? Authorize(string key, bool write, out WiredVariableMenu menu, out WiredWebApiGrant grant, bool bulk = false)
    {
        menu = null!;
        grant = Store.Find(key)!;

        if (grant == null) {
            return WiredWebApiResult.Fail(401, Unauthorized, "Unknown key.");
        }

        if (write && !grant.Write) {
            return WiredWebApiResult.Fail(403, Forbidden, "This key cannot change variables.");
        }

        if (bulk && !grant.BulkDelete) {
            return WiredWebApiResult.Fail(403, Forbidden, "This add-on does not allow bulk delete.");
        }

        if (!rooms.TryGetRoom(grant.RoomId, out var room)) {
            return WiredWebApiResult.Fail(409, RoomNotLoaded, "The room is not loaded.");
        }

        if (room.GetRoomItemHandler()?.GetItem(grant.ItemId) is not { } item
            || item.Definition.WiredDescriptor?.CanonicalName != "wf_xtra_var_web_api") {
            return WiredWebApiResult.Fail(401, Unauthorized, "Unknown key.");
        }

        menu = new(room, room.GetWired().Variables);

        return null;
    }

    private static string TargetName(WiredVariableTarget target) => target switch
    {
        WiredVariableTarget.User => "user",
        WiredVariableTarget.Furni => "furni",
        WiredVariableTarget.Global => "room",
        _ => "context"
    };

    public WiredWebApiResult Read(string key)
    {
        if (Authorize(key, false, out var menu, out var grant) is { } failure) {
            return failure;
        }

        var snapshot = menu.Snapshot();

        return WiredWebApiResult.Ok(new
        {
            roomId = grant.RoomId,
            variables = snapshot.Definitions.Where(variable => variable.Definition.Target != WiredVariableTarget.Context).Select(variable => new
            {
                id = variable.CatalogId,
                name = variable.Definition.Name,
                target = TargetName(variable.Definition.Target),
                hasValue = variable.HasValue,
                writable = variable.CanWriteValue,
                holders = snapshot.Assignments.Where(holder => holder.Key.DefinitionId == variable.Definition.ItemId && holder.Key.Target == variable.Definition.Target)
                    .Select(holder => new
                    {
                        holderId = holder.Key.HolderId,
                        value = holder.Value.Value,
                        createdAt = holder.Value.CreatedAt?.ToUnixTimeMilliseconds(),
                        updatedAt = holder.Value.UpdatedAt?.ToUnixTimeMilliseconds()
                    })
            })
        });
    }

    private static WiredVariableDescription? Find(WiredVariableMenu menu, string variableId) =>
        menu.Catalog().Find(variableId) is { Definition.Target: not WiredVariableTarget.Context, IsBuiltin: false, IsDerived: false } variable ? variable : null;

    public WiredWebApiResult Write(string key, string variableId, long value, int holderId)
    {
        if (Authorize(key, true, out var menu, out _) is { } failure) {
            return failure;
        }

        if (Find(menu, variableId) is not { } variable) {
            return WiredWebApiResult.Fail(404, UnknownVariable, "No such variable in this room.");
        }

        return variable.CanWriteValue && menu.Write(variable.Definition.Target, variable.Definition.Target == WiredVariableTarget.Global ? 0 : holderId,
            variable.Definition.ItemId, value, WiredVariableMutation.Set)
            ? WiredWebApiResult.Ok(new { id = variable.CatalogId, holderId, value })
            : WiredWebApiResult.Fail(422, Rejected, "The variable value could not be changed.");
    }

    public WiredWebApiResult Clear(string key, string variableId)
    {
        if (Authorize(key, true, out var menu, out _, bulk: true) is { } failure) {
            return failure;
        }

        if (Find(menu, variableId) is not { Definition.Target: WiredVariableTarget.User or WiredVariableTarget.Furni } variable) {
            return WiredWebApiResult.Fail(404, UnknownVariable, "No such user or furni variable in this room.");
        }

        return WiredWebApiResult.Ok(new { id = variable.CatalogId, removed = menu.Clear(variable.Definition.Target, variable.Definition.ItemId) });
    }
}
