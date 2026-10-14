using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Plus.Communication.Http;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.Badges.Rarity;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.WebApi;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

/// <summary>The Variables Web API add-on (2002): server-made keys and the HTTP variable API they open.</summary>
public sealed class WiredWebApiTests : IAsyncLifetime
{
    private WebServer? _server;
    private HttpClient _http = new();
    private readonly RecordingApi _api = new();

    public async Task InitializeAsync()
    {
        _server = new WebServer(Options.Create(new WebServerConfiguration { Port = 0 }), new NoRarity(), new NoFurnidata(), _api);
        await _server.Start();
        _http = new HttpClient { BaseAddress = new Uri(_server.Urls.Single()) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();

        if (_server != null) {
            await _server.Stop();
        }
    }

    [Fact]
    public void KeyResultPacketFollowsClass4132()
    {
        var packet = new HabbiconTestSupport.RecordingPacket();
        new WiredWebApiKeyComposer(12, true, "k").Compose(packet);

        Assert.Equal(new object[] { 12u, true, "k" }, packet.Writes);
        Assert.Equal(467u, new WiredWebApiKeyComposer(0, false, "").MessageId);
    }

    [Fact]
    public void KeysAreRandomAndOnlyTheirHashesAreKept()
    {
        var keys = Enumerable.Range(0, 50).Select(_ => WiredWebApiKeyFormat.Create()).ToArray();

        Assert.All(keys, key => Assert.True(WiredWebApiKeyFormat.IsKey(key)));
        Assert.Equal(keys.Length, keys.Distinct().Count());
        Assert.Equal(64, DatabaseWiredWebApiStore.Hash(keys[0]).Length);
        Assert.NotEqual(DatabaseWiredWebApiStore.Hash(keys[0]), DatabaseWiredWebApiStore.Hash(keys[1]));
    }

    [Fact]
    public async Task RequestsWithoutABearerKeyAreUnauthorized()
    {
        var response = await _http.GetAsync("/api/wired/variables");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(WiredWebApiService.Unauthorized, (await Json(response)).GetProperty("code").GetString());
        Assert.Empty(_api.Calls);
    }

    [Fact]
    public async Task TheBearerKeyReachesTheServiceForReadsWritesAndClears()
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "abc");

        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/api/wired/variables")).StatusCode);
        var put = await _http.PutAsync("/api/wired/variables/user:9", new StringContent("{\"value\": 5000000000, \"holderId\": 7}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.PutAsync("/api/wired/variables/user:9", new StringContent("{\"value\": \"x\"}", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _http.DeleteAsync("/api/wired/variables/user:9")).StatusCode);

        Assert.Equal(["read abc", "write abc user:9 5000000000 7", "clear abc user:9"], _api.Calls);
    }

    [Fact]
    public void UnknownKeysWriteRefusalsAndUnloadedRoomsAreTheirOwnErrors()
    {
        var store = new FixedStore { Grant = null };
        var api = new WiredWebApiService(EditorTestSupport.UntouchableDatabase(), DispatchProxy.Create<IRoomManager, NoRooms>()) { StoreOverride = store };

        Assert.Equal(401, api.Read("nope").Status);

        store.Grant = new(5, 42, false, false);
        Assert.Equal((403, WiredWebApiService.Forbidden), (api.Write("k", "user:1", 1, 0).Status, api.Write("k", "user:1", 1, 0).Code));
        Assert.Equal((409, WiredWebApiService.RoomNotLoaded), (api.Read("k").Status, api.Read("k").Code));

        store.Grant = new(5, 42, true, false);
        Assert.Equal(403, api.Clear("k", "user:1").Status);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private sealed class RecordingApi : IWiredWebApiService
    {
        public List<string> Calls { get; } = [];
        public WiredWebApiResult Read(string key) => Record($"read {key}");
        public WiredWebApiResult Write(string key, string variableId, long value, int holderId) => Record($"write {key} {variableId} {value} {holderId}");
        public WiredWebApiResult Clear(string key, string variableId) => Record($"clear {key} {variableId}");

        private WiredWebApiResult Record(string call)
        {
            Calls.Add(call);

            return WiredWebApiResult.Ok(new { ok = true });
        }
    }

    private sealed class FixedStore : IWiredWebApiStore
    {
        public WiredWebApiGrant? Grant { get; set; }
        public WiredWebApiGrant? Find(string key) => Grant;
        public void Issue(uint itemId, uint roomId, bool read, string key) => throw new NotSupportedException();
        public (string? ReadHash, string? WriteHash) Hashes(uint itemId) => throw new NotSupportedException();
        public void Saved(uint itemId, uint roomId, string readKey, string writeKey, bool bulkDelete) => throw new NotSupportedException();
    }

    public class NoRooms : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name == nameof(IRoomManager.TryGetRoom)
            ? false : throw new NotSupportedException(targetMethod?.Name);
    }

    private sealed class NoRarity : IBadgeRarityManager
    {
        public BadgeLeaderboardSnapshot Snapshot => BadgeLeaderboardSnapshot.Empty;
        public Task Refresh() => Task.CompletedTask;
        public Task<LeaderboardProfile?> GetProfile(int userId) => Task.FromResult<LeaderboardProfile?>(null);
    }

    private sealed class NoFurnidata : ICatalogFurnidata
    {
        public CatalogFurnidataFile? Current() => null;
    }
}
