namespace Plus.HabboHotel.Catalog;

// The catalogue a page belongs to. The client keeps one window per mode and routes every index and page by the mode
// it carries.
public static class CatalogModes
{
    public const string Normal = "NORMAL";
    public const string BuildersClub = "BUILDERS_CLUB";

    // Anything the client sends that is not the Builders Club catalogue reads as the normal one.
    public static string FromClient(string? mode) => mode == BuildersClub ? BuildersClub : Normal;
}
