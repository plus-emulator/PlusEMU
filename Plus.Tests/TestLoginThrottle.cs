using Microsoft.Extensions.Options;
using Plus.HabboHotel.Users.Authentication;

namespace Plus.Tests;

internal static class TestLoginThrottle
{
    internal static ILoginThrottle Off { get; } =
        new LoginThrottle(Options.Create(new LoginThrottleConfiguration { MaxFailuresPerIp = 0 }), TimeProvider.System, TestLogging.For<LoginThrottle>());
}
