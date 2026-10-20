using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Plus.Database;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Rooms.Chat.Commands.Moderator;
using Xunit;

namespace Plus.Tests;

public sealed class RoleChatCommandTests
{
    [Fact]
    public void ProductionCommandRegistrationDiscoversBothRoleCommands()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDatabase>(EditorTestSupport.UntouchableDatabase());
        services.AddSingleton<IAccessControl>(provider => new AccessControl(provider.GetRequiredService<IDatabase>(), null!, NullLogger<AccessControl>.Instance, TimeProvider.System));
        services.AddSingleton(TimeProvider.System);
        Program.AddAssignableTo<ICommandBase>(services, typeof(GiveRoleCommand).Assembly);
        using var provider = services.BuildServiceProvider();
        var give = provider.GetRequiredService<GiveRoleCommand>();
        var take = provider.GetRequiredService<TakeRoleCommand>();
        var commands = new CommandManager([give, take], null!, provider.GetRequiredService<IDatabase>(), TimeProvider.System, NullLogger<CommandManager>.Instance);
        Assert.True(commands.TryGetCommand("giverole", out var registeredGive));
        Assert.Same(give, registeredGive);
        Assert.True(commands.TryGetCommand("takerole", out var registeredTake));
        Assert.Same(take, registeredTake);
        Assert.Contains(PermissionKeys.All, definition => definition.Key == PermissionKeys.CommandGiverole);
        Assert.Contains(PermissionKeys.All, definition => definition.Key == PermissionKeys.CommandTakerole);
    }

    // CommandManager lets a user run a command only with "command.<key>"; AccessControl marks keys missing here as orphans.
    [Fact]
    public void EveryChatCommandHasARegisteredPermission()
    {
        var registered = PermissionKeys.All.Select(definition => definition.Key).ToHashSet();
        var missing = typeof(ICommandBase).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ICommandBase).IsAssignableFrom(type))
            .Select(type => "command." + ((ICommandBase)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type)).Key)
            .Where(key => !registered.Contains(key))
            .Order()
            .ToArray();

        Assert.Empty(missing);
    }
}
