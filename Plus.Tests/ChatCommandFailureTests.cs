using Microsoft.Extensions.Logging;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public async Task AFailingChatCommandKeepsThePlayerConnectedAndWhispersHowToUseIt()
    {
        EnterAsOwner();
        var failure = new InvalidOperationException("database is down");
        var logger = new CommandErrorLogger();
        var clients = Proxy<IGameClientManager>((method, _) => method == "GetClientByUsername" ? _client : throw new InvalidOperationException(method));
        var commands = new CommandManager([new FailingCommand(failure), new FailingPlainCommand(failure), new FailingTargetCommand(failure)], clients, _database, _interactionClock, logger);
        string[] keys = ["command.failing", "command.failingplain", "command.failingtarget"];
        _client.GetHabbo().Access = UserAccess.Create([], keys.Select(key => new UserPermissionOverride(key, false)), keys);
        var disconnects = 0;
        _client.DisconnectRequested = () => disconnects++;

        Assert.True(await commands.Parse(_client, ":failing yes"));
        Assert.Equal("Something went wrong with this command. Use it like this: :failing %yes%", LastWhisper());

        Assert.True(await commands.Parse(_client, ":failingplain"));
        Assert.Equal("Something went wrong with this command. Use it like this: :failingplain", LastWhisper());

        Assert.True(await commands.Parse(_client, ":failingtarget someone"));
        Assert.Equal("Something went wrong with this command. Use it like this: :failingtarget %username%", LastWhisper());

        Assert.Equal(0, disconnects);
        Assert.Equal([failure, failure, failure], logger.Errors);
    }

    private sealed class FailingCommand(Exception failure) : IChatCommand
    {
        public string Key => "failing";
        public string Parameters => "%yes%";
        public string Description => "Always fails.";
        public void Execute(GameClient session, Room room, string[] parameters) => throw failure;
    }

    private sealed class FailingPlainCommand(Exception failure) : IChatCommand
    {
        public string Key => "failingplain";
        public string Parameters => "";
        public string Description => "Always fails.";
        public void Execute(GameClient session, Room room, string[] parameters) => throw failure;
    }

    private sealed class FailingTargetCommand(Exception failure) : ITargetChatCommand
    {
        public string Key => "failingtarget";
        public string Parameters => "%username%";
        public string Description => "Always fails.";
        public bool MustBeInSameRoom => false;

        public async Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
        {
            await Task.Yield();

            throw failure;
        }
    }

    private sealed class CommandErrorLogger : ILogger<CommandManager>
    {
        public List<Exception?> Errors { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error) {
                Errors.Add(exception);
            }
        }
    }
}
