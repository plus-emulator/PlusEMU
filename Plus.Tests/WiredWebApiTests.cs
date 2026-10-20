using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Plus.Communication.Http;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.Badges.Rarity;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Items.Wired.WebApi;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The Variables Web API add-on (2002) and Habbo's public Wired Variables Web API it opens. Every JSON answer is checked against the
/// official spec (Fixtures/HabboWiredVariablesOpenApi.json, the "Wired Variables" part of habbo.com /api/public/api-docs/), and the
/// error bodies against what habbo.com answers without a key.
/// </summary>
public sealed class WiredWebApiTests : IAsyncLifetime
{
    private const uint RoomId = 1;
    private const uint OtherRoomId = 2;
    private const uint AddonId = 7;

    private static readonly JsonElement Spec = JsonDocument.Parse(File.ReadAllText(HabbiconPacketTests.Repo("Plus.Tests/Fixtures/HabboWiredVariablesOpenApi.json"))).RootElement;

    // Error bodies habbo.com answers that the spec's examples do not list.
    private static readonly string[] ObservedErrors = [WiredWebApiErrors.Missing, WiredWebApiErrors.RequestFailed];

    private readonly string _readKey = WiredWebApiKeyFormat.Create();
    private readonly string _writeKey = WiredWebApiKeyFormat.Create();
    private readonly FakeStore _store = new();
    private readonly FakeDirectory _directory = new();
    private readonly FakeRoom _room = new();
    private WebServer? _server;
    private HttpClient _http = new();

    private static readonly WiredWebApiVariable Score = new(10, "score", WiredVariableTarget.User, true, false);
    private static readonly WiredWebApiVariable Vip = new(11, "vip", WiredVariableTarget.User, false, false);
    private static readonly WiredWebApiVariable Locked = new(12, "locked", WiredVariableTarget.User, true, true);
    private static readonly WiredWebApiVariable Charge = new(20, "charge", WiredVariableTarget.Furni, true, false);
    private static readonly WiredWebApiVariable Round = new(30, "round", WiredVariableTarget.Global, true, false);

    public async Task InitializeAsync()
    {
        _store.Keys[RoomId] = [new(AddonId, DatabaseWiredWebApiStore.Hash(_readKey), DatabaseWiredWebApiStore.Hash(_writeKey), false)];
        _directory.Rooms.UnionWith([RoomId, OtherRoomId]);
        _directory.Loaded[RoomId] = _room;
        _directory.Users[44] = "alice";
        _directory.Users[45] = "bob";
        _room.Variables_.AddRange([Score, Vip, Locked, Charge, Round]);
        _room.Users[44] = "alice";
        _room.Pets[12] = ("Rex", -3);
        _room.Floor.Add(22);
        _room.Wall.Add(91);
        _room.Set(Score, 44, 7);
        _room.Set(Score, 45, 3);
        _room.Set(Charge, 22, 9);
        _room.Set(Round, 0, 2);

        var service = new WiredWebApiService(_store, _directory, TimeProvider.System);
        _server = new WebServer(Options.Create(new WebServerConfiguration { Port = 0 }), new NoRarity(), new NoFurnidata(), service);
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
        Assert.Equal(3954u, new WiredWebApiKeyComposer(0, false, "").MessageId);
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
    public async Task EveryOperationOfTheSpecIsRouted()
    {
        foreach (var path in Spec.GetProperty("paths").EnumerateObject()) {
            foreach (var operation in path.Value.EnumerateObject()) {
                var url = path.Name.Replace("{roomId}", "1").Replace("{scope}", "user").Replace("{variableName}", "score")
                    .Replace("{targetKind}", "users").Replace("{entityId}", "44");
                var (status, body, _) = await Call(operation.Name.ToUpperInvariant(), url, "{}");

                Assert.True(body.ValueKind == JsonValueKind.Object, $"{operation.Name} {path.Name} is not routed");
                Assert.Contains(status, new[] { 400, 403 });
            }
        }
    }

    [Fact]
    public async Task ErrorsMatchWhatHabboAnswersWithoutAKey()
    {
        // Observed on habbo.com: a key that the room has none of, or none at all, is key_missing; Bearer is not read.
        await AssertError("GET", "/api/public/rooms/1/variables", null, 403, WiredWebApiErrors.KeyMissing);
        await AssertError("GET", "/api/public/rooms/2/variables", null, 403, WiredWebApiErrors.KeyMissing, read: _readKey);
        await AssertError("GET", "/api/public/rooms/1/variables", null, 403, WiredWebApiErrors.KeyMissing, write: _writeKey);
        await AssertError("GET", "/api/public/rooms/1/variables", null, 403, WiredWebApiErrors.KeyMissing, bearer: _readKey);
        await AssertError("GET", "/api/public/rooms/1/variables", null, 403, WiredWebApiErrors.KeyInvalid, read: WiredWebApiKeyFormat.Create());
        await AssertError("PUT", "/api/public/rooms/1/variables/user/score/users/44", "{\"value\":\"1\"}", 403, WiredWebApiErrors.KeyMissing, read: _readKey);
        await AssertError("GET", "/api/public/rooms/1/variables_profile/global", null, 403, WiredWebApiErrors.KeyMissing);

        // Room first, then the path and the body, then the key.
        await AssertError("GET", "/api/public/rooms/2147483000/variables", null, 404, WiredWebApiErrors.Missing);
        await AssertError("GET", "/api/public/rooms/abc/variables", null, 404, WiredWebApiErrors.Missing, read: "x");
        await AssertError("GET", "/api/public/rooms/1/variables/user/score/badkind/1", null, 400, WiredWebApiErrors.InvalidTarget, read: "x");
        await AssertError("GET", "/api/public/rooms/1/variables/user/score/furni/1", null, 400, WiredWebApiErrors.InvalidTarget, read: "x");
        await AssertError("GET", "/api/public/rooms/1/variables/user/score/users/abc", null, 404, WiredWebApiErrors.Missing, read: "x");
        await AssertError("PATCH", "/api/public/rooms/1/variables/user/score/users/1", "{\"value\":1.5}", 400, WiredWebApiErrors.RequestFailed, write: "x");
        await AssertError("PATCH", "/api/public/rooms/1/variables/user/score/users/1", "{\"value\":\"abc\"}", 400, WiredWebApiErrors.RequestFailed, write: "x");
        await AssertError("PUT", "/api/public/rooms/1/variables/user/score/users/1", "{}", 400, WiredWebApiErrors.InvalidValue, write: "x");
        await AssertError("POST", "/api/public/rooms/1/variables/bulk-delete", "{\"variables\":[]}", 400, WiredWebApiErrors.BulkDeleteEmpty);
        await AssertError("POST", "/api/public/rooms/1/variables/user/score/batch", "{\"requests\":[]}", 400, WiredWebApiErrors.BatchEmpty, read: "x", write: "x");

        await AssertPage("GET", "/api/public/rooms/1/variables/badscope/foo/users/1");
        await AssertPage("POST", "/api/public/rooms/1/variables");
        await AssertPage("GET", "/api/public/rooms/1/variables/user");
    }

    [Fact]
    public async Task TheOldRouteIsGone()
    {
        var response = await _http.GetAsync("/api/wired/variables");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not_found", JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TheDocsPageIsHabbosSwaggerUiPage()
    {
        // habbo.com redirects the bare path to the slash form with its framework's 301 page.
        using var noRedirect = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = _http.BaseAddress };
        var redirect = await noRedirect.GetAsync("/api/public/api-docs");
        Assert.Equal(HttpStatusCode.MovedPermanently, redirect.StatusCode);
        Assert.Equal("/api/public/api-docs/", redirect.Headers.Location?.OriginalString);
        Assert.Contains("<pre>Redirecting to /api/public/api-docs/</pre>", await redirect.Content.ReadAsStringAsync());

        var page = await _http.GetAsync("/api/public/api-docs/");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html; charset=utf-8", page.Content.Headers.ContentType?.ToString());
        Assert.Contains("<title>Swagger UI</title>", html);
        Assert.Contains("<div id=\"swagger-ui\"></div>", html);

        // Every file the page loads is served from the emulator itself.
        foreach (var file in Regex.Matches(html, "(?:href|src)=\"\\./([^\"]+)\"").Select(match => match.Groups[1].Value)) {
            var asset = await _http.GetAsync("/api/public/api-docs/" + file);
            Assert.True(asset.StatusCode == HttpStatusCode.OK, file);
            Assert.NotEmpty(await asset.Content.ReadAsByteArrayAsync());
        }

        Assert.Equal("text/css; charset=utf-8", (await _http.GetAsync("/api/public/api-docs/swagger-ui.css")).Content.Headers.ContentType?.ToString());
        await AssertPage("GET", "/api/public/api-docs/index.html");
        await AssertPage("GET", "/api/public/api-docs/package.json");
    }

    [Fact]
    public async Task TheDocsListExactlyTheRoutedOperationsAsHabboDocumentsThem()
    {
        var spec = JsonNode.Parse(WebApiDocsEndpoints.Spec)!;
        var habbo = JsonNode.Parse(File.ReadAllText(HabbiconPacketTests.Repo("Plus.Tests/Fixtures/HabboWiredVariablesOpenApi.json")))!;

        foreach (var part in new[] { "openapi", "tags", "paths", "components" }) {
            Assert.True(JsonNode.DeepEquals(habbo[part], spec[part]), $"{part} differs from habbo.com's spec");
        }

        // No servers entry: the default server "/" is the page's own origin.
        Assert.Null(spec["servers"]);

        var documented = spec["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject().Select(operation => operation.Key.ToUpperInvariant() + " " + path.Key))
            .Order(StringComparer.Ordinal);
        var routed = _server!.Services!.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/api/public/rooms/", StringComparison.Ordinal))
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Select(method => method + " " + endpoint.RoutePattern.RawText))
            .Order(StringComparer.Ordinal);
        Assert.Equal(documented, routed);

        // The page's swagger-ui-init.js embeds that spec.
        var script = await _http.GetStringAsync("/api/public/api-docs/swagger-ui-init.js");
        var embedded = Regex.Match(script, "\"swaggerDoc\": (\\{.*?\\n  \\}),\\n  \"customOptions\"", RegexOptions.Singleline).Groups[1].Value;
        Assert.True(JsonNode.DeepEquals(spec, JsonNode.Parse(embedded)));
        Assert.Contains("\"supportedSubmitMethods\": []", script);
    }

    [Fact]
    public async Task ListsTheRoomsVariableNamesByScope()
    {
        var (status, body, _) = await Call("GET", "/api/public/rooms/1/variables", read: _readKey);

        Assert.Equal(200, status);
        Assert.Equal(new[] { "score", "vip", "locked" }, Strings(body.GetProperty("users")));
        Assert.Equal(new[] { "charge" }, Strings(body.GetProperty("furni")));
        Assert.Equal(new[] { "round" }, Strings(body.GetProperty("global")));
    }

    [Fact]
    public async Task ReadsOneValueAsADecimalStringWithItsTimes()
    {
        var (status, body, _) = await Call("GET", "/api/public/rooms/1/variables/user/score/users/44", read: _readKey);

        Assert.Equal(200, status);
        Assert.Equal("7", body.GetProperty("value").GetString());
        Assert.Equal("2023-11-16T22:13:20Z", body.GetProperty("creation_time").GetString());

        // A user who left keeps their permanent value.
        Assert.Equal("3", (await Call("GET", "/api/public/rooms/1/variables/user/score/users/45", read: _readKey)).Body.GetProperty("value").GetString());
        await AssertError("GET", "/api/public/rooms/1/variables/user/score/users/46", null, 404, WiredWebApiErrors.NotFound, read: _readKey);
        await AssertError("GET", "/api/public/rooms/1/variables/user/nope/users/44", null, 404, WiredWebApiErrors.NotFound, read: _readKey);
        await AssertError("GET", "/api/public/rooms/1/variables/furni/charge/wall-items/22", null, 404, WiredWebApiErrors.NotFound, read: _readKey);
        Assert.Equal("9", (await Call("GET", "/api/public/rooms/1/variables/furni/charge/furni/22", read: _readKey)).Body.GetProperty("value").GetString());

        // A variable without a value has only its times.
        _room.Set(Vip, 44, 1);
        Assert.False((await Call("GET", "/api/public/rooms/1/variables/user/vip/users/44", read: _readKey)).Body.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task PutGivesAndSetsPatchOnlyUpdates()
    {
        await AssertError("PATCH", "/api/public/rooms/1/variables/user/score/pets/12", "{\"value\":\"5\"}", 404, WiredWebApiErrors.NotFound, write: _writeKey);

        var (status, body, _) = await Call("PUT", "/api/public/rooms/1/variables/user/score/pets/12", "{\"value\":\"-9007199254740993\"}", write: _writeKey);
        Assert.Equal(200, status);
        Assert.Equal("-9007199254740993", body.GetProperty("value").GetString());

        Assert.Equal("8", (await Call("PATCH", "/api/public/rooms/1/variables/user/score/pets/12", "{\"value\":\"8\"}", write: _writeKey)).Body.GetProperty("value").GetString());
        Assert.Equal(8, _room.Get(Score, -3));

        await AssertError("PUT", "/api/public/rooms/1/variables/user/score/users/45", "{\"value\":\"1\"}", 403, WiredWebApiErrors.UserNotParticipating, write: _writeKey);
        await AssertError("PUT", "/api/public/rooms/1/variables/user/locked/users/44", "{\"value\":\"1\"}", 403, WiredWebApiErrors.OperationNotAllowed, write: _writeKey);
        await AssertError("PUT", "/api/public/rooms/1/variables/furni/charge/furni-bc/22", "{\"value\":\"1\"}", 404, WiredWebApiErrors.NotFound, write: _writeKey);
        await AssertError("PUT", "/api/public/rooms/1/variables/user/score/users/44", "{\"value\":\"1\"}", 403, WiredWebApiErrors.KeyInvalid, write: _readKey);
    }

    [Fact]
    public async Task DeleteRemovesOneValueEvenOfAUserWhoLeft()
    {
        Assert.Equal(204, (await Call("DELETE", "/api/public/rooms/1/variables/user/score/users/45", write: _writeKey)).Status);
        Assert.Null(_room.Get(Score, 45));
        await AssertError("DELETE", "/api/public/rooms/1/variables/user/score/users/45", null, 404, WiredWebApiErrors.NotFound, write: _writeKey);
    }

    [Fact]
    public async Task ListsPagesAndCountsTheValuesOfOneTargetKind()
    {
        _room.Users[50] = "carol";
        _room.Set(Score, 50, 11);

        var (status, body, _) = await Call("GET", "/api/public/rooms/1/variables/user/score/users?order_by=value&order_dir=desc&size=2", read: _readKey);
        Assert.Equal(200, status);
        Assert.Equal((1, 2), (body.GetProperty("page").GetInt32(), body.GetProperty("size").GetInt32()));
        Assert.Equal(new[] { 50L, 44L }, body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()));
        Assert.Equal("11", body.GetProperty("items")[0].GetProperty("value").GetString());

        var second = (await Call("GET", "/api/public/rooms/1/variables/user/score/users?order_by=value&order_dir=desc&size=2&page=2", read: _readKey)).Body;
        Assert.Equal(new[] { 45L }, second.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()));
        Assert.Equal(WiredWebApiService.MaxPageSize, (await Call("GET", "/api/public/rooms/1/variables/user/score/users?size=1000", read: _readKey)).Body.GetProperty("size").GetInt32());

        Assert.Equal(3, (await Call("GET", "/api/public/rooms/1/variables/user/score/users/count", read: _readKey)).Body.GetProperty("count").GetInt32());
        Assert.Equal(0, (await Call("GET", "/api/public/rooms/1/variables/user/score/bots/count", read: _readKey)).Body.GetProperty("count").GetInt32());
        Assert.Equal(0, (await Call("GET", "/api/public/rooms/1/variables/user/score/pets/count", read: _readKey)).Body.GetProperty("count").GetInt32());
        await AssertError("GET", "/api/public/rooms/1/variables/user/nope/users/count", null, 404, WiredWebApiErrors.NotFound, read: _readKey);
    }

    [Fact]
    public async Task BulkDeleteNeedsTheAddonsPermissionAndKnownVariables()
    {
        await AssertError("POST", "/api/public/rooms/1/variables/bulk-delete", "{\"variables\":[\"score\"]}", 403, WiredWebApiErrors.BulkDeleteNotEnabled, write: _writeKey);

        _store.Keys[RoomId] = [_store.Keys[RoomId][0] with { BulkDelete = true }];
        await AssertError("POST", "/api/public/rooms/1/variables/bulk-delete", "{\"variables\":[\"score\",\"round\"]}", 400, WiredWebApiErrors.BulkDeleteInvalidVariable, write: _writeKey);
        Assert.Equal(7, _room.Get(Score, 44));

        Assert.Equal(204, (await Call("POST", "/api/public/rooms/1/variables/bulk-delete", "{\"variables\":[\"score\",\"charge\"]}", write: _writeKey)).Status);
        Assert.Null(_room.Get(Score, 44));
        Assert.Null(_room.Get(Charge, 22));
        Assert.Equal(2, _room.Get(Round, 0));
    }

    [Fact]
    public async Task BatchAnswersEveryOperationOnItsOwn()
    {
        var request = """
            {"requests":[
              {"op_id":"get-user","method":"GET","path":"users/44"},
              {"op_id":"put-pet","method":"PUT","path":"pets/12","body":{"value":"5"}},
              {"op_id":"delete-user","method":"DELETE","path":"users/45"},
              {"op_id":"invalid-target","method":"GET","path":"furni/22"},
              {"method":"PATCH","path":"users/44"}
            ]}
            """;
        var (status, body, _) = await Call("POST", "/api/public/rooms/1/variables/user/score/batch", request, read: _readKey, write: _writeKey);
        var results = body.GetProperty("results").EnumerateArray().ToArray();

        Assert.Equal(200, status);
        Assert.Equal(new[] { 200, 200, 204, 400, 400 }, results.Select(x => x.GetProperty("status").GetInt32()));
        Assert.Equal("7", results[0].GetProperty("body").GetProperty("value").GetString());
        Assert.Equal("get-user", results[0].GetProperty("op_id").GetString());
        Assert.Equal(JsonValueKind.Null, results[4].GetProperty("op_id").ValueKind);
        Assert.Equal(WiredWebApiErrors.InvalidTarget, results[3].GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(WiredWebApiErrors.InvalidTarget, results[3].GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(WiredWebApiErrors.InvalidValue, results[4].GetProperty("error").GetProperty("code").GetString());

        // With only the read key, writes in the batch are refused one by one.
        var readOnly = (await Call("POST", "/api/public/rooms/1/variables/user/score/batch", request, read: _readKey)).Body.GetProperty("results");
        Assert.Equal(new[] { 200, 403 }, readOnly.EnumerateArray().Take(2).Select(x => x.GetProperty("status").GetInt32()));
        Assert.Equal(WiredWebApiErrors.KeyMissing, readOnly[1].GetProperty("error").GetProperty("code").GetString());

        var tooMany = "{\"requests\":[" + string.Join(",", Enumerable.Repeat("{\"method\":\"GET\",\"path\":\"users/44\"}", 51)) + "]}";
        await AssertError("POST", "/api/public/rooms/1/variables/user/score/batch", tooMany, 400, WiredWebApiErrors.BatchLimitExceeded, read: _readKey);
        await AssertError("POST", "/api/public/rooms/1/variables/user/nope/batch", request, 404, WiredWebApiErrors.NotFound, read: _readKey);
        await AssertError("POST", "/api/public/rooms/1/variables/user/score/batch", request, 403, WiredWebApiErrors.KeyMissing);
    }

    [Fact]
    public async Task GlobalVariablesAreReadAndPatched()
    {
        Assert.Equal("2", (await Call("GET", "/api/public/rooms/1/variables/global/round", read: _readKey)).Body.GetProperty("value").GetString());
        Assert.Equal("4", (await Call("PATCH", "/api/public/rooms/1/variables/global/round", "{\"value\":\"4\"}", write: _writeKey)).Body.GetProperty("value").GetString());
        await AssertError("GET", "/api/public/rooms/1/variables/global/score", null, 404, WiredWebApiErrors.NotFound, read: _readKey);
        await AssertError("PATCH", "/api/public/rooms/1/variables/global/round", "{}", 400, WiredWebApiErrors.InvalidValue, write: _writeKey);

        var profile = (await Call("GET", "/api/public/rooms/1/variables_profile/global", read: _readKey)).Body;
        Assert.Equal("4", profile.GetProperty("variables").GetProperty("round").GetProperty("value").GetString());

        var patched = await Call("PATCH", "/api/public/rooms/1/variables_profile/global", "{\"variables\":{\"round\":\"6\"}}", write: _writeKey);
        Assert.Equal("6", patched.Body.GetProperty("variables").GetProperty("round").GetProperty("value").GetString());
        await AssertError("PATCH", "/api/public/rooms/1/variables_profile/global", "{\"variables\":{\"round\":null}}", 400, WiredWebApiErrors.RequestFailed, write: _writeKey);
        await AssertError("PATCH", "/api/public/rooms/1/variables_profile/global", "{\"variables\":{\"nope\":\"1\"}}", 404, WiredWebApiErrors.NotFound, write: _writeKey);
    }

    [Fact]
    public async Task ProfilesNameTheirTargetAndHoldItsVariables()
    {
        var byName = (await Call("GET", "/api/public/rooms/1/variables_profile/user/users?name=bob", read: _readKey)).Body;
        Assert.Equal(45, byName.GetProperty("user").GetProperty("id").GetInt64());
        Assert.Equal("bob", byName.GetProperty("user").GetProperty("name").GetString());
        Assert.Equal("3", byName.GetProperty("variables").GetProperty("score").GetProperty("value").GetString());

        await AssertError("GET", "/api/public/rooms/1/variables_profile/user/users?unique_id=hhus-1", null, 404, WiredWebApiErrors.EntityNotFound, read: _readKey);
        await AssertError("GET", "/api/public/rooms/1/variables_profile/user/users", null, 400, WiredWebApiErrors.InvalidTarget, read: _readKey);
        await AssertError("GET", "/api/public/rooms/1/variables_profile/user/users?name=nobody", null, 404, WiredWebApiErrors.EntityNotFound, read: _readKey);

        var pet = (await Call("GET", "/api/public/rooms/1/variables_profile/user/pets/12", read: _readKey)).Body;
        Assert.Equal("Rex", pet.GetProperty("pet").GetProperty("name").GetString());
        Assert.Empty(pet.GetProperty("variables").EnumerateObject());

        var wall = (await Call("GET", "/api/public/rooms/1/variables_profile/furni/wall-items/91", read: _readKey)).Body;
        Assert.Equal(91, wall.GetProperty("wall_item").GetProperty("id").GetInt64());
        await AssertError("GET", "/api/public/rooms/1/variables_profile/furni/furni-bc/22", null, 404, WiredWebApiErrors.EntityNotFound, read: _readKey);
        await AssertError("GET", "/api/public/rooms/1/variables_profile/furni/users/22", null, 400, WiredWebApiErrors.InvalidTarget, read: _readKey);
        await AssertError("GET", "/api/public/rooms/1/variables_profile/user/bots/1", null, 404, WiredWebApiErrors.EntityNotFound, read: _readKey);
    }

    [Fact]
    public async Task ProfilePatchSetsAndRemovesAndDeleteEmptiesTheProfile()
    {
        var patched = await Call("PATCH", "/api/public/rooms/1/variables_profile/user/users/44", "{\"variables\":{\"score\":\"12\",\"vip\":\"0\"}}", write: _writeKey);
        Assert.Equal(200, patched.Status);
        Assert.Equal("12", patched.Body.GetProperty("variables").GetProperty("score").GetProperty("value").GetString());
        Assert.True(patched.Body.GetProperty("variables").TryGetProperty("vip", out _));

        var removed = await Call("PATCH", "/api/public/rooms/1/variables_profile/user/users/44", "{\"variables\":{\"vip\":null}}", write: _writeKey);
        Assert.False(removed.Body.GetProperty("variables").TryGetProperty("vip", out _));
        await AssertError("PATCH", "/api/public/rooms/1/variables_profile/user/users/44", "{\"variables\":{\"vip\":null}}", 404, WiredWebApiErrors.NotFound, write: _writeKey);
        await AssertError("PATCH", "/api/public/rooms/1/variables_profile/furni/furni/22", "{\"variables\":{\"charge\":\"x\"}}", 400, WiredWebApiErrors.RequestFailed, write: _writeKey);
        Assert.Equal("1", (await Call("PATCH", "/api/public/rooms/1/variables_profile/furni/furni/22", "{\"variables\":{\"charge\":\"1\"}}", write: _writeKey))
            .Body.GetProperty("variables").GetProperty("charge").GetProperty("value").GetString());

        Assert.Equal(204, (await Call("DELETE", "/api/public/rooms/1/variables_profile/user/users/45", write: _writeKey)).Status);
        Assert.Null(_room.Get(Score, 45));
        await AssertError("DELETE", "/api/public/rooms/1/variables_profile/user/users/45", null, 404, WiredWebApiErrors.NotFound, write: _writeKey);
        await AssertError("DELETE", "/api/public/rooms/1/variables_profile/user/users/99", null, 404, WiredWebApiErrors.EntityNotFound, write: _writeKey);
    }

    [Fact]
    public async Task AnUnloadedRoomAMissingAddonAndTooManyRequestsHaveTheirOwnErrors()
    {
        _store.Keys[OtherRoomId] = [new(8, DatabaseWiredWebApiStore.Hash(_readKey), null, false)];
        await AssertError("GET", "/api/public/rooms/2/variables", null, 404, WiredWebApiErrors.RoomNotFound, read: _readKey);
        await AssertError("PUT", "/api/public/rooms/2/variables/user/score/users/44", "{\"value\":\"1\"}", 403, WiredWebApiErrors.KeyMissing, write: _writeKey);

        _room.Addon = 0;
        await AssertError("GET", "/api/public/rooms/1/variables", null, 403, WiredWebApiErrors.ApiDisabled, read: _readKey);
        _room.Addon = AddonId;

        for (var i = 1; i < WiredWebApiRateLimit.Requests; i++) {
            await Call("GET", "/api/public/rooms/1/variables", read: _readKey);
        }

        await AssertError("GET", "/api/public/rooms/1/variables", null, 429, WiredWebApiErrors.TooManyRequests, read: _readKey);
        // Each key has its own window.
        Assert.Equal(200, (await Call("PATCH", "/api/public/rooms/1/variables/global/round", "{\"value\":\"1\"}", write: _writeKey)).Status);
    }

    [Fact]
    public void RemovingOneValueReachesAHolderOutsideTheFrameAndIsAnExternalChange()
    {
        var module = new WiredVariableModule(RoomId, new OneVariable(), new MemoryWiredVariableStore(), TimeProvider.System);
        var holder = new WiredVariableHolder(WiredVariableTarget.User, 44, 3);
        var reference = new WiredVariableReference(WiredVariableTarget.User, "custom:10");
        Assert.True(module.Mutate(reference, holder, WiredVariableMutation.Give, 5, new(RoomId, [holder])));
        module.DrainChanges();

        Assert.Equal(5, module.RemoveValue(10, WiredVariableTarget.User, 44, 3, 1)?.Value);
        Assert.Null(module.RemoveValue(10, WiredVariableTarget.User, 44, 3, 1));
        Assert.Null(module.RemoveValue(10, WiredVariableTarget.Global, 0, 0, 1));
        var change = Assert.Single(module.DrainChanges());
        Assert.Equal((WiredVariableChangeKind.Removed, 1, 3), (change.Kind, change.Origin, change.EntityId));
        Assert.Empty(module.GetStoredHolders(10));
    }

    [Fact]
    public void TheSpecCheckRejectsWhatTheSpecDoesNotAllow()
    {
        var schemas = Spec.GetProperty("components").GetProperty("schemas");

        Assert.False(Matches(schemas.GetProperty("WiredVariable"), JsonDocument.Parse("{\"value\":7}").RootElement, out _));
        Assert.False(Matches(schemas.GetProperty("WiredAnyUserProfile"), JsonDocument.Parse("{\"variables\":{}}").RootElement, out _));
        Assert.False(Matches(schemas.GetProperty("WiredBatchResults"), JsonDocument.Parse("{\"results\":[{\"status\":201}]}").RootElement, out _));
        Assert.True(Matches(schemas.GetProperty("WiredBatchResults"), Spec.GetProperty("components").GetProperty("schemas").GetProperty("WiredBatchResults").GetProperty("example"), out _));
    }

    [Fact]
    public void TheLiveRoomReadsWritesAndRemovesThroughTheVariableModule()
    {
        var world = new WiredChestProtocolTests.World();
        var directory = new LiveDirectory();
        var variables = world.Room.GetWired().Variables;
        var module = new WiredVariableModule(42, directory, new MemoryWiredVariableStore(), world.Clock, new RoomWiredBuiltinVariables(world.Room));
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(variables, module);
        Assert.True(WiredBoxRegistry.TryGet("wf_var_user", out var descriptor));
        var item = new Item { Id = 10, RoomId = 42, OwnerId = 1, Definition = new() { Id = 10, InteractionName = "wf_var_user" } };
        ((ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling).GetField("_floorItems", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(world.Room.GetRoomItemHandler())!)[10] = item;
        var box = new WiredVariableDefinitionBox(world.Room, item, descriptor);
        WiredNativeTestSupport.InstallLegacyVariableDraft(box, new() { IntParams = [1, 10], Text = "score" });
        Assert.True(WiredVariableDefinitions.TryDecode("wf_var_user", 10, 42, 1, box.Configuration, out var definition, out _));
        directory.Rows[10] = definition!;
        variables.ConfigurationLoaded(box);
        var room = new WiredWebApiRoom(world.Room);

        var score = Assert.Single(room.Variables());
        Assert.Equal(("score", WiredVariableTarget.User), (score.Name, score.Target));
        Assert.False(room.HasAddon(10));
        var user = room.Find(WiredWebApiTargetKind.Users, 1)!;
        Assert.True(user.Present);
        Assert.Null(room.Find(WiredWebApiTargetKind.Users, 2));
        Assert.Null(room.Find(WiredWebApiTargetKind.FurniBc, 10));

        Assert.Equal(WiredWebApiWriteOutcome.NoValue, room.Write(score, user, WiredWebApiWriteMode.Patch, 4));
        Assert.Equal(WiredWebApiWriteOutcome.Written, room.Write(score, user, WiredWebApiWriteMode.Put, 4));
        Assert.Equal(WiredWebApiWriteOutcome.Written, room.Write(score, user, WiredWebApiWriteMode.Patch, 9_000_000_000));
        Assert.Equal(9_000_000_000, room.Values(score)[1].Value);
        Assert.Equal(1, room.FromStorage(WiredWebApiTargetKind.Users, 1)!.Id);
        Assert.Equal(WiredWebApiWriteOutcome.NotPresent, room.Write(score, new(WiredWebApiTargetKind.Users, 2, null, 2, false), WiredWebApiWriteMode.Put, 1));
        Assert.All(module.DrainChanges(), change => Assert.Equal(1, change.Origin));

        Assert.True(room.Remove(score, user));
        Assert.Empty(room.Values(score));
        Assert.False(room.Remove(score, user));
    }

    private sealed class LiveDirectory : IWiredVariableDirectory
    {
        public Dictionary<uint, WiredVariableDefinition> Rows { get; } = [];
        public WiredVariableDefinition? Find(uint itemId) => Rows.GetValueOrDefault(itemId);
        public uint? GetRoomOwner(uint roomId) => roomId == 42 ? 1u : null;
    }

    private async Task AssertError(string method, string path, string? body, int status, string error, string? read = null, string? write = null, string? bearer = null)
    {
        var (actual, json, _) = await Call(method, path, body, read, write, bearer);

        Assert.Equal((status, error), (actual, json.ValueKind == JsonValueKind.Object && json.TryGetProperty("error", out var code) ? code.GetString() : json.ToString()));
        Assert.Single(json.EnumerateObject());
    }

    private async Task AssertPage(string method, string path)
    {
        var response = await _http.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Contains($"<pre>Cannot {method} {path}</pre>", await response.Content.ReadAsStringAsync());
    }

    /// <summary>Sends a request and checks a JSON answer against the operation's documented responses in the official spec.</summary>
    private async Task<(int Status, JsonElement Body, string ContentType)> Call(string method, string path, string? body = null, string? read = null, string? write = null,
        string? bearer = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (body is not null) {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        if (read is not null) {
            request.Headers.Add("X-Wired-Read-Key", read);
        }

        if (write is not null) {
            request.Headers.Add("X-Wired-Write-Key", write);
        }

        if (bearer is not null) {
            request.Headers.Add("Authorization", "Bearer " + bearer);
        }

        var response = await _http.SendAsync(request);
        var status = (int)response.StatusCode;
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "";
        var text = await response.Content.ReadAsStringAsync();

        if (!contentType.StartsWith("application/json", StringComparison.Ordinal)) {
            Assert.Equal(204, status);

            return (status, default, contentType);
        }

        Assert.Equal("application/json; charset=utf-8", contentType);
        var json = JsonDocument.Parse(text).RootElement.Clone();
        var operation = Operation(method, new Uri(new Uri("http://x"), path).AbsolutePath);
        Assert.True(operation.GetProperty("responses").TryGetProperty(status.ToString(), out var documented), $"{status} is not documented for {method} {path}");

        if (documented.TryGetProperty("content", out var content)) {
            var media = content.GetProperty("application/json");
            Validate(media.GetProperty("schema"), json, "$");

            if (json.TryGetProperty("error", out var error) && status >= 400) {
                var known = Spec.GetProperty("components").GetProperty("examples").EnumerateObject().Select(x => x.Value.GetProperty("value").GetProperty("error").GetString());
                Assert.Contains(error.GetString(), known.Concat(ObservedErrors));
            }
        }

        return (status, json, contentType);
    }

    // The most specific path template of the spec that matches (literal segments win over parameters).
    private static JsonElement Operation(string method, string path)
    {
        var segments = path.Split('/');
        var best = Spec.GetProperty("paths").EnumerateObject()
            .Where(x => x.Value.TryGetProperty(method.ToLowerInvariant(), out _))
            .Select(x => (Path: x, Parts: x.Name.Split('/')))
            .Where(x => x.Parts.Length == segments.Length && x.Parts.Zip(segments).All(p => p.First.StartsWith('{') || p.First == p.Second))
            .OrderByDescending(x => x.Parts.Count(p => !p.StartsWith('{'))).First();

        return best.Path.Value.GetProperty(method.ToLowerInvariant());
    }

    // The part of OpenAPI 3 schemas the spec uses: $ref, allOf, oneOf, type, required, properties, additionalProperties, items,
    // pattern, enum and nullable.
    private static void Validate(JsonElement schema, JsonElement value, string at)
    {
        Assert.True(Matches(schema, value, out var failure), $"{at}: {failure} in {value}");
    }

    private static bool Matches(JsonElement schema, JsonElement value, out string failure)
    {
        failure = "";

        if (schema.TryGetProperty("$ref", out var reference)) {
            return Matches(Spec.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1]), value, out failure);
        }

        if (value.ValueKind == JsonValueKind.Null) {
            return schema.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean() || Fail("null", out failure);
        }

        if (schema.TryGetProperty("allOf", out var all)) {
            foreach (var part in all.EnumerateArray()) {
                if (!Matches(part, value, out failure)) {
                    return false;
                }
            }
        }

        if (schema.TryGetProperty("oneOf", out var one) && one.EnumerateArray().Count(part => Matches(part, value, out _)) != 1) {
            return Fail("not exactly one of oneOf", out failure);
        }

        if (schema.TryGetProperty("enum", out var values) && !values.EnumerateArray().Any(x => x.ToString() == value.ToString())) {
            return Fail("not in enum", out failure);
        }

        if (!schema.TryGetProperty("type", out var type)) {
            return true;
        }

        switch (type.GetString()) {
            case "string":
                return value.ValueKind == JsonValueKind.String
                    && (!schema.TryGetProperty("pattern", out var pattern) || Regex.IsMatch(value.GetString()!, pattern.GetString()!))
                    && (!schema.TryGetProperty("format", out var format) || format.GetString() != "date-time" || DateTimeOffset.TryParse(value.GetString(), out _))
                    || Fail("not a matching string", out failure);
            case "integer":
                return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _) || Fail("not an integer", out failure);
            case "array":
                if (value.ValueKind != JsonValueKind.Array) {
                    return Fail("not an array", out failure);
                }

                foreach (var item in value.EnumerateArray()) {
                    if (schema.TryGetProperty("items", out var items) && !Matches(items, item, out failure)) {
                        return false;
                    }
                }

                return true;
            case "object":
                if (value.ValueKind != JsonValueKind.Object) {
                    return Fail("not an object", out failure);
                }

                if (schema.TryGetProperty("required", out var required)) {
                    foreach (var name in required.EnumerateArray()) {
                        if (!value.TryGetProperty(name.GetString()!, out _)) {
                            return Fail($"no {name}", out failure);
                        }
                    }
                }

                foreach (var property in value.EnumerateObject()) {
                    if (schema.TryGetProperty("properties", out var properties) && properties.TryGetProperty(property.Name, out var known)) {
                        if (!Matches(known, property.Value, out failure)) {
                            return false;
                        }
                    }
                    else if (schema.TryGetProperty("additionalProperties", out var additional) && !Matches(additional, property.Value, out failure)) {
                        return false;
                    }
                }

                return true;
            default:
                return true;
        }
    }

    private static bool Fail(string reason, out string failure)
    {
        failure = reason;

        return false;
    }

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(x => x.GetString()!).ToArray();

    private sealed class FakeStore : IWiredWebApiStore
    {
        public Dictionary<uint, IReadOnlyList<WiredWebApiKeys>> Keys { get; } = [];
        public IReadOnlyList<WiredWebApiKeys> RoomKeys(uint roomId) => Keys.GetValueOrDefault(roomId) ?? [];
        public void Issue(uint itemId, uint roomId, bool read, string key) => throw new NotSupportedException();
        public (string? ReadHash, string? WriteHash) Hashes(uint itemId) => throw new NotSupportedException();
        public void Saved(uint itemId, uint roomId, string readKey, string writeKey, bool bulkDelete) => throw new NotSupportedException();
    }

    private sealed class FakeDirectory : IWiredWebApiDirectory
    {
        public HashSet<uint> Rooms { get; } = [];
        public Dictionary<uint, IWiredWebApiRoom> Loaded { get; } = [];
        public Dictionary<long, string> Users { get; } = [];
        public bool RoomExists(uint roomId) => Rooms.Contains(roomId);
        public IWiredWebApiRoom? LoadedRoom(uint roomId) => Loaded.GetValueOrDefault(roomId);
        public (long Id, string Name)? UserByName(string name) => Users.FirstOrDefault(x => x.Value == name) is { Value: not null } user ? (user.Key, user.Value) : null;
        public string? UserName(long id) => Users.GetValueOrDefault(id);
    }

    /// <summary>A room with users in it (by id), pets (id to name and holder id), floor and wall items, and stored values.</summary>
    private sealed class FakeRoom : IWiredWebApiRoom
    {
        private static readonly DateTimeOffset Created = DateTimeOffset.FromUnixTimeSeconds(1700172800);
        private readonly Dictionary<(uint, long), WiredVariableValue> _values = [];

        public uint Addon { get; set; } = AddonId;
        public List<WiredWebApiVariable> Variables_ { get; } = [];
        public Dictionary<long, string> Users { get; } = [];
        public Dictionary<long, (string Name, long Storage)> Pets { get; } = [];
        public HashSet<long> Floor { get; } = [];
        public HashSet<long> Wall { get; } = [];

        public void Set(WiredWebApiVariable variable, long storage, long value) => _values[(variable.DefinitionId, storage)] = new(value, Created, Created);
        public long? Get(WiredWebApiVariable variable, long storage) => _values.GetValueOrDefault((variable.DefinitionId, storage))?.Value;

        public bool HasAddon(uint itemId) => itemId == Addon;
        public IReadOnlyList<WiredWebApiVariable> Variables() => Variables_;

        public WiredWebApiEntity? Find(WiredWebApiTargetKind kind, long id) => kind switch
        {
            WiredWebApiTargetKind.Users => Users.TryGetValue(id, out var name) ? new(kind, id, name, id, true) : null,
            WiredWebApiTargetKind.Pets => Pets.TryGetValue(id, out var pet) ? new(kind, id, pet.Name, pet.Storage, true) : null,
            WiredWebApiTargetKind.Furni => Floor.Contains(id) ? new(kind, id, null, id, true) : null,
            WiredWebApiTargetKind.WallItems => Wall.Contains(id) ? new(kind, id, null, id, true) : null,
            _ => null
        };

        public WiredWebApiEntity? FromStorage(WiredWebApiTargetKind kind, long storageId) => kind switch
        {
            WiredWebApiTargetKind.Users => storageId > 0 ? Find(kind, storageId) ?? new(kind, storageId, null, storageId, false) : null,
            WiredWebApiTargetKind.Pets => Pets.Where(x => x.Value.Storage == storageId).Select(x => Find(kind, x.Key)).FirstOrDefault(),
            WiredWebApiTargetKind.Bots => null,
            _ => Find(kind, storageId)
        };

        public IReadOnlyDictionary<long, WiredVariableValue> Values(WiredWebApiVariable variable) =>
            _values.Where(x => x.Key.Item1 == variable.DefinitionId).ToDictionary(x => x.Key.Item2, x => x.Value);

        public WiredWebApiWriteOutcome Write(WiredWebApiVariable variable, WiredWebApiEntity? entity, WiredWebApiWriteMode mode, long value)
        {
            if (variable.ReadOnly) {
                return WiredWebApiWriteOutcome.NotAllowed;
            }

            if (variable.Target != WiredVariableTarget.Global && entity is not { Present: true }) {
                return WiredWebApiWriteOutcome.NotPresent;
            }

            var key = (variable.DefinitionId, entity?.StorageId ?? 0);
            var held = _values.ContainsKey(key);

            if (mode == WiredWebApiWriteMode.Patch && !held) {
                return WiredWebApiWriteOutcome.NoValue;
            }

            _values[key] = new(variable.HasValue ? value : 1, held ? _values[key].CreatedAt : DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

            return WiredWebApiWriteOutcome.Written;
        }

        public bool Remove(WiredWebApiVariable variable, WiredWebApiEntity entity) => _values.Remove((variable.DefinitionId, entity.StorageId));

        public int Clear(WiredWebApiVariable variable)
        {
            var keys = _values.Keys.Where(x => x.Item1 == variable.DefinitionId).ToArray();

            foreach (var key in keys) {
                _values.Remove(key);
            }

            return keys.Length;
        }
    }

    private sealed class OneVariable : IWiredVariableDirectory
    {
        public WiredVariableDefinition? Find(uint itemId) => itemId == 10
            ? new(10, RoomId, 5, "score", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true) : null;
        public uint? GetRoomOwner(uint roomId) => 5;
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
