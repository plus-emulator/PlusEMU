using System.Collections.Immutable;
using Dapper;
using Plus.Database;

namespace Plus.HabboHotel.Items.Wired.Labels;

/// <summary>
/// A WIRED Label's bubble: its text, colour, who sees it
/// (0 everyone, 1 users with wired inspection rights) and whether it stays open. A label nobody wrote yet carries the client's
/// update-message defaults: no text, blue, inspectors only, closed.
/// </summary>
public sealed record WiredLabel(uint FurniId, string Text, int BubbleColor, int Visibility, bool AlwaysOpen)
{
    public const string Interaction = "wired_label";
    public const int DefaultColor = 4278912;
    // The label widget sets no character limit (the layout XML is not read); the server keeps a bubble's worth of text.
    public const int MaxTextLength = 1000;

    // Blue, Teal, Green, Gold, Orange, Red, Purple, Charcoal.
    public static readonly ImmutableArray<int> Colors = [4278912, 2651759, 4157508, 8416562, 8409402, 8404554, 6834048, 5592405];

    public static WiredLabel Default(uint furniId) => new(furniId, "", DefaultColor, 1, false);

    public static bool IsLabel(Item item) => string.Equals(item.Definition.InteractionName, Interaction, StringComparison.OrdinalIgnoreCase);

    /// <summary>The settings view sends always-open only with visibility Everyone; anything else outside its choices is refused.</summary>
    public static bool ValidSettings(int color, int visibility, bool alwaysOpen) => Colors.Contains(color) && visibility is 0 or 1
        && (!alwaysOpen || visibility == 0);
}

public interface IWiredLabelStore
{
    IReadOnlyDictionary<uint, WiredLabel> Load(IReadOnlyCollection<uint> furniIds);
    void Save(WiredLabel label);
}

public sealed class DatabaseWiredLabelStore(IDatabase database) : IWiredLabelStore
{
    public IReadOnlyDictionary<uint, WiredLabel> Load(IReadOnlyCollection<uint> furniIds)
    {
        if (furniIds.Count == 0) {
            return new Dictionary<uint, WiredLabel>();
        }

        using var connection = database.Connection();

        return connection.Query<Row>("SELECT item_id AS ItemId, text AS Text, bubble_color AS BubbleColor, visibility AS Visibility, always_open AS AlwaysOpen "
            + "FROM wired_labels WHERE item_id IN @Ids", new { Ids = furniIds.ToArray() })
            .ToDictionary(row => (uint)row.ItemId, row => new WiredLabel((uint)row.ItemId, row.Text, row.BubbleColor, row.Visibility, row.AlwaysOpen));
    }

    public void Save(WiredLabel label)
    {
        using var connection = database.Connection();
        connection.Execute("INSERT INTO wired_labels (item_id, text, bubble_color, visibility, always_open) VALUES (@FurniId, @Text, @BubbleColor, @Visibility, @AlwaysOpen) "
            + "ON DUPLICATE KEY UPDATE text = @Text, bubble_color = @BubbleColor, visibility = @Visibility, always_open = @AlwaysOpen", label);
    }

    private sealed class Row
    {
        public long ItemId { get; set; }
        public string Text { get; set; } = "";
        public int BubbleColor { get; set; }
        public int Visibility { get; set; }
        public bool AlwaysOpen { get; set; }
    }
}
