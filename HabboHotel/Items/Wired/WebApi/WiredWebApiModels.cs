using System.Globalization;
using System.Text.Json.Serialization;
using Plus.HabboHotel.Items.Wired.Variables;

namespace Plus.HabboHotel.Items.Wired.WebApi;

/// <summary>
/// The {targetKind} path segment of Habbo's public Wired Variables Web API (components/parameters WiredTargetKind): users, pets and bots
/// for the user scope; furni, furni-bc, wall-items and wall-items-bc for the furni scope.
/// </summary>
public enum WiredWebApiTargetKind
{
    Users,
    Pets,
    Bots,
    Furni,
    FurniBc,
    WallItems,
    WallItemsBc
}

public static class WiredWebApiTargetKinds
{
    private static readonly Dictionary<string, WiredWebApiTargetKind> ByPath = new(StringComparer.Ordinal)
    {
        ["users"] = WiredWebApiTargetKind.Users,
        ["pets"] = WiredWebApiTargetKind.Pets,
        ["bots"] = WiredWebApiTargetKind.Bots,
        ["furni"] = WiredWebApiTargetKind.Furni,
        ["furni-bc"] = WiredWebApiTargetKind.FurniBc,
        ["wall-items"] = WiredWebApiTargetKind.WallItems,
        ["wall-items-bc"] = WiredWebApiTargetKind.WallItemsBc
    };

    /// <summary>A target kind of the given scope ("user" or "furni"); anything else is wired.variables.invalid_target.</summary>
    public static bool TryParse(string scope, string text, out WiredWebApiTargetKind kind) =>
        ByPath.TryGetValue(text, out kind) && (scope, Scope(kind)) is ("user", WiredVariableTarget.User) or ("furni", WiredVariableTarget.Furni);

    public static WiredVariableTarget Scope(WiredWebApiTargetKind kind) =>
        kind is WiredWebApiTargetKind.Users or WiredWebApiTargetKind.Pets or WiredWebApiTargetKind.Bots ? WiredVariableTarget.User : WiredVariableTarget.Furni;

    /// <summary>The property naming the target in a variables profile (WiredUserProfile "user", WiredFurniBcProfile "furni_bc", ...).</summary>
    public static string ProfileProperty(WiredWebApiTargetKind kind) => kind switch
    {
        WiredWebApiTargetKind.Users => "user",
        WiredWebApiTargetKind.Pets => "pet",
        WiredWebApiTargetKind.Bots => "bot",
        WiredWebApiTargetKind.Furni => "furni",
        WiredWebApiTargetKind.FurniBc => "furni_bc",
        WiredWebApiTargetKind.WallItems => "wall_item",
        _ => "wall_item_bc"
    };

    /// <summary>Entity ids are decimal digits; anything else is answered {"error":"not-found"} (observed on habbo.com).</summary>
    public static bool TryParseEntityId(string text, out long id)
    {
        id = 0;

        return text.Length is > 0 and <= 19 && text.All(char.IsAsciiDigit) && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out id);
    }
}

/// <summary>A room variable the API reaches: a custom user, furni or global variable, named as in the room.</summary>
public sealed record WiredWebApiVariable(uint DefinitionId, string Name, WiredVariableTarget Target, bool HasValue, bool ReadOnly);

/// <summary>
/// A holder of user or furni variables. <see cref="StorageId"/> is the engine's holder id (user id, item id, or the room-local id of a
/// pet or bot); <see cref="Present"/> says whether it stands in the room now, which writes need.
/// </summary>
public sealed record WiredWebApiEntity(WiredWebApiTargetKind Kind, long Id, string? Name, long StorageId, bool Present);

public enum WiredWebApiWriteMode
{
    /// <summary>PUT: sets the value, giving the variable to the holder when it has none.</summary>
    Put,

    /// <summary>PATCH: updates a value the holder already has.</summary>
    Patch
}

public enum WiredWebApiWriteOutcome
{
    Written,
    NoValue,
    NotPresent,
    NotAllowed
}

/// <summary>The WiredVariable schema: the value as a signed 64-bit decimal string and the two timestamps.</summary>
public sealed record WiredWebApiValue(
    [property: JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Value,
    [property: JsonPropertyName("creation_time"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CreationTime,
    [property: JsonPropertyName("update_time"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UpdateTime)
{
    public static WiredWebApiValue From(WiredWebApiVariable variable, WiredVariableValue value) => new(
        variable.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : null, Time(value.CreatedAt), Time(value.UpdatedAt));

    // date-time as in the spec's examples: "2023-11-16T22:13:20Z".
    private static string? Time(DateTimeOffset? time) => time?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}

/// <summary>The loaded room the API reads and writes. Implemented over the live room; tests use their own.</summary>
public interface IWiredWebApiRoom
{
    /// <summary>Whether the add-on with this item id still stands in the room.</summary>
    bool HasAddon(uint itemId);

    IReadOnlyList<WiredWebApiVariable> Variables();

    /// <summary>The holder of this kind and id, or null when there is none. Users who are not in the room are found by id only.</summary>
    WiredWebApiEntity? Find(WiredWebApiTargetKind kind, long id);

    /// <summary>The holder of this kind behind an engine holder id, or null when that holder is of another kind or gone.</summary>
    WiredWebApiEntity? FromStorage(WiredWebApiTargetKind kind, long storageId);

    /// <summary>Stored values by engine holder id; a global variable has one value under 0.</summary>
    IReadOnlyDictionary<long, WiredVariableValue> Values(WiredWebApiVariable variable);

    WiredWebApiWriteOutcome Write(WiredWebApiVariable variable, WiredWebApiEntity? entity, WiredWebApiWriteMode mode, long value);

    bool Remove(WiredWebApiVariable variable, WiredWebApiEntity entity);

    int Clear(WiredWebApiVariable variable);
}

/// <summary>What the API needs outside a loaded room: the rooms table, the add-on keys and user names.</summary>
public interface IWiredWebApiDirectory
{
    bool RoomExists(uint roomId);
    IWiredWebApiRoom? LoadedRoom(uint roomId);
    (long Id, string Name)? UserByName(string name);
    string? UserName(long id);
}
