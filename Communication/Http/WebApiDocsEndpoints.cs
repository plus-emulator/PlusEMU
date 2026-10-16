using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Plus.Communication.Http;

/// <summary>
/// The public Web API's Swagger UI page, laid out like habbo.com /api/public/api-docs/: the bare path redirects to the slash form,
/// which serves the swagger-ui-dist page whose swagger-ui-init.js embeds the spec. The spec (ApiDocs/openapi.json) is the
/// "Wired Variables" part of habbo.com's spec, the only endpoints this server routes. Every file is an embedded resource, so
/// the page needs no CDN.
/// </summary>
public static class WebApiDocsEndpoints
{
    public const string Path = "/api/public/api-docs";

    private const string ResourcePrefix = "Plus.ApiDocs.";
    private const string SpecPlaceholder = "<% swaggerDoc %>";

    private static readonly Lazy<IReadOnlyDictionary<string, DocsFile>> Files = new(Load);

    /// <summary>The OpenAPI document the page shows.</summary>
    public static string Spec => Text("openapi.json");

    public static void Map(IEndpointRouteBuilder routes)
    {
        // Routing ignores a trailing slash, so one route answers both forms.
        routes.MapGet(Path, context => context.Request.Path.Value!.EndsWith('/') ? Serve(context, Files.Value["index.html"]) : Redirect(context));
        routes.MapGet(Path + "/{file}", context =>
            Files.Value.TryGetValue((string)context.Request.RouteValues["file"]!, out var file) && file.Name != "index.html"
                ? Serve(context, file)
                : WiredWebApiEndpoints.WriteNotFoundPage(context));
    }

    // habbo.com answers the bare path with its framework's 301 page.
    private static Task Redirect(HttpContext context)
    {
        var location = context.Request.PathBase + Path + "/" + context.Request.QueryString;
        var response = context.Response;
        response.StatusCode = StatusCodes.Status301MovedPermanently;
        response.Headers.Location = location;
        response.ContentType = "text/html; charset=UTF-8";
        response.Headers.ContentSecurityPolicy = "default-src 'none'";

        return response.WriteAsync("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<title>Redirecting</title>\n</head>\n<body>\n"
            + "<pre>Redirecting to " + WebUtility.HtmlEncode(location) + "</pre>\n</body>\n</html>\n");
    }

    private static Task Serve(HttpContext context, DocsFile file)
    {
        var response = context.Response;
        response.Headers.CacheControl = "public, max-age=0";
        response.Headers.ETag = file.ETag;

        if (context.Request.Headers.IfNoneMatch.Any(tag => tag == file.ETag)) {
            response.StatusCode = StatusCodes.Status304NotModified;

            return Task.CompletedTask;
        }

        response.ContentType = file.ContentType;
        response.ContentLength = file.Content.Length;

        return response.Body.WriteAsync(file.Content).AsTask();
    }

    private static IReadOnlyDictionary<string, DocsFile> Load()
    {
        var files = new Dictionary<string, DocsFile>(StringComparer.Ordinal);

        foreach (var name in new[] { "swagger-ui.css", "swagger-ui-bundle.js", "swagger-ui-standalone-preset.js", "favicon-16x16.png", "favicon-32x32.png" }) {
            files[name] = DocsFile.Of(name, Bytes("swagger-ui-dist." + name));
        }

        files["index.html"] = DocsFile.Of("index.html", Bytes("index.html"));
        files["swagger-ui-init.js"] = DocsFile.Of("swagger-ui-init.js", Encoding.UTF8.GetBytes(InitScript(Spec)));

        return files;
    }

    /// <summary>swagger-ui-init.js with the spec embedded the way habbo.com's page embeds its own (indented one level).</summary>
    internal static string InitScript(string spec) =>
        Text("swagger-ui-init.js").Replace(SpecPlaceholder, spec.TrimEnd('\n').Replace("\n", "\n  "), StringComparison.Ordinal);

    private static string Text(string name) => Encoding.UTF8.GetString(Bytes(name));

    private static byte[] Bytes(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException($"Missing embedded resource {ResourcePrefix + name}");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }

    private sealed record DocsFile(string Name, byte[] Content, string ContentType, string ETag)
    {
        public static DocsFile Of(string name, byte[] content) =>
            new(name, content, ContentTypeOf(name), "W/\"" + Convert.ToHexStringLower(SHA256.HashData(content))[..32] + "\"");

        private static string ContentTypeOf(string name) => System.IO.Path.GetExtension(name) switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}
