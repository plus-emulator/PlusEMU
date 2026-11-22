using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Plus.Communication.RCON;
using Plus.Communication.RCON.Commands;
using Plus.Communication.RCON.Commands.User;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Grants;
using Xunit;

namespace Plus.Tests;

public sealed class RconConnectionTests
{
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(30);
    private const string Secret = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task AcknowledgesASplitJsonRequestOnlyAfterTheCommandSucceeds()
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new RecordingCommands(true);
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, commands, new FakeTimeProvider());

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"give_user_"));
        await Task.Delay(20);
        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("currency\",\"parameters\":[\"42\",\"credits\",\"5\"]}\n"));

        var response = await ReadResponse(sockets.Client);
        Assert.Equal(0, response.GetProperty("status").GetInt32());
        Assert.Equal("give_user_currency\u000142:credits:5", commands.Request);
    }

    [Fact]
    public async Task DispatchesAJsonShutdownRequest()
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new RecordingCommands(true);
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, commands, new FakeTimeProvider());

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"shutdown\",\"parameters\":[\"5\",\"30\"]}\n"));

        Assert.Equal(0, (await ReadResponse(sockets.Client)).GetProperty("status").GetInt32());
        Assert.Equal("shutdown\u00015:30", commands.Request);
    }

    [Fact]
    public async Task ReportsACommandRejectionInsteadOfFabricatingSuccess()
    {
        using var sockets = await ConnectedSockets.Create();
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, new RecordingCommands(false), new FakeTimeProvider());

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"disconnect_user\",\"parameters\":[\"42\"]}\n"));

        var response = await ReadResponse(sockets.Client);
        Assert.Equal(1, response.GetProperty("status").GetInt32());
        Assert.Contains("rejected", response.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("{not-json}\n")]
    [InlineData("{\"command\":\"not_registered\",\"parameters\":[]}\n")]
    [InlineData("{\"command\":\"reload_filter\",\"parameters\":[],\"extra\":true}\n")]
    [InlineData("{\"command\":\"alert_user\",\"parameters\":[\"42\",\"unsafe:delimiter\"]}\n")]
    public async Task RejectsMalformedUnknownAndDelimiterInjectingRequests(string request)
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new RecordingCommands(true);
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, commands, new FakeTimeProvider());

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes(request));

        Assert.Equal(1, (await ReadResponse(sockets.Client)).GetProperty("status").GetInt32());
        Assert.Null(commands.Request);
    }

    [Fact]
    public async Task RejectsRequestsBeyondTheBoundWithoutDispatching()
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new RecordingCommands(true);
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, commands, new FakeTimeProvider());
        var oversized = "{\"command\":\"alert_user\",\"parameters\":[\"" + new string('a', RconConnection.MaxRequestBytes) + "\"]}\n";

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes(oversized));

        Assert.Equal(1, (await ReadResponse(sockets.Client)).GetProperty("status").GetInt32());
        Assert.Null(commands.Request);
    }

    [Fact]
    public async Task TimesOutAnIncompleteFrameWithoutDispatching()
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new RecordingCommands(true);
        var time = new FakeTimeProvider();
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, commands, time);

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"disconnect_user\""));
        time.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(1));
        Assert.False(sockets.Client.Poll(0, SelectMode.SelectRead));
        time.Advance(TimeSpan.FromMilliseconds(1));

        var response = await ReadResponse(sockets.Client);
        Assert.Equal(1, response.GetProperty("status").GetInt32());
        Assert.Contains("timed out", response.GetProperty("message").GetString());
        Assert.Null(commands.Request);
    }

    [Fact]
    public async Task ACompletedFrameCannotTimeOutWhileItsCommandIsDispatching()
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new BlockingCommands();
        var time = new FakeTimeProvider();
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, commands, time);

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"disconnect_user\",\"parameters\":[\"42\"]}\n"));

        try {
            await commands.Entered.Task.WaitAsync(HangGuard);
            time.Advance(TimeSpan.FromSeconds(2.2));
            Assert.False(sockets.Client.Poll(0, SelectMode.SelectRead));
        }
        finally {
            commands.Release.TrySetResult();
        }

        Assert.Equal(0, (await ReadResponse(sockets.Client)).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task PreservesTheLegacyOneReadProtocolWithoutAnAcknowledgement()
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new RecordingCommands(true);
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, commands, new FakeTimeProvider());

        await sockets.Client.SendAsync(Encoding.Default.GetBytes("disconnect_user\u000142"));
        var buffer = new byte[1];

        Assert.Equal(0, await sockets.Client.ReceiveAsync(buffer));
        Assert.Equal("disconnect_user\u000142", commands.Request);
    }

    [Fact]
    public async Task ALegacyCommandThatThrowsStillClosesTheSocket()
    {
        using var sockets = await ConnectedSockets.Create();
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, new ThrowingCommands(), new FakeTimeProvider());

        await sockets.Client.SendAsync(Encoding.Default.GetBytes("disconnect_user\u000142"));

        await AssertClosed(sockets.Client);
    }

    [Theory]
    [InlineData("disconnect_user")]
    [InlineData("alert_user\u000142")]
    [InlineData("progress_user_achievement\u000142:ACH")]
    public async Task MalformedLegacyInputRunsNothingAndClosesTheSocket(string request)
    {
        var (commands, disconnect, sent) = LegacyCommands();
        using var sockets = await ConnectedSockets.Create();
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, commands, new FakeTimeProvider());

        await sockets.Client.SendAsync(Encoding.Default.GetBytes(request));

        await AssertClosed(sockets.Client);
        Assert.Null(disconnect.Received);
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData("disconnect_user")]
    [InlineData("alert_user\u000142")]
    [InlineData("progress_user_achievement\u000142:ACH")]
    public void TheCommandManagerRefusesMalformedLegacyInput(string request)
    {
        var (commands, disconnect, sent) = LegacyCommands();

        Assert.False(commands.Parse(request));
        Assert.Null(disconnect.Received);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task ACorrectSecretRunsTheCommand()
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new RecordingCommands(true);
        using var connection = Connect(sockets, commands, Secret);

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"disconnect_user\",\"parameters\":[\"42\"],\"secret\":\"" + Secret + "\"}\n"));

        Assert.Equal(0, (await ReadResponse(sockets.Client)).GetProperty("status").GetInt32());
        Assert.Equal("disconnect_user\u000142", commands.Request);
    }

    [Fact]
    public async Task ACorrectSecretRunsAnAcknowledgedCommand()
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new AcknowledgedCommands(_ => Task.FromResult(GrantOutcome.Success(null)));
        using var connection = Connect(sockets, commands, Secret);

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"secret\":\"" + Secret + "\",\"command\":\"give_user_badge\",\"parameters\":[\"42\",\"ADM\"]}\n"));

        Assert.Equal(0, (await ReadResponse(sockets.Client)).GetProperty("status").GetInt32());
        Assert.Equal(["42", "ADM"], commands.Received!);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"secret\":null")]
    [InlineData(",\"secret\":\"\"")]
    [InlineData(",\"secret\":\"0123456789abcdef0123456789abcdeX\"")]
    [InlineData(",\"secret\":\"0123456789ABCDEF0123456789ABCDEF\"")]
    [InlineData(",\"secret\":\"0123456789abcdef\"")]
    [InlineData(",\"secret\":\"0123456789abcdef0123456789abcdef0\"")]
    public async Task AMissingOrWrongSecretRunsNothingAndClosesTheConnection(string secretMember)
    {
        var invalid = await InvalidRequestMessage();
        using var sockets = await ConnectedSockets.Create();
        var commands = new RecordingCommands(true);
        using var connection = Connect(sockets, commands, Secret);

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"disconnect_user\",\"parameters\":[\"42\"]" + secretMember + "}\n"));

        var response = await ReadResponse(sockets.Client);
        Assert.Equal(1, response.GetProperty("status").GetInt32());
        Assert.Equal(invalid, response.GetProperty("message").GetString());
        await AssertClosed(sockets.Client);
        Assert.Null(commands.Request);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"secret\":\"0123456789abcdef0123456789abcdeX\"")]
    public async Task AMissingOrWrongSecretNeverRunsAnAcknowledgedCommand(string secretMember)
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new AcknowledgedCommands(_ => Task.FromResult(GrantOutcome.Success(null)));
        using var connection = Connect(sockets, commands, Secret);

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"grant_user_bundle\",\"parameters\":[\"42\",\"order-1\",\"AAAA\"]" + secretMember + "}\n"));

        Assert.Equal(1, (await ReadResponse(sockets.Client)).GetProperty("status").GetInt32());
        await AssertClosed(sockets.Client);
        Assert.Null(commands.Received);
    }

    [Fact]
    public async Task TheLegacyFormatIsRefusedWhenASecretIsSet()
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new RecordingCommands(true);
        using var connection = Connect(sockets, commands, Secret);

        await sockets.Client.SendAsync(Encoding.Default.GetBytes("disconnect_user\u000142"));

        await AssertClosed(sockets.Client);
        Assert.Null(commands.Request);
    }

    [Fact]
    public async Task TheLegacyFormatStillWorksWithoutASecret()
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new RecordingCommands(true);
        using var connection = Connect(sockets, commands, string.Empty);

        await sockets.Client.SendAsync(Encoding.Default.GetBytes("disconnect_user\u000142"));

        await AssertClosed(sockets.Client);
        Assert.Equal("disconnect_user\u000142", commands.Request);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"secret\":\"any value the website holds\"")]
    public async Task AServerWithoutASecretRunsRequestsWithOrWithoutTheSecretMember(string secretMember)
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new RecordingCommands(true);
        using var connection = Connect(sockets, commands, string.Empty);

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"disconnect_user\",\"parameters\":[\"42\"]" + secretMember + "}\n"));

        Assert.Equal(0, (await ReadResponse(sockets.Client)).GetProperty("status").GetInt32());
        Assert.Equal("disconnect_user\u000142", commands.Request);
    }

    [Fact]
    public async Task NeitherTheConfiguredNorAWrongSecretIsWrittenToTheLog()
    {
        const string wrong = "fedcba9876543210fedcba9876543210";
        var logger = new RecordingLogger();
        var requests = new (ICommandManager Commands, string Request)[]
        {
            (new RecordingCommands(true), "{\"command\":\"disconnect_user\",\"parameters\":[\"42\"],\"secret\":\"" + wrong + "\"}\n"),
            (new RecordingCommands(false), "{\"command\":\"disconnect_user\",\"parameters\":[\"42\"],\"secret\":\"" + Secret + "\"}\n"),
            (new AcknowledgedCommands(_ => throw new InvalidOperationException("failed")), "{\"command\":\"give_user_badge\",\"parameters\":[\"42\",\"ADM\"],\"secret\":\"" + Secret + "\"}\n"),
            (new RecordingCommands(true), "disconnect_user\u000142:" + Secret),
        };

        foreach (var (commands, request) in requests) {
            using var sockets = await ConnectedSockets.Create();
            using var connection = Connect(sockets, commands, Secret, logger);

            await sockets.Client.SendAsync(Encoding.UTF8.GetBytes(request));
            await AssertClosed(sockets.Client);
        }

        Assert.NotEmpty(logger.Entries);
        Assert.All(logger.Entries, entry => Assert.DoesNotContain(Secret, entry.Text));
        Assert.All(logger.Entries, entry => Assert.DoesNotContain(wrong, entry.Text));
    }

    [Fact]
    public void AnEmptySecretOnLoopbackStartsWithoutAWarning()
    {
        var logger = new RecordingLogger();

        Assert.Null(RconSocket.ReadSecret(new RconConfiguration { Hostname = "127.0.0.1" }, logger));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("10.0.0.5")]
    public void AnEmptySecretOnANonLoopbackAddressWarnsAtStartup(string hostname)
    {
        var logger = new RecordingLogger();

        Assert.Null(RconSocket.ReadSecret(new RconConfiguration { Hostname = hostname }, logger));
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Text.Contains("Rcon:Secret"));
    }

    [Fact]
    public void ASecretShorterThan32CharactersIsRejectedAtStartup()
    {
        var shortSecret = Secret[..31];

        var error = Assert.Throws<InvalidOperationException>(() =>
            RconSocket.ReadSecret(new RconConfiguration { Hostname = "127.0.0.1", Secret = shortSecret }, new RecordingLogger()));

        Assert.Contains("Rcon:Secret", error.Message);
        Assert.Contains("32", error.Message);
        Assert.DoesNotContain(shortSecret, error.Message);
    }

    [Fact]
    public async Task ASecretOf32CharactersOnANonLoopbackAddressStartsWithoutAWarning()
    {
        var logger = new RecordingLogger();
        var secret = RconSocket.ReadSecret(new RconConfiguration { Hostname = "0.0.0.0", Secret = Secret }, logger);
        using var sockets = await ConnectedSockets.Create();
        var commands = new RecordingCommands(true);
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, commands, new FakeTimeProvider(), secret);

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"disconnect_user\",\"parameters\":[\"42\"]}\n"));

        Assert.Equal(1, (await ReadResponse(sockets.Client)).GetProperty("status").GetInt32());
        Assert.Null(commands.Request);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    private static RconConnection Connect(ConnectedSockets sockets, ICommandManager commands, string secret, ILogger<RconConnection>? logger = null) =>
        new(sockets.Server, logger ?? NullLogger<RconConnection>.Instance, commands, new FakeTimeProvider(),
            RconSocket.ReadSecret(new RconConfiguration { Hostname = "127.0.0.1", Secret = secret }, NullLogger.Instance));

    private static async Task<string?> InvalidRequestMessage()
    {
        using var sockets = await ConnectedSockets.Create();
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, new RecordingCommands(true), new FakeTimeProvider());

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"not_registered\",\"parameters\":[]}\n"));

        return (await ReadResponse(sockets.Client)).GetProperty("message").GetString();
    }

    private static (CommandManager Commands, RecordingCommand Disconnect, List<(uint Header, byte[] Payload)> Sent) LegacyCommands()
    {
        var (client, sent) = HabbiconTestSupport.Client(new Habbo { Id = 42 });
        var clients = new TestGameClientManager(id => id == 42 ? client : null);
        var disconnect = new RecordingCommand("disconnect_user");
        var commands = new CommandManager([
            disconnect, new AlertUserCommand(clients), new ProgressUserAchievementCommand(clients, TestItemRuntime.Achievements),
        ]);

        return (commands, disconnect, sent);
    }

    private static async Task AssertClosed(Socket socket)
    {
        var buffer = new byte[256];

        try {
            while (await socket.ReceiveAsync(buffer.AsMemory()).AsTask().WaitAsync(HangGuard) > 0) { }
        }
        catch (SocketException) { }
    }

    private static async Task<JsonElement> ReadResponse(Socket socket)
    {
        using var stream = new NetworkStream(socket, ownsSocket: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var line = await reader.ReadLineAsync().WaitAsync(HangGuard);

        return JsonDocument.Parse(line!).RootElement.Clone();
    }

    [Fact]
    public async Task AnAcknowledgedCommandGetsItsLargeParameterAndRepliesWithItsOutcomeAndResult()
    {
        using var sockets = await ConnectedSockets.Create();
        var payload = new string('A', 10_924);
        var commands = new AcknowledgedCommands(_ => Task.FromResult(GrantOutcome.Success(new { balance = 7 })));
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, commands, new FakeTimeProvider());

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"grant_user_bundle\",\"parameters\":[\"42\",\"order-1\",\"" + payload + "\"]}\n"));

        var response = await ReadResponse(sockets.Client);
        Assert.Equal(0, response.GetProperty("status").GetInt32());
        Assert.Equal("ok", response.GetProperty("message").GetString());
        Assert.Equal(7, response.GetProperty("result").GetProperty("balance").GetInt32());
        Assert.False(response.TryGetProperty("detail", out _));
        Assert.Equal(["42", "order-1", payload], commands.Received!);
    }

    [Theory]
    [InlineData(0, "user_online")]
    [InlineData(1, "busy")]
    [InlineData(2, "internal_error")]
    public async Task AnAcknowledgedRefusalOrFailureCarriesItsStableIdAndNoResult(int failure, string expected)
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new AcknowledgedCommands(_ => failure switch
        {
            0 => Task.FromResult(GrantOutcome.Fail(GrantOutcome.UserOnline)),
            1 => throw new TimeoutException(),
            _ => throw new InvalidOperationException(),
        });
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, commands, new FakeTimeProvider());

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("{\"command\":\"take_user_badge\",\"parameters\":[\"42\",\"ACH\"]}\n"));

        var response = await ReadResponse(sockets.Client);
        Assert.Equal(1, response.GetProperty("status").GetInt32());
        Assert.Equal(expected, response.GetProperty("message").GetString());
        Assert.False(response.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task ARequestBeyondTheCapIsRejectedAsTooLarge()
    {
        using var sockets = await ConnectedSockets.Create();
        var commands = new AcknowledgedCommands(_ => throw new InvalidOperationException("dispatched"));
        using var connection = new RconConnection(sockets.Server, NullLogger<RconConnection>.Instance, commands, new FakeTimeProvider());
        var oversized = "{\"command\":\"grant_user_bundle\",\"parameters\":[\"42\",\"k\",\"" + new string('A', RconConnection.MaxRequestBytes) + "\"]}\n";

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes(oversized));

        Assert.Equal("too_large", (await ReadResponse(sockets.Client)).GetProperty("message").GetString());
        Assert.Null(commands.Received);
    }

    private sealed class AcknowledgedCommands(Func<string[], Task<GrantOutcome>> execute) : ICommandManager, IAcknowledgedRconCommand
    {
        public string[]? Received { get; private set; }
        public string Key => "acknowledged";
        string IRconCommand.Parameters => string.Empty;
        public string Description => string.Empty;

        public bool Parse(string data) => throw new InvalidOperationException("Acknowledged commands are not parsed.");
        public IAcknowledgedRconCommand Acknowledged(string command) => this;

        public Task<GrantOutcome> Execute(string[] parameters)
        {
            Received = parameters;

            return execute(parameters);
        }
    }

    private sealed class RecordingCommands(bool result) : ICommandManager
    {
        public string? Request { get; private set; }

        public bool Parse(string data)
        {
            Request = data;

            return result;
        }
    }

    private sealed class RecordingLogger : ILogger<RconConnection>
    {
        public ConcurrentQueue<(LogLevel Level, string Text)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue((logLevel, formatter(state, exception) + " " + state + " " + exception));
    }

    private sealed class ThrowingCommands : ICommandManager
    {
        public bool Parse(string data) => throw new InvalidOperationException("The command failed.");
    }

    private sealed class RecordingCommand(string key) : IRconCommand
    {
        public string[]? Received { get; private set; }
        public string Key => key;
        public string Parameters => string.Empty;
        public string Description => string.Empty;

        public Task<bool> TryExecute(string[] parameters)
        {
            Received = parameters;

            return Task.FromResult(true);
        }
    }

    private sealed class BlockingCommands : ICommandManager
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Parse(string data)
        {
            Entered.SetResult();
            Release.Task.GetAwaiter().GetResult();

            return true;
        }
    }

    private sealed class ConnectedSockets(Socket client, Socket server) : IDisposable
    {
        public Socket Client { get; } = client;
        public Socket Server { get; } = server;

        public static async Task<ConnectedSockets> Create()
        {
            using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);
            var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var connect = client.ConnectAsync(listener.LocalEndPoint!);
            var server = await listener.AcceptAsync();
            await connect;

            return new ConnectedSockets(client, server);
        }

        public void Dispose()
        {
            Client.Dispose();
            Server.Dispose();
        }
    }
}
