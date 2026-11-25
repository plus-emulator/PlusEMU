namespace Plus.Communication.Http;

/// <summary>
/// "WebServer" section of config.json. Every value has a working default, so the server
/// still listens on 127.0.0.1:8080 when the section is missing.
/// </summary>
public class WebServerConfiguration
{
    public string Hostname { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Proxy addresses whose forwarded visitor address is used. Null means loopback only.
    /// On the AIR login paths the proxy must overwrite X-Real-IP with the visitor address itself and never pass on a value a
    /// client sent, because the failed-login limit there trusts that header.
    /// </summary>
    public IEnumerable<string>? TrustedProxies { get; set; }
}
