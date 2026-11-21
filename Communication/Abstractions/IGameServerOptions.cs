namespace Plus.Communication.Abstractions;

public interface IGameServerOptions
{
    string Name { get; }
    string Hostname { get; }
    int Port { get; }

    /// <summary>Proxy addresses whose forwarded visitor address is used. Null means loopback only.</summary>
    IEnumerable<string>? TrustedProxies { get; }

    /// <summary>Open game connections allowed per visitor address. 0 turns the limit off.</summary>
    int MaxConnectionsPerIp { get; }
}
