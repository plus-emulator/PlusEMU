using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plus.Communication.Abstractions;

namespace Plus.HabboHotel.Users.Authentication;

public class LoginThrottleConfiguration
{
    public int MaxFailuresPerIp { get; set; } = 10;
    public int WindowMinutes { get; set; } = 5;
    public int BlockMinutes { get; set; } = 10;
}

public interface ILoginThrottle
{
    bool IsBlocked(ClientAddress? address);

    void RecordFailure(ClientAddress? address);
}

internal sealed class LoginThrottle : ILoginThrottle
{
    public LoginThrottle(IOptions<LoginThrottleConfiguration> options, TimeProvider clock, ILogger<LoginThrottle> logger) : this(options, clock, logger, 10_000) { }

    internal LoginThrottle(IOptions<LoginThrottleConfiguration> options, TimeProvider clock, ILogger<LoginThrottle> logger, int capacity)
    {
    }

    internal int TrackedAddresses => throw new NotImplementedException();

    public bool IsBlocked(ClientAddress? address) => throw new NotImplementedException();

    public void RecordFailure(ClientAddress? address) => throw new NotImplementedException();
}
