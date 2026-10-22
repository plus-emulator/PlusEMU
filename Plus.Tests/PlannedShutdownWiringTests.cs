using System.Data;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Plus.Core;
using Plus.Database;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Rooms.Trading;
using Plus.HabboHotel.Users.Authentication;
using Xunit;

namespace Plus.Tests;

public sealed class PlannedShutdownWiringTests
{
    [Theory]
    [InlineData(typeof(ICatalogPurchaseService))]
    [InlineData(typeof(ICatalogGiftPurchaseService))]
    [InlineData(typeof(IClubCatalogService))]
    [InlineData(typeof(ITradeRequestService))]
    [InlineData(typeof(ISsoLoginService))]
    public void ContainerHandsTheHotelShutdownToEveryGuardedService(Type service)
    {
        using var services = Services();
        var shutdown = services.GetRequiredService<IPlannedShutdown>();
        var instance = services.GetRequiredService(service);
        var field = instance.GetType().GetField("_plannedShutdown", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Same(shutdown, field.GetValue(instance));
    }

    private static ServiceProvider Services()
    {
        var collection = new ServiceCollection();
        var defaults = typeof(Program).GetField("_defaultTypes", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = defaults.GetValue(null);

        try {
            defaults.SetValue(null, new Dictionary<ServiceLifetime, IEnumerable<Type>>
            { [ServiceLifetime.Singleton] = [], [ServiceLifetime.Scoped] = [] });
            typeof(Program).GetMethod("AddDefaultRules", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [collection, typeof(Program).Assembly]);
        }
        finally {
            defaults.SetValue(null, previous);
        }

        collection.RemoveAll<IDatabase>();
        collection.AddSingleton<IDatabase, UnusedDatabase>();
        collection.AddSingleton(TimeProvider.System);
        collection.AddOptions();
        collection.AddLogging();

        return collection.BuildServiceProvider();
    }

    private sealed class UnusedDatabase : IDatabase
    {
        public bool IsConnected() => false;

        public IDbConnection Connection() => throw new InvalidOperationException("No database in this test.");
    }
}
