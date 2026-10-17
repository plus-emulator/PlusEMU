namespace Plus.Communication.Http.Air;

/// <summary>
/// "AirClient" section of config.json: what the emulator serves the official AIR client over HTTP.
/// Relative file paths are relative to the Config directory.
/// </summary>
public class AirClientConfiguration
{
    /// <summary>The hotel's public origin (url.prefix), for example https://example.com. Empty: taken from the request
    /// (X-Forwarded-Proto and Host), which is only right behind a proxy that fixes the Host header.</summary>
    public string UrlPrefix { get; set; } = string.Empty;

    /// <summary>external_variables base files in key=value form (for example a saved copy of Habbo's), merged in order.</summary>
    public List<string> ExternalVariablesFiles { get; set; } = [];

    /// <summary>external_flash_texts files, merged in order: key=value text, or a JSON object of key to text such as
    /// Volt's ExternalTexts.json.</summary>
    public List<string> ExternalTextsFiles { get; set; } = [];

    /// <summary>productdata: Habbo's XML or text file served as is, or a ProductData.json converted to XML. Empty: the
    /// name and description of every furnidata entry.</summary>
    public string ProductDataFile { get; set; } = string.Empty;

    /// <summary>external_variables entries that win over the files and the generated ones.</summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    // GET /api/client/air.json, the launcher's hotel contract. It is only served once GameHost is set.

    public string Name { get; set; } = "PlusEMU";

    /// <summary>The environment id the client runs as; it must be one of the client's live.environment.list
    /// (en, pt, de, es, fi, fr, it, nl, tr, s2), or the client drops it (AIR WIN63-202610151745-318700621, class_64.as:200).</summary>
    public string Environment { get; set; } = "en";

    /// <summary>Public host of the Flash listener. It cannot sit behind an HTTP-only proxy.</summary>
    public string GameHost { get; set; } = string.Empty;

    /// <summary>Comma-separated ports the client tries in order.</summary>
    public string GamePorts { get; set; } = "30000";

    /// <summary>AIR client package version and zip URL. Empty: the launcher uses Habbo's current client, whose
    /// build has to match the emulator's packet headers.</summary>
    public string ClientVersion { get; set; } = string.Empty;
    public string ClientUrl { get; set; } = string.Empty;

    /// <summary>web.api and pocket.api; empty means UrlPrefix.</summary>
    public string WebApi { get; set; } = string.Empty;
    public string PocketApi { get; set; } = string.Empty;

    /// <summary>Extra key: value lines for the client's embedded configuration.</summary>
    public Dictionary<string, string> Configuration { get; set; } = [];

    /// <summary>How long a ticket from POST /api/client/air-ticket stays usable. It is single use either way.</summary>
    public int TicketLifetimeSeconds { get; set; } = 60;

    /// <summary>How long a launcher token lives.</summary>
    public int LauncherTokenLifetimeDays { get; set; } = 30;
}
