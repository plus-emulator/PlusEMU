namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>A user variable a Variable Leaderboard can rank, with the add-ons stacked on its definition box.</summary>
public sealed record WiredLeaderboardChoice(WiredVariableDescription Description, WiredVariableLevelSystem? LevelSystem,
    IReadOnlyDictionary<int, string> TextConnector)
{
    public string VariableId => Description.CatalogId;
    public bool Persistent => Description.Definition.IsDurable;
}

public sealed partial class WiredRoomVariables
{
    /// <summary>The user variables defined by boxes on one tile, bottom of the stack first.</summary>
    public IReadOnlyList<WiredLeaderboardChoice> LeaderboardChoices(int x, int y)
    {
        WiredVariableDefinitionBox[] boxes;

        lock (_membershipGate) {
            boxes = [.. _definitions.Values.Where(box => IsAttached(box.Item) && box.Item.GetX == x && box.Item.GetY == y)
                .OrderBy(box => box.Item.GetZ).ThenBy(box => box.Item.Id)];
        }

        var described = Module.DescribeDefinitions(boxes.Select(box => box.Item.Id)).ToDictionary(description => description.Definition.ItemId);

        return [.. boxes.Select(box => described.GetValueOrDefault(box.Item.Id))
            .OfType<WiredVariableDescription>().Where(description => description.Definition.Target == WiredVariableTarget.User)
            .Select(description => new WiredLeaderboardChoice(description,
                MetadataOn(description.Definition.ItemId, "wf_xtra_var_lvlup_system")?.LevelSystem,
                MetadataOn(description.Definition.ItemId, "wf_xtra_var_text_connector")?.TextConnector ?? new Dictionary<int, string>()))];
    }
}
