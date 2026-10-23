using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Plus.Communication.Http;
using Plus.Communication.Http.Air;
using Plus.HabboHotel.Badges.Rarity;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

/// <summary>The Flash gamedata as the AIR client reads it.</summary>
public sealed class FlashGamedataTests : IAsyncLifetime
{
    private readonly string _directory = Directory.CreateTempSubdirectory("flash-gamedata").FullName;
    private HttpClient _http = new();
    private WebServer? _server;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _http.Dispose();

        if (_server != null) {
            await _server.Stop();
        }

        Directory.Delete(_directory, true);
    }

    private static CatalogFurnidataFile Furnidata() => CatalogFurnidata.Generate([
        new FurnidataEntry
        {
            Id = 13, Classname = "shelves_norja", Revision = 61856, Category = "shelf", XDim = 1, YDim = 1, PartColors = "#ffffff,#F7EBBC",
            Name = "Beige Bookcase", Description = "For nic & nacs", CanStandOn = false, CanSitOn = false, Tradeable = true
        },
        new FurnidataEntry { Id = 4001, Classname = "poster", IsWall = true, Revision = 1, Name = "Poster", Description = "A poster" }
    ], []);

    private async Task Start(AirClientConfiguration configuration)
    {
        var options = Options.Create(configuration);
        var furnidata = new FixedFurnidata(Furnidata());
        _server = new WebServer(Options.Create(new WebServerConfiguration { Port = 0 }), new NoRarity(), furnidata, null,
            new FlashGamedata(options, furnidata), options);
        await _server.Start();
        _http.Dispose();
        _http = new HttpClient { BaseAddress = new Uri(_server.Urls.Single()) };
    }

    private async Task<Dictionary<string, (string Url, string Hash)>> Hashes()
    {
        var response = await _http.GetAsync("/gamedata/hashes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("hashes").EnumerateArray().ToDictionary(
            entry => entry.GetProperty("name").GetString()!,
            entry => (entry.GetProperty("url").GetString()!, entry.GetProperty("hash").GetString()!));
    }

    private async Task<string> Fetch(string url, string hash)
    {
        var response = await _http.GetAsync(new Uri(url).PathAndQuery + "/" + hash);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task HashesListTheFourFilesTheClientRequiresUnderTheConfiguredPrefix()
    {
        await Start(new AirClientConfiguration { UrlPrefix = "https://hotel.example/" });

        var hashes = await Hashes();

        Assert.Equal(["external_texts", "external_variables", "furnidata", "productdata"], hashes.Keys.Order());
        Assert.Equal("https://hotel.example/gamedata/external_flash_texts", hashes["external_texts"].Url);
        Assert.Equal("https://hotel.example/gamedata/furnidata_xml", hashes["furnidata"].Url);
        Assert.All(hashes.Values, entry => Assert.Matches("^[0-9a-f]{40}$", entry.Hash));
    }

    [Fact]
    public async Task PrefixFallsBackToTheForwardedOrigin()
    {
        await Start(new AirClientConfiguration());
        using var request = new HttpRequestMessage(HttpMethod.Get, "/gamedata/hashes");
        request.Headers.Host = "hotel.example";
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var document = JsonDocument.Parse(await (await _http.SendAsync(request)).Content.ReadAsStringAsync());

        Assert.StartsWith("https://hotel.example/gamedata/", document.RootElement.GetProperty("hashes")[0].GetProperty("url").GetString());
    }

    [Fact]
    public async Task FurnidataIsTheXmlTheClientParses()
    {
        await Start(new AirClientConfiguration { UrlPrefix = "https://hotel.example" });
        var (url, hash) = (await Hashes())["furnidata"];

        var xml = XDocument.Parse(await Fetch(url, hash));

        var floor = Assert.Single(xml.Root!.Element("roomitemtypes")!.Elements("furnitype"));
        Assert.Equal("13", floor.Attribute("id")!.Value);
        Assert.Equal("shelves_norja", floor.Attribute("classname")!.Value);
        Assert.Equal("Beige Bookcase", floor.Element("name")!.Value);
        Assert.Equal(["#ffffff", "#F7EBBC"], floor.Element("partcolors")!.Elements("color").Select(color => color.Value));
        Assert.Equal("0", floor.Element("canstandon")!.Value);
        Assert.Equal("1", floor.Element("tradeable")!.Value);
        Assert.Equal("poster", Assert.Single(xml.Root.Element("wallitemtypes")!.Elements("furnitype")).Attribute("classname")!.Value);
    }

    [Fact]
    public async Task VariablesPointTheSessionAtTheHashedFurnidataAndProductdata()
    {
        File.WriteAllText(Path.Join(_directory, "vars.txt"), "# base\nflash.dynamic.download.url=https://cdn.example/dcr/\nfurnidata.load.url=https://elsewhere/\n");
        await Start(new AirClientConfiguration
        {
            UrlPrefix = "https://hotel.example",
            ExternalVariablesFiles = [Path.Join(_directory, "vars.txt")],
            Variables = new() { ["client.starting"] = "Welcome" }
        });
        var hashes = await Hashes();

        var variables = FlashGamedata.KeyValueEntries(await Fetch(hashes["external_variables"].Url, "1")).ToDictionary();

        Assert.Equal("https://cdn.example/dcr/", variables["flash.dynamic.download.url"]);
        Assert.Equal($"https://hotel.example/gamedata/furnidata_xml/{hashes["furnidata"].Hash}", variables["furnidata.load.url"]);
        Assert.Equal($"https://hotel.example/gamedata/productdata/{hashes["productdata"].Hash}", variables["productdata.load.url"]);
        Assert.Equal("Welcome", variables["client.starting"]);
    }

    [Fact]
    public async Task TextsMergeKeyValueAndJsonFilesWithEscapedLineBreaks()
    {
        File.WriteAllText(Path.Join(_directory, "base.txt"), "a=one\nb=two\n");
        File.WriteAllText(Path.Join(_directory, "texts.json"), "{\"b\":\"line1\\nline2\",\"c\":\"three\"}");
        await Start(new AirClientConfiguration
        {
            UrlPrefix = "https://hotel.example",
            ExternalTextsFiles = [Path.Join(_directory, "base.txt"), Path.Join(_directory, "texts.json")]
        });
        var (url, hash) = (await Hashes())["external_texts"];

        var texts = await Fetch(url, hash);

        Assert.Equal("a=one\nb=line1\\nline2\nc=three\n", texts);
    }

    [Fact]
    public async Task TextsAreNeverEmptySinceTheClientRejectsAnEmptyFile()
    {
        await Start(new AirClientConfiguration { UrlPrefix = "https://hotel.example" });
        var (url, hash) = (await Hashes())["external_texts"];

        Assert.NotEmpty(await Fetch(url, hash));
    }

    [Fact]
    public async Task ProductdataDefaultsToTheFurnidataNames()
    {
        await Start(new AirClientConfiguration { UrlPrefix = "https://hotel.example" });
        var (url, hash) = (await Hashes())["productdata"];

        var products = XDocument.Parse(await Fetch(url, hash)).Root!.Elements("product").ToDictionary(product => product.Attribute("code")!.Value, product => product.Element("name")!.Value);

        Assert.Equal("Beige Bookcase", products["shelves_norja"]);
        Assert.Equal("Poster", products["poster"]);
    }

    [Fact]
    public async Task ProductdataJsonIsConvertedToXml()
    {
        File.WriteAllText(Path.Join(_directory, "ProductData.json"), "{\"productdata\":{\"product\":[{\"code\":\"club_sofa\",\"name\":\"Club Sofa\",\"description\":\"Soft\"}]}}");
        await Start(new AirClientConfiguration { UrlPrefix = "https://hotel.example", ProductDataFile = Path.Join(_directory, "ProductData.json") });
        var (url, hash) = (await Hashes())["productdata"];

        var product = Assert.Single(XDocument.Parse(await Fetch(url, hash)).Root!.Elements("product"));

        Assert.Equal("club_sofa", product.Attribute("code")!.Value);
        Assert.Equal("Club Sofa", product.Element("name")!.Value);
    }

    [Fact]
    public async Task OnlyTheCurrentHashIsCachedForGood()
    {
        await Start(new AirClientConfiguration { UrlPrefix = "https://hotel.example" });
        var (url, hash) = (await Hashes())["external_variables"];

        var current = await _http.GetAsync(new Uri(url).PathAndQuery + "/" + hash);
        var initial = await _http.GetAsync(new Uri(url).PathAndQuery + "/1");

        Assert.True(current.Headers.CacheControl!.Public);
        Assert.True(initial.Headers.CacheControl!.NoCache);
        Assert.Equal(await current.Content.ReadAsStringAsync(), await initial.Content.ReadAsStringAsync());
    }

    private sealed class FixedFurnidata(CatalogFurnidataFile? file) : ICatalogFurnidata
    {
        public CatalogFurnidataFile? Current() => file;
    }

    private sealed class NoRarity : IBadgeRarityManager
    {
        public BadgeLeaderboardSnapshot Snapshot => BadgeLeaderboardSnapshot.Empty;
        public Task Refresh() => Task.CompletedTask;
        public Task<LeaderboardProfile?> GetProfile(int userId) => Task.FromResult<LeaderboardProfile?>(null);
    }
}
