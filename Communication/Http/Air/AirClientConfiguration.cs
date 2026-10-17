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
}
