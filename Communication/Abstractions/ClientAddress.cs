using System.Net;

namespace Plus.Communication.Abstractions;

/// <summary>
/// The address a game connection comes from. <see cref="IsVisitor"/> is false when the connection comes from a
/// trusted proxy that did not forward a visitor address. Per-IP rules skip such connections.
/// </summary>
public readonly record struct ClientAddress(IPAddress Address, bool IsVisitor)
{
    public override string ToString() => Address.ToString();
}
