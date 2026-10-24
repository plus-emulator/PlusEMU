using System.Net;
using Microsoft.Extensions.Logging;

namespace Plus.Communication.Abstractions;

public sealed class ConnectionAdmission
{
    public ConnectionAdmission(IWebsocketServerOptions options, TimeProvider clock, ILogger logger)
    {
    }

    public ClientAddress Resolve(IPAddress peer, IEnumerable<(string Name, string Value)> headers) => throw new NotImplementedException();

    public IDisposable? TryAdmit(ClientAddress address) => throw new NotImplementedException();
}
