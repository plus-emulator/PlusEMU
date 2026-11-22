namespace Plus.Communication.Abstractions;

public interface IGameServerOptions
{
    string Name { get; }
    string Hostname { get; }
    int Port { get; }

    /// <summary>
    /// Proxy addresses whose forwarded visitor address is used. Null means loopback only. Raw TCP has no forwarded
    /// address, so a Flash connection from a trusted proxy has no known visitor: it gets no per-address limit, no login
    /// limit and no IP ban check.
    /// </summary>
    IEnumerable<string>? TrustedProxies { get; }

    /// <summary>Open game connections allowed per visitor address. 0 turns the limit off.</summary>
    int MaxConnectionsPerIp { get; }
}
