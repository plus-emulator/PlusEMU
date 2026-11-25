using Microsoft.Extensions.Logging;
using Plus.Core.Settings;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Process;
using Xunit;

namespace Plus.Tests;

public sealed class ScheduledCurrencyFailureTests
{
    [Fact]
    public void FailedRewardWriteIsLoggedAsAnError()
    {
        var logger = new CapturingLogger();
        var (process, habbo) = Create(new Store(true), Settings(5, 10));
        Plus.Core.ExceptionLogger.Configure(new CapturingFactory(logger));

        try {
            using (process) {
                RunInterval(process);
            }
        }
        finally {
            Plus.Core.ExceptionLogger.Configure(TestLogging.Factory);
        }

        Assert.Contains(logger.Errors, error => error?.Message == "write failed");
        Assert.Equal((100, 20), (habbo.Credits, habbo.Duckets));
    }

    [Fact]
    public void NegativeRewardSettingWaitsForTheNextInterval()
    {
        var store = new Store(false);
        var (process, habbo) = Create(store, Settings(-1, 10));

        using (process) {
            RunInterval(process);
        }

        Assert.Empty(store.Writes);
        Assert.Equal(3, habbo.CreditsUpdateTick);
    }

    private static (ProcessComponent Process, Habbo Habbo) Create(Store store, ISettingsManager settings)
    {
        var habbo = new Habbo { Id = 7, Credits = 100, Duckets = 20, Access = UserAccess.Empty };
        var (client, _) = HabbiconTestSupport.Client(habbo);
        habbo.Client = client;
        var process = new ProcessComponent(TestLogging.For<ProcessComponent>(), TimeProvider.System, store,
            CatalogSnapshotTestSupport.Proxy<IAchievementManager>((_, _) => false), settings);
        Assert.True(process.Init(habbo));

        return (process, habbo);
    }

    private static void RunInterval(ProcessComponent process)
    {
        for (var check = 0; check < 3; check++) {
            process.Run(null);
        }
    }

    private static ISettingsManager Settings(int credits, int duckets) => new TestRoomSettings(new()
    {
        ["user.currency_scheduler.credit_reward"] = credits.ToString(),
        ["user.currency_scheduler.ducket_reward"] = duckets.ToString(),
        ["user.currency_scheduler.tick"] = "3"
    });

    private sealed class Store(bool throws) : IUserProcessStore
    {
        public List<(int, int, int)> Writes { get; } = [];
        public void ResetDailyRespects(int userId, int respects, int petRespects, string day) => throw new NotSupportedException();
        public bool SaveCurrency(int userId, int credits, int duckets)
        {
            Writes.Add((userId, credits, duckets));

            return throws ? throw new InvalidOperationException("write failed") : true;
        }
    }

    private sealed class CapturingFactory(ILogger logger) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => logger;
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<Exception?> Errors { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error) {
                Errors.Add(exception);
            }
        }
    }
}
