using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plus.HabboHotel.Items.Wired.WebApi;

namespace Plus.Communication.Http;

/// <summary>
/// The Variables Web API add-on's HTTP side, authenticated with "Authorization: Bearer &lt;key&gt;" by the keys the add-on's editor
/// generated: GET /api/wired/variables reads the room's variables, PUT /api/wired/variables/{id} sets one holder's value
/// ({"value": n, "holderId": n}) and DELETE /api/wired/variables/{id} clears every holder when the add-on allows bulk delete.
/// </summary>
public class WiredWebApiEndpoints(IWiredWebApiService api)
{
    private const string Route = "/api/wired/variables";

    public void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet(Route, (HttpContext context) => Answer(Key(context) is { } key ? api.Read(key) : Missing()));
        routes.MapPut(Route + "/{id}", async (HttpContext context, string id) => Answer(Key(context) is { } key
            ? await ReadBody(context) is { } body ? api.Write(key, id, body.Value, body.HolderId) : Invalid()
            : Missing()));
        routes.MapDelete(Route + "/{id}", (HttpContext context, string id) => Answer(Key(context) is { } key ? api.Clear(key, id) : Missing()));
    }

    private static string? Key(HttpContext context) =>
        context.Request.Headers.Authorization.ToString() is { } header && header.StartsWith("Bearer ", StringComparison.Ordinal)
            ? header[7..].Trim() : null;

    private static WiredWebApiResult Missing() => WiredWebApiResult.Fail(401, WiredWebApiService.Unauthorized, "Send the key as a bearer token.");

    private static WiredWebApiResult Invalid() => WiredWebApiResult.Fail(400, WebErrors.InvalidRequest, "Invalid request.");

    private sealed record WriteBody(long Value, int HolderId);

    private static async Task<WriteBody?> ReadBody(HttpContext context)
    {
        try {
            using var document = await JsonDocument.ParseAsync(context.Request.Body);
            var root = document.RootElement;

            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("value", out var value)
                && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
                ? new(number, root.TryGetProperty("holderId", out var holder) && holder.ValueKind == JsonValueKind.Number
                    && holder.TryGetInt32(out var id) ? id : 0) : null;
        }
        catch (JsonException) {
            return null;
        }
    }

    private static IResult Answer(WiredWebApiResult result) => result.Body is { } body
        ? Results.Json(body, statusCode: result.Status)
        : Results.Json(new { error = result.Error, code = result.Code }, statusCode: result.Status);
}
