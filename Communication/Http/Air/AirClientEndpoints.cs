using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plus.Communication.Encryption.Keys;
using Plus.HabboHotel.Users.Authentication;

namespace Plus.Communication.Http.Air;

/// <summary>
/// The hotel side of the AIR launcher's custom hotel contract:
/// <list type="bullet">
/// <item>GET /api/client/air.json: environment, game host and ports, url.prefix and the handshake modulus the launcher
/// patches into the client.</item>
/// <item>POST /api/client/air-ticket with <c>Authorization: Bearer &lt;launcher token&gt;</c>: a fresh single-use game ticket.</item>
/// <item>POST /api/client/air-launcher-token with <c>{"ticket": "..."}</c>: trades a live game ticket, which only the
/// hotel website hands to a signed-in user, for a launcher token. DELETE with the Bearer token revokes it.</item>
/// </list>
/// </summary>
public class AirClientEndpoints(AirClientConfiguration configuration, IHandshakeRsaKeyProvider handshakeKey, ISsoTicketStore tickets,
    ILauncherTokenStore launcherTokens, TimeProvider time)
{
    public const string TicketPath = "/api/client/air-ticket";
    public const string LauncherTokenPath = "/api/client/air-launcher-token";

    public void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/client/air.json", Contract);
        // Passed as Delegate: a Task-returning (HttpContext) handler otherwise binds as a RequestDelegate and its IResult is dropped.
        routes.MapPost(TicketPath, (Delegate)(Func<HttpContext, Task<IResult>>)Ticket);
        routes.MapPost(LauncherTokenPath, (Delegate)(Func<HttpContext, Task<IResult>>)IssueLauncherToken);
        routes.MapDelete(LauncherTokenPath, (Delegate)(Func<HttpContext, Task<IResult>>)RevokeLauncherToken);
    }

    private IResult Contract(HttpContext context)
    {
        // Without a game host or with the fallback key the client cannot connect, so there is no contract to give.
        if (string.IsNullOrWhiteSpace(configuration.GameHost) || !handshakeKey.IsConfigured || !handshakeKey.Key.UsesAirExponent) {
            return Results.NotFound();
        }

        var prefix = FlashGamedataEndpoints.UrlPrefix(context, configuration);
        var contract = new Dictionary<string, object>
        {
            ["version"] = 1,
            ["name"] = configuration.Name,
            ["environment"] = configuration.Environment,
            ["game"] = new { host = configuration.GameHost, ports = configuration.GamePorts },
            ["urls"] = new { prefix, webApi = OrDefault(configuration.WebApi, prefix), pocketApi = OrDefault(configuration.PocketApi, prefix) },
            ["rsa"] = new { modulus = handshakeKey.Key.ModulusHex },
            ["ticket"] = new { url = TicketPath }
        };

        if (!string.IsNullOrWhiteSpace(configuration.ClientUrl)) {
            contract["client"] = new { version = configuration.ClientVersion, url = configuration.ClientUrl };
        }

        if (configuration.Configuration.Count > 0) {
            contract["configuration"] = configuration.Configuration;
        }

        return Results.Json(contract);
    }

    private async Task<IResult> Ticket(HttpContext context)
    {
        if (Bearer(context) is not { } token || await launcherTokens.Resolve(token) is not { } userId) {
            return Unauthorized(context);
        }

        var ticket = await tickets.Issue(userId, TimeSpan.FromSeconds(Math.Clamp(configuration.TicketLifetimeSeconds, 10, 300)));

        return ticket == null ? Unauthorized(context) : Results.Json(new { ticket });
    }

    private async Task<IResult> IssueLauncherToken(HttpContext context)
    {
        string? ticket = null;

        try {
            using var body = await JsonDocument.ParseAsync(context.Request.Body);

            if (body.RootElement.ValueKind == JsonValueKind.Object && body.RootElement.TryGetProperty("ticket", out var value) &&
                value.ValueKind == JsonValueKind.String) {
                ticket = value.GetString();
            }
        }
        catch (JsonException) { }

        if (string.IsNullOrEmpty(ticket)) {
            return Results.BadRequest(new { error = "Send the game ticket as {\"ticket\": \"...\"}.", code = WebErrors.InvalidRequest });
        }

        // The ticket is used up here, exactly as a game login would use it.
        if (await tickets.Consume(ticket) is not { } userId) {
            return Unauthorized(context);
        }

        var lifetime = TimeSpan.FromDays(Math.Clamp(configuration.LauncherTokenLifetimeDays, 1, 365));
        var token = await launcherTokens.Issue(userId, lifetime);

        return token == null ? Unauthorized(context) : Results.Json(new { token, expiresAt = time.GetUtcNow().Add(lifetime) });
    }

    private async Task<IResult> RevokeLauncherToken(HttpContext context)
    {
        if (Bearer(context) is not { } token) {
            return Unauthorized(context);
        }

        await launcherTokens.Revoke(token);

        return Results.NoContent();
    }

    private static string? Bearer(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();

        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && header[7..].Trim() is { Length: > 0 } token ? token : null;
    }

    private static IResult Unauthorized(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";

        return Results.Json(new { error = "Not signed in.", code = "unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    private static string OrDefault(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.TrimEnd('/');
}
