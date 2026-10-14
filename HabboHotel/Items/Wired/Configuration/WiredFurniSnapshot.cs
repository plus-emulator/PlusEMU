namespace Plus.HabboHotel.Items.Wired.Configuration;

public sealed record WiredFurniSnapshot(uint ItemId, uint DefinitionId, int X, int Y, double Z, int Rotation, string State)
{
    // Arcturus snapshot effects use a single space to mean "do not restore state".
    // An explicit flag preserves that behavior without changing literal-space snapshots
    // created by Plus. Position and rotation still restore normally.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool SkipStateRestore { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public WiredWallSnapshot? Wall { get; init; }
}
