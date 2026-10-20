namespace Plus.HabboHotel.Items.Wired.Configuration;

public sealed record WiredFurniSnapshot(uint ItemId, uint DefinitionId, int X, int Y, double Z, int Rotation, string State)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public WiredWallSnapshot? Wall { get; init; }

    // A snapshot saved while a switch still held "" means the "0" that furni now loads with.
    public string StateFor(ItemDefinition definition) => FurniExtraData.LegacyState(definition, State);
}
