using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Options;
using Plus.HabboHotel.Catalog;

namespace Plus.Communication.Http.Air;

/// <summary>One gamedata file as the AIR client fetches it: <c>{url}/{hash}</c>.</summary>
public sealed record FlashGamedataFile(string Name, string Route, byte[] Content, string ContentType, string Hash);

public sealed class FlashGamedataSet(IReadOnlyList<FlashGamedataFile> files, string urlPrefix)
{
    public IReadOnlyList<FlashGamedataFile> Files { get; } = files;

    public FlashGamedataFile? ByRoute(string route) => Files.FirstOrDefault(file => file.Route == route);

    /// <summary>
    /// <c>{"hashes":[{name,url,hash}]}</c>, the document <c>GameDataResources.parse</c> reads. The client needs
    /// external_texts, external_variables, furnidata and productdata (AIR WIN63-202610151745-318700621,
    /// com/sulake/core/localization/GameDataResources.as:35-79) and loads <c>url + "/" + hash</c>
    /// (CoreLocalizationManager.as:166, HabboConfigurationManager.as:320).
    /// </summary>
    public byte[] HashesJson() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        hashes = Files.Select(file => new { name = file.Name, url = $"{urlPrefix}/gamedata/{file.Route}", hash = file.Hash })
    });
}

public interface IFlashGamedata
{
    FlashGamedataSet Current(string urlPrefix);
}

/// <summary>
/// The AIR client's Flash gamedata, built from the hotel's own data: furnidata from the catalog's generated
/// furnidata, texts and variables from configured files, productdata from a configured file or the furnidata.
/// Rebuilt when the catalog or one of the files changes.
/// </summary>
public sealed class FlashGamedata(IOptions<AirClientConfiguration> options, ICatalogFurnidata furnidata) : IFlashGamedata
{
    public const string TextsRoute = "external_flash_texts";
    public const string VariablesRoute = "external_variables";
    public const string FurnidataRoute = "furnidata_xml";
    public const string ProductdataRoute = "productdata";

    private const string TextPlain = "text/plain; charset=utf-8";
    private const string Xml = "application/xml; charset=utf-8";

    private readonly AirClientConfiguration _configuration = options.Value;
    private readonly object _sync = new();
    private (string Key, FlashGamedataSet Set)? _cached;

    public FlashGamedataSet Current(string urlPrefix)
    {
        var furni = furnidata.Current();
        var key = string.Join('|', new[] { urlPrefix, furni?.ETag ?? "" }
            .Concat(_configuration.ExternalVariablesFiles.Concat(_configuration.ExternalTextsFiles).Append(_configuration.ProductDataFile).Select(Stamp)));

        lock (_sync) {
            if (_cached is { } cached && cached.Key == key) {
                return cached.Set;
            }

            var set = Build(urlPrefix, furni);
            _cached = (key, set);

            return set;
        }
    }

    private FlashGamedataSet Build(string urlPrefix, CatalogFurnidataFile? furni)
    {
        var furnidataXml = File(FurnidataRoute, "furnidata", FurnidataXml(furni), Xml);
        var productdata = ProductData(furni);
        var texts = File(TextsRoute, "external_texts", KeyValueText(Merge(_configuration.ExternalTextsFiles), "# No external_flash_texts configured (AirClient:ExternalTextsFiles)."), TextPlain);

        // SessionDataManager loads furnidata.load.url and productdata.load.url from these variables
        // (AIR WIN63-202610151745-318700621, com/sulake/habbo/session/SessionDataManager.as:336-374).
        var variables = Merge(_configuration.ExternalVariablesFiles);
        variables["furnidata.load.url"] = $"{urlPrefix}/gamedata/{FurnidataRoute}/{furnidataXml.Hash}";
        variables["productdata.load.url"] = $"{urlPrefix}/gamedata/{ProductdataRoute}/{productdata.Hash}";

        foreach (var (name, value) in _configuration.Variables) {
            variables[name] = value;
        }

        var externalVariables = File(VariablesRoute, "external_variables", KeyValueText(variables, ""), TextPlain);

        return new([texts, externalVariables, furnidataXml, productdata], urlPrefix);
    }

    private static FlashGamedataFile File(string route, string name, byte[] content, string contentType) =>
        new(name, route, content, contentType, Convert.ToHexStringLower(SHA1.HashData(content)));

    private FlashGamedataFile ProductData(CatalogFurnidataFile? furni)
    {
        if (string.IsNullOrWhiteSpace(_configuration.ProductDataFile)) {
            return File(ProductdataRoute, "productdata", ProductDataXml(FurnidataProducts(furni)), Xml);
        }

        var path = Resolve(_configuration.ProductDataFile);

        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) {
            return File(ProductdataRoute, "productdata", ProductDataXml(JsonProducts(System.IO.File.ReadAllBytes(path))), Xml);
        }

        var content = System.IO.File.ReadAllBytes(path);
        var text = Encoding.UTF8.GetString(content).TrimStart();

        return File(ProductdataRoute, "productdata", content, text.StartsWith('<') ? Xml : TextPlain);
    }

    /// <summary>
    /// The XML furnidata <c>FurnitureDataParser.parseXmlFormat</c> reads: roomitemtypes and wallitemtypes with
    /// furnitype id and classname attributes and one element per field, booleans as 1 or 0, part colours as
    /// partcolors/color (AIR WIN63-202610151745-318700621, com/sulake/habbo/session/furniture/FurnitureDataParser.as:193-265).
    /// </summary>
    public static byte[] FurnidataXml(CatalogFurnidataFile? furni)
    {
        using var document = furni == null ? null : JsonDocument.Parse(furni.Content);

        return WriteXml(writer =>
        {
            writer.WriteStartElement("furnidata");

            foreach (var section in new[] { "roomitemtypes", "wallitemtypes" }) {
                writer.WriteStartElement(section);

                if (document != null && document.RootElement.TryGetProperty(section, out var types) &&
                    types.TryGetProperty("furnitype", out var entries) && entries.ValueKind == JsonValueKind.Array) {
                    foreach (var entry in entries.EnumerateArray()) {
                        WriteFurnitype(writer, entry);
                    }
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        });
    }

    private static void WriteFurnitype(XmlWriter writer, JsonElement entry)
    {
        writer.WriteStartElement("furnitype");
        writer.WriteAttributeString("id", Scalar(entry.GetProperty("id")));
        writer.WriteAttributeString("classname", Scalar(entry.GetProperty("classname")));

        foreach (var property in entry.EnumerateObject()) {
            if (property.Name is "id" or "classname") {
                continue;
            }

            writer.WriteStartElement(property.Name);

            if (property.Value.ValueKind == JsonValueKind.Object) {
                foreach (var list in property.Value.EnumerateObject().Where(list => list.Value.ValueKind == JsonValueKind.Array)) {
                    foreach (var item in list.Value.EnumerateArray()) {
                        writer.WriteElementString(list.Name, Scalar(item));
                    }
                }
            }
            else {
                writer.WriteString(Scalar(property.Value));
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static string Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => "1",
        JsonValueKind.False => "0",
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        JsonValueKind.String => value.GetString()!,
        _ => value.GetRawText()
    };

    /// <summary>The XML productdata <c>ProductDataParser.parseXmlFormat</c> reads: product elements with a code
    /// attribute and a name (AIR WIN63-202610151745-318700621, com/sulake/habbo/session/product/ProductDataParser.as:154-178).</summary>
    public static byte[] ProductDataXml(IEnumerable<(string Code, string Name, string Description)> products) => WriteXml(writer =>
    {
        writer.WriteStartElement("productdata");

        foreach (var (code, name, description) in products) {
            writer.WriteStartElement("product");
            writer.WriteAttributeString("code", code);
            writer.WriteElementString("name", name);
            writer.WriteElementString("description", description);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    });

    private static IEnumerable<(string, string, string)> FurnidataProducts(CatalogFurnidataFile? furni)
    {
        if (furni == null) {
            yield break;
        }

        using var document = JsonDocument.Parse(furni.Content);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var section in new[] { "roomitemtypes", "wallitemtypes" }) {
            if (!document.RootElement.TryGetProperty(section, out var types) || !types.TryGetProperty("furnitype", out var entries)) {
                continue;
            }

            foreach (var entry in entries.EnumerateArray()) {
                var code = Text(entry, "classname");

                if (code.Length > 0 && seen.Add(code)) {
                    yield return (code, Text(entry, "name"), Text(entry, "description"));
                }
            }
        }
    }

    // ProductData.json: {"productdata":{"product":[{"code","name","description"}]}}.
    private static List<(string, string, string)> JsonProducts(byte[] json)
    {
        using var document = JsonDocument.Parse(json, JsonOptions);
        var products = new List<(string, string, string)>();

        if (document.RootElement.TryGetProperty("productdata", out var root) && root.TryGetProperty("product", out var list)) {
            foreach (var product in list.EnumerateArray()) {
                products.Add((Text(product, "code"), Text(product, "name"), Text(product, "description")));
            }
        }

        return products;
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";

    private static readonly JsonDocumentOptions JsonOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private Dictionary<string, string> Merge(IEnumerable<string> files)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in files.Where(file => !string.IsNullOrWhiteSpace(file))) {
            var bytes = System.IO.File.ReadAllBytes(Resolve(file));

            foreach (var (name, value) in file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase)
                         ? JsonEntries(bytes)
                         : KeyValueEntries(Encoding.UTF8.GetString(bytes))) {
                merged[name] = value;
            }
        }

        return merged;
    }

    /// <summary>key=value lines the way the client reads them: # starts a comment, the first = splits, both sides are
    /// trimmed, a literal \n in a value is a line break (CoreLocalizationManager.as:368-405).</summary>
    public static IEnumerable<(string, string)> KeyValueEntries(string text)
    {
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)) {
            var separator = line.IndexOf('=');

            if (line.StartsWith('#') || separator <= 0) {
                continue;
            }

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (name.Length > 0 && value.Length > 0) {
                yield return (name, value);
            }
        }
    }

    private static IEnumerable<(string, string)> JsonEntries(byte[] json)
    {
        using var document = JsonDocument.Parse(json, JsonOptions);

        foreach (var property in document.RootElement.EnumerateObject()) {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) {
                continue;
            }

            var value = Scalar(property.Value).Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\\n").Trim();

            if (value.Length > 0) {
                yield return (property.Name.Trim(), value);
            }
        }
    }

    private static byte[] KeyValueText(Dictionary<string, string> entries, string whenEmpty)
    {
        var builder = new StringBuilder();

        foreach (var (name, value) in entries) {
            builder.Append(name).Append('=').Append(value).Append('\n');
        }

        return Encoding.UTF8.GetBytes(builder.Length == 0 ? whenEmpty + "\n" : builder.ToString());
    }

    private static byte[] WriteXml(Action<XmlWriter> write)
    {
        using var stream = new MemoryStream();

        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false })) {
            writer.WriteStartDocument();
            write(writer);
            writer.WriteEndDocument();
        }

        return stream.ToArray();
    }

    private static string Resolve(string file) => Path.IsPathRooted(file) ? file : Path.Join(Directory.GetCurrentDirectory(), "Config", file);

    private static string Stamp(string file)
    {
        if (string.IsNullOrWhiteSpace(file)) {
            return "";
        }

        var info = new FileInfo(Resolve(file));

        return info.Exists ? $"{info.LastWriteTimeUtc.Ticks}:{info.Length}" : "missing";
    }
}
