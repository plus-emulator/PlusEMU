using System.Collections.Immutable;
using System.Globalization;

namespace Plus.HabboHotel.Items.DataFormat;

/// <summary>
/// Habbo's recolorable Builders Club furni: an ordinary multi-state furni whose state goes out as MapData together with its
/// colour. A fresh one is {"r":"255","b":"255","g":"255","state":"0"}.
/// The client reads r, g and b from that map and falls back to 255 per channel; legacy data paints it white. The state stays the legacy value, so use, Wired and snapshots treat it like any multi-state furni.
/// </summary>
public sealed class RecolorableDataFormat : LegacyDataFormat
{
    public const int White = 0xFFFFFF;

    private int _rgb = White;

    public RecolorableDataFormat()
    {
        StoreWithoutNotification("0");
    }

    public int Rgb => _rgb;
    public int Red => (_rgb >> 16) & 0xFF;
    public int Green => (_rgb >> 8) & 0xFF;
    public int Blue => _rgb & 0xFF;

    public override FurniDataStructure StructureType => FurniDataStructure.Map;

    /// <summary>Sets the colour; false for a value outside 0..0xFFFFFF or the colour it already has.</summary>
    public bool SetColor(int rgb)
    {
        if (rgb is < 0 or > White || rgb == _rgb) {
            return false;
        }

        _rgb = rgb;
        RaiseDataUpdated();

        return true;
    }

    /// <summary>The wire map in Habbo's key order, with the given state.</summary>
    public ImmutableArray<KeyValuePair<string, string>> WireMap(string state) =>
    [
        new("r", Red.ToString(CultureInfo.InvariantCulture)),
        new("b", Blue.ToString(CultureInfo.InvariantCulture)),
        new("g", Green.ToString(CultureInfo.InvariantCulture)),
        new("state", state)
    ];

    /// <summary>Stored as the same map: "state\t0\nr\t255\ng\t255\nb\t255".</summary>
    public override string Serialize() => string.Create(CultureInfo.InvariantCulture, $"state\t{Data}\nr\t{Red}\ng\t{Green}\nb\t{Blue}");

    /// <summary>Reads the stored map, a bare legacy state, or the catalogue's "r,g,b"; anything unreadable is white.</summary>
    public override void Store(string data)
    {
        data ??= "";
        var state = "0";
        int red = 255, green = 255, blue = 255;

        if (data.Contains('\t')) {
            foreach (var line in data.Split('\n')) {
                var pair = line.Split('\t');

                if (pair.Length != 2) {
                    continue;
                }

                switch (pair[0]) {
                    case "state":
                        state = int.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out _) ? pair[1] : "0";
                        break;
                    case "r":
                        red = Channel(pair[1]);
                        break;
                    case "g":
                        green = Channel(pair[1]);
                        break;
                    case "b":
                        blue = Channel(pair[1]);
                        break;
                }
            }
        }
        else if (TryParseCatalogueColor(data, out var rgb)) {
            red = (rgb >> 16) & 0xFF;
            green = (rgb >> 8) & 0xFF;
            blue = rgb & 0xFF;
        }
        else if (int.TryParse(data, NumberStyles.None, CultureInfo.InvariantCulture, out _)) {
            state = data;
        }

        _rgb = (red << 16) | (green << 8) | blue;
        Data = state;
    }

    /// <summary>The client sends the chosen colour as "r,g,b".</summary>
    public static bool TryParseCatalogueColor(string? value, out int rgb)
    {
        rgb = White;
        var parts = value?.Split(',');

        if (parts is not { Length: 3 }) {
            return false;
        }

        var channels = new int[3];

        for (var index = 0; index < 3; index++) {
            if (!int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out channels[index]) || channels[index] > 255) {
                return false;
            }
        }

        rgb = (channels[0] << 16) | (channels[1] << 8) | channels[2];

        return true;
    }

    /// <summary>The catalogue's "r,g,b" for a colour.</summary>
    public static string FormatCatalogueColor(int rgb) =>
        string.Create(CultureInfo.InvariantCulture, $"{(rgb >> 16) & 0xFF},{(rgb >> 8) & 0xFF},{rgb & 0xFF}");

    // The client reads one to three digits up to 255, otherwise 255.
    private static int Channel(string value) =>
        value.Length is >= 1 and <= 3 && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var channel) && channel <= 255 ? channel : 255;
}
