using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Plus.Communication.Abstractions;
using Plus.Communication.Encryption.Keys;
using Plus.Communication.Http;
using Plus.Communication.Http.Air;
using Plus.HabboHotel.Badges.Rarity;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Users.Authentication;
using Xunit;

namespace Plus.Tests;

/// <summary>The hotel side of the launcher's custom hotel contract (HabboCustomLauncher README, "Hotel-side contract").</summary>
public sealed class AirClientEndpointsTests : IAsyncLifetime
{
    private const string Visitor = "203.0.113.7";
    private static readonly HandshakeRsaKey AirKey = HandshakeRsaKey.FromPem(HandshakeRsaKey.GeneratePem());

    private readonly FakeTickets _tickets = new();
    private readonly FakeLauncherTokens _launcherTokens = new();
    private HttpClient _http = new();
    private ServiceProvider? _services;
    private IWebServer? _server;

    /// <summary>The throttle that game socket logins use.</summary>
    private ILoginThrottle SocketThrottle => _services!.GetRequiredService<ILoginThrottle>();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _http.Dispose();

        if (_server != null) {
            await _server.Stop();
        }

        _services?.Dispose();
    }

    /// <summary>Builds the web server and the login throttle with the production registration rules.</summary>
    private async Task Start(AirClientConfiguration configuration, IHandshakeRsaKeyProvider? key = null, IEnumerable<string>? trustedProxies = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new WebServerConfiguration { Port = 0, TrustedProxies = trustedProxies }));
        services.AddSingleton(Options.Create(configuration));
        services.AddSingleton(Options.Create(new LoginThrottleConfiguration { MaxFailuresPerIp = 3, WindowMinutes = 5, BlockMinutes = 10 }));
        services.AddSingleton<IBadgeRarityManager>(new NoRarity());
        services.AddSingleton<ICatalogFurnidata>(new NoFurnidata());
        services.AddSingleton(key ?? new FixedKey(AirKey, true));
        services.AddSingleton<ISsoTicketStore>(_tickets);
        services.AddSingleton<ILauncherTokenStore>(_launcherTokens);
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();
        Program.AddAssignableTo<ILoginThrottle>(services, typeof(Program).Assembly);
        Program.AddAssignableTo<IWebServer>(services, typeof(Program).Assembly);
        _services = services.BuildServiceProvider();
        _server = _services.GetRequiredService<IWebServer>();
        await _server.Start();
        _http.Dispose();
        _http = new HttpClient { BaseAddress = new Uri(_server.Urls.Single()) };
    }

    private static AirClientConfiguration Configured() => new() { UrlPrefix = "https://hotel.example", GameHost = "game.hotel.example", GamePorts = "30000,993" };

    private static async Task<JsonElement> Json(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private HttpRequestMessage Post(string path, string? bearer = null, string body = "{}", string? visitor = null, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        if (visitor != null) {
            request.Headers.Add("X-Real-IP", visitor);
        }

        if (forwardedFor != null) {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        if (bearer != null) {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return request;
    }

    [Fact]
    public async Task ContractCarriesWhatTheLauncherPatchesIn()
    {
        await Start(Configured());

        var response = await _http.GetAsync("/api/client/air.json");
        var contract = await Json(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, contract.GetProperty("version").GetInt32());
        Assert.Equal("en", contract.GetProperty("environment").GetString());
        Assert.Equal("game.hotel.example", contract.GetProperty("game").GetProperty("host").GetString());
        Assert.Equal("30000,993", contract.GetProperty("game").GetProperty("ports").GetString());
        Assert.Equal("https://hotel.example", contract.GetProperty("urls").GetProperty("prefix").GetString());
        Assert.Equal("https://hotel.example", contract.GetProperty("urls").GetProperty("webApi").GetString());
        Assert.Equal(AirKey.ModulusHex, contract.GetProperty("rsa").GetProperty("modulus").GetString());
        Assert.Equal(AirClientEndpoints.TicketPath, contract.GetProperty("ticket").GetProperty("url").GetString());
        Assert.False(contract.TryGetProperty("client", out _));
    }

    [Fact]
    public async Task ContractIncludesAPinnedClientAndExtraConfiguration()
    {
        var configuration = Configured();
        configuration.ClientVersion = "30";
        configuration.ClientUrl = "https://hotel.example/client/HabboWin.zip";
        configuration.Configuration["hotelview.banner.url"] = "https://hotel.example/gamedata/supersecret";
        await Start(configuration);

        var contract = await Json(await _http.GetAsync("/api/client/air.json"));

        Assert.Equal("30", contract.GetProperty("client").GetProperty("version").GetString());
        Assert.Equal("https://hotel.example/client/HabboWin.zip", contract.GetProperty("client").GetProperty("url").GetString());
        Assert.Equal("https://hotel.example/gamedata/supersecret", contract.GetProperty("configuration").GetProperty("hotelview.banner.url").GetString());
    }

    [Fact]
    public async Task NoContractWithoutAGameHost()
    {
        await Start(new AirClientConfiguration());

        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/api/client/air.json")).StatusCode);
    }

    [Fact]
    public async Task NoContractOnTheFallbackKeyTheClientCannotUse()
    {
        var legacy = new RsaKeys();
        await Start(Configured(), new FixedKey(HandshakeRsaKey.FromHex(legacy.N, legacy.E, legacy.D), false));

        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/api/client/air.json")).StatusCode);
    }

    [Fact]
    public async Task TicketNeedsALiveLauncherToken()
    {
        await Start(Configured());

        var missing = await _http.SendAsync(Post(AirClientEndpoints.TicketPath));
        var unknown = await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "not-a-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal("Bearer", missing.Headers.WwwAuthenticate.Single().Scheme);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Empty(_tickets.Issued);
    }

    [Fact]
    public async Task TicketIsAShortLivedGameTicketForTheTokensUser()
    {
        _launcherTokens.Tokens["launcher-7"] = 7;
        await Start(Configured());

        var response = await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "launcher-7"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (userId, ticket, lifetime) = Assert.Single(_tickets.Issued);
        Assert.Equal(7, userId);
        Assert.Equal(TimeSpan.FromSeconds(60), lifetime);
        Assert.Equal(ticket, (await Json(response)).GetProperty("ticket").GetString());
    }

    [Fact]
    public async Task ALiveGameTicketTradesOnceForALauncherToken()
    {
        _tickets.Live["web-ticket"] = 9;
        await Start(Configured());

        var first = await _http.SendAsync(Post(AirClientEndpoints.LauncherTokenPath, body: "{\"ticket\":\"web-ticket\"}"));
        var replay = await _http.SendAsync(Post(AirClientEndpoints.LauncherTokenPath, body: "{\"ticket\":\"web-ticket\"}"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var token = (await Json(first)).GetProperty("token").GetString()!;
        Assert.Equal(9, _launcherTokens.Tokens[token]);
        Assert.Equal(TimeSpan.FromDays(30), _launcherTokens.Lifetimes[token]);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task LauncherTokenExchangeNeedsATicket()
    {
        await Start(Configured());

        var response = await _http.SendAsync(Post(AirClientEndpoints.LauncherTokenPath, body: "not json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_launcherTokens.Tokens);
    }

    [Fact]
    public async Task DeleteRevokesTheLauncherToken()
    {
        _launcherTokens.Tokens["launcher-7"] = 7;
        await Start(Configured());

        var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Delete, AirClientEndpoints.LauncherTokenPath)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", "launcher-7") }
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "launcher-7"))).StatusCode);
    }

    [Fact]
    public async Task WrongTicketsBlockTheVisitorBeforeTheNextTicketIsUsed()
    {
        _tickets.Live["web-ticket"] = 9;
        await Start(Configured());

        for (var attempt = 0; attempt < 3; attempt++) {
            var wrong = await _http.SendAsync(Post(AirClientEndpoints.LauncherTokenPath, body: $"{{\"ticket\":\"guess-{attempt}\"}}", visitor: Visitor));
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var blocked = await _http.SendAsync(Post(AirClientEndpoints.LauncherTokenPath, body: "{\"ticket\":\"web-ticket\"}", visitor: Visitor));

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal(WebErrors.TooManyRequests, (await Json(blocked)).GetProperty("code").GetString());
        Assert.True(_tickets.Live.ContainsKey("web-ticket"));
        Assert.Empty(_launcherTokens.Tokens);
    }

    [Fact]
    public async Task WrongLauncherTokensBlockTheVisitorBeforeTheNextTicketIsIssued()
    {
        _launcherTokens.Tokens["launcher-7"] = 7;
        await Start(Configured());

        for (var attempt = 0; attempt < 3; attempt++) {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, $"guess-{attempt}", visitor: Visitor))).StatusCode);
        }

        var blocked = await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "launcher-7", visitor: Visitor));

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal(WebErrors.TooManyRequests, (await Json(blocked)).GetProperty("code").GetString());
        Assert.Empty(_tickets.Issued);
    }

    [Fact]
    public async Task BothEndpointsCountAgainstOneLimit()
    {
        _tickets.Live["web-ticket"] = 9;
        await Start(Configured());

        await _http.SendAsync(Post(AirClientEndpoints.LauncherTokenPath, body: "{\"ticket\":\"guess-1\"}", visitor: Visitor));
        await _http.SendAsync(Post(AirClientEndpoints.LauncherTokenPath, body: "{\"ticket\":\"guess-2\"}", visitor: Visitor));
        await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "guess-3", visitor: Visitor));

        var blocked = await _http.SendAsync(Post(AirClientEndpoints.LauncherTokenPath, body: "{\"ticket\":\"web-ticket\"}", visitor: Visitor));

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task WrongTriesOnTheAirEndpointsDoNotBlockASocketLoginFromTheSameAddress()
    {
        await Start(Configured());

        for (var attempt = 0; attempt < 3; attempt++) {
            await _http.SendAsync(Post(AirClientEndpoints.TicketPath, $"guess-{attempt}", visitor: Visitor));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "guess", visitor: Visitor))).StatusCode);
        Assert.False(SocketThrottle.IsBlocked(new ClientAddress(IPAddress.Parse(Visitor), true)));
    }

    [Fact]
    public async Task ManyAddressesOnTheAirEndpointsDoNotStopTheSocketThrottleTrackingANewAddress()
    {
        await Start(Configured());

        for (var attempt = 0; attempt < 3; attempt++) {
            await _http.SendAsync(Post(AirClientEndpoints.TicketPath, $"guess-{attempt}", visitor: Visitor));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "guess", visitor: Visitor))).StatusCode);

        // More distinct addresses than one throttle tracks.
        foreach (var batch in Enumerable.Range(0, 10_000).Chunk(100)) {
            await Task.WhenAll(batch.Select(address => _http.SendAsync(Post(AirClientEndpoints.TicketPath, "guess", visitor: $"10.0.{address / 256}.{address % 256}"))));
        }

        var newcomer = new ClientAddress(IPAddress.Parse("198.51.100.200"), true);

        for (var attempt = 0; attempt < 3; attempt++) {
            SocketThrottle.RecordFailure(newcomer);
        }

        Assert.True(SocketThrottle.IsBlocked(newcomer));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "guess", visitor: Visitor))).StatusCode);
    }

    [Fact]
    public async Task ABlockedVisitorDoesNotBlockAnother()
    {
        _launcherTokens.Tokens["launcher-7"] = 7;
        await Start(Configured());

        for (var attempt = 0; attempt < 3; attempt++) {
            await _http.SendAsync(Post(AirClientEndpoints.TicketPath, $"guess-{attempt}", visitor: Visitor));
        }

        Assert.Equal(HttpStatusCode.OK, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "launcher-7", visitor: "203.0.113.8"))).StatusCode);
    }

    [Fact]
    public async Task SuccessfulRequestsDoNotCountAsFailures()
    {
        _launcherTokens.Tokens["launcher-7"] = 7;
        await Start(Configured());

        for (var attempt = 0; attempt < 5; attempt++) {
            Assert.Equal(HttpStatusCode.OK, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "launcher-7", visitor: Visitor))).StatusCode);
        }
    }

    [Fact]
    public async Task AnUntrustedPeerCannotEscapeTheLimitWithForgedAddresses()
    {
        await Start(Configured(), trustedProxies: ["10.0.0.2"]);

        for (var attempt = 0; attempt < 3; attempt++) {
            await _http.SendAsync(Post(AirClientEndpoints.TicketPath, $"guess-{attempt}", visitor: $"198.51.100.{attempt + 1}"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "guess", visitor: "198.51.100.9"))).StatusCode);
    }

    [Fact]
    public async Task AProxyThatNamesNoVisitorIsNeverBlocked()
    {
        await Start(Configured());

        for (var attempt = 0; attempt < 5; attempt++) {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, $"guess-{attempt}"))).StatusCode);
        }
    }

    [Fact]
    public async Task AProxyThatSendsOnlyXForwardedForIsNeverCountedOrBlocked()
    {
        _launcherTokens.Tokens["launcher-7"] = 7;
        await Start(Configured());

        for (var attempt = 0; attempt < 5; attempt++) {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, $"guess-{attempt}", forwardedFor: Visitor))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "launcher-7", visitor: Visitor))).StatusCode);
    }

    [Fact]
    public async Task AProxyIsCountedByXRealIpAndNotByXForwardedFor()
    {
        _launcherTokens.Tokens["launcher-7"] = 7;
        await Start(Configured());

        for (var attempt = 0; attempt < 3; attempt++) {
            await _http.SendAsync(Post(AirClientEndpoints.TicketPath, $"guess-{attempt}", visitor: Visitor, forwardedFor: $"198.51.100.{attempt + 1}"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "launcher-7", visitor: Visitor, forwardedFor: "198.51.100.9"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _http.SendAsync(Post(AirClientEndpoints.TicketPath, "launcher-7", visitor: "203.0.113.8", forwardedFor: Visitor))).StatusCode);
    }

    private sealed class FixedKey(HandshakeRsaKey key, bool configured) : IHandshakeRsaKeyProvider
    {
        public HandshakeRsaKey Key => key;
        public bool IsConfigured => configured;
    }

    private sealed class FakeTickets : ISsoTicketStore
    {
        public Dictionary<string, int> Live { get; } = new();
        public List<(int UserId, string Ticket, TimeSpan Lifetime)> Issued { get; } = new();

        public Task<int?> Consume(string ticket) => Task.FromResult(Live.Remove(ticket, out var userId) ? userId : (int?)null);

        public Task<string?> Issue(int userId, TimeSpan lifetime)
        {
            var ticket = "ticket-" + Guid.NewGuid().ToString("N");
            Issued.Add((userId, ticket, lifetime));

            return Task.FromResult<string?>(ticket);
        }

        public Task Revoke(int userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeLauncherTokens : ILauncherTokenStore
    {
        public Dictionary<string, int> Tokens { get; } = new();
        public Dictionary<string, TimeSpan> Lifetimes { get; } = new();

        public Task<string?> Issue(int userId, TimeSpan lifetime)
        {
            var token = "launcher-" + Guid.NewGuid().ToString("N");
            Tokens[token] = userId;
            Lifetimes[token] = lifetime;

            return Task.FromResult<string?>(token);
        }

        public Task<int?> Resolve(string token) => Task.FromResult(Tokens.TryGetValue(token, out var userId) ? userId : (int?)null);

        public Task Revoke(string token)
        {
            Tokens.Remove(token);

            return Task.CompletedTask;
        }
    }

    private sealed class NoFurnidata : ICatalogFurnidata
    {
        public CatalogFurnidataFile? Current() => null;
    }

    private sealed class NoRarity : IBadgeRarityManager
    {
        public BadgeLeaderboardSnapshot Snapshot => BadgeLeaderboardSnapshot.Empty;
        public Task Refresh() => Task.CompletedTask;
        public Task<LeaderboardProfile?> GetProfile(int userId) => Task.FromResult<LeaderboardProfile?>(null);
    }
}
