using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Plus.Communication.Http.Air;

/// <summary>
/// The Flash gamedata the AIR client loads from <c>${url.prefix}/gamedata/...</c> (AIR WIN63-202610151745-318700621,
/// embedded common_configuration, class_424.as:6):
/// <list type="bullet">
/// <item>GET /gamedata/hashes: the file list with content hashes.</item>
/// <item>GET /gamedata/{external_flash_texts|external_variables|furnidata_xml|productdata}/{hash}: the file. Any hash gets
/// the current content (the client also asks for external_variables/1 and external_flash_texts/1 before it has the
/// list); only the current hash may be cached for good.</item>
/// </list>
/// </summary>
public class FlashGamedataEndpoints(IFlashGamedata gamedata, AirClientConfiguration configuration)
{
    private const string Immutable = "public, max-age=31536000, immutable";

    public void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/gamedata/hashes", Hashes);

        foreach (var route in new[] { FlashGamedata.TextsRoute, FlashGamedata.VariablesRoute, FlashGamedata.FurnidataRoute, FlashGamedata.ProductdataRoute }) {
            routes.MapGet($"/gamedata/{route}/{{hash}}", (HttpContext context, string hash) => File(context, route, hash));
        }
    }

    private IResult Hashes(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-cache";

        return Results.Bytes(gamedata.Current(UrlPrefix(context)).HashesJson(), "application/json; charset=utf-8");
    }

    private IResult File(HttpContext context, string route, string hash)
    {
        if (gamedata.Current(UrlPrefix(context)).ByRoute(route) is not { } file) {
            return Results.NotFound();
        }

        context.Response.Headers.CacheControl = hash == file.Hash ? Immutable : "no-cache";

        return Results.Bytes(file.Content, file.ContentType);
    }

    /// <summary>The configured url.prefix, or the origin the request came in on.</summary>
    public static string UrlPrefix(HttpContext context, AirClientConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.UrlPrefix)) {
            return configuration.UrlPrefix.TrimEnd('/');
        }

        var scheme = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() is { Length: > 0 } forwarded ? forwarded : context.Request.Scheme;

        return $"{scheme}://{context.Request.Host}";
    }

    private string UrlPrefix(HttpContext context) => UrlPrefix(context, configuration);
}
