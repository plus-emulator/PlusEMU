using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plus.HabboHotel.Items.Wired.WebApi;

namespace Plus.Communication.Http;

/// <summary>
/// Habbo's public Wired Variables Web API routes (habbo.com /api/public/api-docs/, tag "Wired Variables"), keyed by the X-Wired-Read-Key
/// and X-Wired-Write-Key headers the room's Variables Web API add-on generates. Unknown routes under /api/public/ get habbo.com's own
/// plain "Cannot METHOD path" 404 page.
/// </summary>
public partial class WiredWebApiEndpoints(IWiredWebApiService api)
{
    public const string PublicPrefix = "/api/public/";
    private const string Room = "/api/public/rooms/{roomId}";
    private const string Entry = Room + "/variables/{scope}/{variableName}/{targetKind}";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = null, DictionaryKeyPolicy = null };

    public void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet(Room + "/variables", (HttpContext context, string roomId) => Answer(api.ListVariables(roomId, Keys(context))));

        routes.MapGet(Entry + "/{entityId}", (HttpContext context, string roomId, string scope, string variableName, string targetKind, string entityId) =>
            Scoped(context, scope, () => api.ReadValue(roomId, scope, variableName, targetKind, entityId, Keys(context))));
        routes.MapPut(Entry + "/{entityId}", async (HttpContext context, string roomId, string scope, string variableName, string targetKind, string entityId) =>
            await ScopedAsync(context, scope, async () => api.WriteValue(roomId, scope, variableName, targetKind, entityId, WiredWebApiWriteMode.Put,
                ValueOf(await Body(context)), Keys(context))));
        routes.MapPatch(Entry + "/{entityId}", async (HttpContext context, string roomId, string scope, string variableName, string targetKind, string entityId) =>
            await ScopedAsync(context, scope, async () => api.WriteValue(roomId, scope, variableName, targetKind, entityId, WiredWebApiWriteMode.Patch,
                ValueOf(await Body(context)), Keys(context))));
        routes.MapDelete(Entry + "/{entityId}", (HttpContext context, string roomId, string scope, string variableName, string targetKind, string entityId) =>
            Scoped(context, scope, () => api.DeleteValue(roomId, scope, variableName, targetKind, entityId, Keys(context))));
        routes.MapGet(Entry, (HttpContext context, string roomId, string scope, string variableName, string targetKind) =>
            Scoped(context, scope, () => api.ListValues(roomId, scope, variableName, targetKind, Paging(context.Request.Query), Keys(context))));
        routes.MapGet(Entry + "/count", (HttpContext context, string roomId, string scope, string variableName, string targetKind) =>
            Scoped(context, scope, () => api.CountValues(roomId, scope, variableName, targetKind, Keys(context))));

        routes.MapPost(Room + "/variables/bulk-delete", async (HttpContext context, string roomId) =>
            Answer(api.BulkDelete(roomId, Names(await Body(context)), Keys(context))));
        routes.MapPost(Room + "/variables/{scope}/{variableName}/batch", async (HttpContext context, string roomId, string scope, string variableName) =>
            await ScopedAsync(context, scope, async () => api.Batch(roomId, scope, variableName, Operations(await Body(context)), Keys(context))));

        routes.MapGet(Room + "/variables/global/{variableName}", (HttpContext context, string roomId, string variableName) =>
            Answer(api.ReadGlobal(roomId, variableName, Keys(context))));
        routes.MapPatch(Room + "/variables/global/{variableName}", async (HttpContext context, string roomId, string variableName) =>
            Answer(api.WriteGlobal(roomId, variableName, ValueOf(await Body(context)), Keys(context))));

        routes.MapGet(Room + "/variables_profile/user/users", (HttpContext context, string roomId) =>
            Answer(api.ReadUserProfileByName(roomId, Query(context, "name"), Query(context, "unique_id"), Keys(context))));
        routes.MapGet(Room + "/variables_profile/user/{targetKind}/{entityId}", (HttpContext context, string roomId, string targetKind, string entityId) =>
            Answer(api.ReadProfile(roomId, "user", targetKind, entityId, Keys(context))));
        routes.MapPatch(Room + "/variables_profile/user/{targetKind}/{entityId}", async (HttpContext context, string roomId, string targetKind, string entityId) =>
            Answer(api.PatchProfile(roomId, "user", targetKind, entityId, Patch(await Body(context), true), Keys(context))));
        routes.MapDelete(Room + "/variables_profile/user/{targetKind}/{entityId}", (HttpContext context, string roomId, string targetKind, string entityId) =>
            Answer(api.DeleteProfile(roomId, targetKind, entityId, Keys(context))));
        routes.MapGet(Room + "/variables_profile/furni/{targetKind}/{entityId}", (HttpContext context, string roomId, string targetKind, string entityId) =>
            Answer(api.ReadProfile(roomId, "furni", targetKind, entityId, Keys(context))));
        routes.MapPatch(Room + "/variables_profile/furni/{targetKind}/{entityId}", async (HttpContext context, string roomId, string targetKind, string entityId) =>
            Answer(api.PatchProfile(roomId, "furni", targetKind, entityId, Patch(await Body(context), true), Keys(context))));
        routes.MapGet(Room + "/variables_profile/global", (HttpContext context, string roomId) => Answer(api.ReadGlobalProfile(roomId, Keys(context))));
        routes.MapPatch(Room + "/variables_profile/global", async (HttpContext context, string roomId) =>
            Answer(api.PatchGlobalProfile(roomId, Patch(await Body(context), false), Keys(context))));
    }

    /// <summary>habbo.com's 404 for anything its public API does not route: an HTML page naming the method and path.</summary>
    public static Task WriteNotFoundPage(HttpContext context)
    {
        var response = context.Response;
        response.StatusCode = StatusCodes.Status404NotFound;
        response.ContentType = "text/html; charset=utf-8";
        response.Headers.ContentSecurityPolicy = "default-src 'none'";
        response.Headers.XContentTypeOptions = "nosniff";

        return response.WriteAsync(NotFoundPage(context.Request.Method, context.Request.PathBase + context.Request.Path));
    }

    public static string NotFoundPage(string method, string path) =>
        "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<title>Error</title>\n</head>\n<body>\n<pre>Cannot "
        + WebUtility.HtmlEncode(method) + " " + WebUtility.HtmlEncode(path) + "</pre>\n</body>\n</html>\n";

    public static bool IsPublic(HttpContext context) => context.Request.Path.StartsWithSegments("/api/public", StringComparison.Ordinal);

    // Only the user and furni scopes are routes on habbo.com; any other scope is its plain 404 page.
    private static IResult Scoped(HttpContext context, string scope, Func<WiredWebApiResult> handle) =>
        scope is "user" or "furni" ? Answer(handle()) : new NotFoundPageResult();

    private static async Task<IResult> ScopedAsync(HttpContext context, string scope, Func<Task<WiredWebApiResult>> handle) =>
        scope is "user" or "furni" ? Answer(await handle()) : new NotFoundPageResult();

    private static WiredWebApiCredentials Keys(HttpContext context) => new(Header(context, "X-Wired-Read-Key"), Header(context, "X-Wired-Write-Key"));

    private static string? Header(HttpContext context, string name) =>
        context.Request.Headers.TryGetValue(name, out var values) && values.ToString().Trim() is { Length: > 0 } value ? value : null;

    private static string? Query(HttpContext context, string name) =>
        context.Request.Query.TryGetValue(name, out var values) && values.ToString() is { Length: > 0 } value ? value : null;

    private static WiredWebApiPaging Paging(IQueryCollection query) =>
        new(query["order_by"].ToString(), query["order_dir"].ToString(), query["page"].ToString(), query["size"].ToString());

    private static IResult Answer(WiredWebApiResult result) =>
        result.Body is null ? Results.StatusCode(result.Status) : Results.Json(result.Body, Json, statusCode: result.Status);

    private static async Task<JsonElement?> Body(HttpContext context)
    {
        try {
            using var document = await JsonDocument.ParseAsync(context.Request.Body);

            return document.RootElement.Clone();
        }
        catch (JsonException) {
            return null;
        }
    }

    [GeneratedRegex("^-?[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalPattern();

    // "value": a signed 64-bit integer as a decimal string (WiredValueWrite).
    private static WiredWebApiInput Input(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { } text && DecimalPattern().IsMatch(text)
            && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? WiredWebApiInput.Of(number) : WiredWebApiInput.Malformed;

    private static WiredWebApiInput ValueOf(JsonElement? body) =>
        body is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null
            ? Input(value) : WiredWebApiInput.Missing;

    private static IReadOnlyList<string>? Names(JsonElement? body) =>
        body is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("variables", out var names) && names.ValueKind == JsonValueKind.Array
            ? names.EnumerateArray().Select(name => name.ValueKind == JsonValueKind.String ? name.GetString() ?? "" : "").ToArray() : null;

    private static IReadOnlyDictionary<string, WiredWebApiInput?>? Patch(JsonElement? body, bool allowNull) =>
        body is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("variables", out var variables) && variables.ValueKind == JsonValueKind.Object
            ? variables.EnumerateObject().ToDictionary(x => x.Name,
                x => x.Value.ValueKind == JsonValueKind.Null ? allowNull ? null : WiredWebApiInput.Malformed : (WiredWebApiInput?)Input(x.Value), StringComparer.Ordinal)
            : null;

    private static IReadOnlyList<WiredWebApiBatchOperation>? Operations(JsonElement? body) =>
        body is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("requests", out var requests) && requests.ValueKind == JsonValueKind.Array
            ? requests.EnumerateArray().Select(Operation).ToArray() : null;

    private static WiredWebApiBatchOperation Operation(JsonElement request)
    {
        if (request.ValueKind != JsonValueKind.Object) {
            return new(null, null, null, WiredWebApiInput.Missing);
        }

        string? Text(string name) => request.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        return new(Text("op_id"), Text("method"), Text("path"), ValueOf(request.TryGetProperty("body", out var body) ? body : null));
    }
}

internal sealed class NotFoundPageResult : IResult
{
    public Task ExecuteAsync(HttpContext httpContext) => WiredWebApiEndpoints.WriteNotFoundPage(httpContext);
}
