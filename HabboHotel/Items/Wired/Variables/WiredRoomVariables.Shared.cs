
namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>
/// Share and Import. Observed on AIR WIN63-202610201032-107964540: a Share add-on publishes the text connector, level-up system and
/// Variable FX of the shared variables stacked with it when its Synchronize button is pressed, to the references that exist then. The
/// Import add-on stacked with a reference picks which of those categories it shows, and that works at once. A stack is the furniture on
/// one tile: a Share add-on or an Import add-on on another tile does not count.
/// </summary>
public sealed partial class WiredRoomVariables
{
    private const string ShareName = "wf_xtra_var_share";
    private const string ImportName = "wf_xtra_var_import";

    /// <summary>
    /// The Share add-on's Synchronize button: publishes the add-ons of the shared variables stacked with the add-on to every reference
    /// of those variables, and tells the rooms that hold them. False when the item is not an attached Share add-on of this room.
    /// </summary>
    public bool Synchronize(Item share)
    {
        if (!_metadata.TryGetValue(share.Id, out var box) || box.Descriptor.CanonicalName != ShareName || !IsAttached(share)) {
            return false;
        }

        var stacked = Module.DescribeDefinitions(_definitions.Keys).Select(description => description.Definition)
            .Where(definition => definition is { Availability: WiredVariableAvailability.Shared, Link: null } && definition.RoomId == _room.Id
                && _definitions.TryGetValue(definition.ItemId, out var variable) && IsAttached(variable.Item)
                && variable.Item.GetX == share.GetX && variable.Item.GetY == share.GetY)
            .ToArray();

        foreach (var definition in stacked) {
            var published = Publication(definition, box);

            foreach (var reference in Module.ListReferences(definition.ItemId)) {
                SharedAddons.Save(reference.ItemId, published);
            }
        }

        return true;
    }

    private WiredSharedAddons Publication(WiredVariableDefinition definition, WiredVariableMetadataBox share)
    {
        var connector = share.Category(0) ? MetadataOn(definition.ItemId, "wf_xtra_var_text_connector")?.TextConnector : null;

        return new()
        {
            TextConnector = [.. (connector ?? new Dictionary<int, string>()).OrderBy(pair => pair.Key).Select(pair => new KeyValuePair<int, string>(pair.Key, pair.Value))]
        };
    }

    /// <summary>
    /// What a reference shows of the add-ons published to it: the categories its stack's Import add-on has on, none without one.
    /// Only a reference to a variable of another room receives anything.
    /// </summary>
    private WiredSharedAddons? ImportedAddons(WiredVariableDefinition reference) =>
        reference.Link is { } link && link.SourceRoomId != _room.Id && MetadataOn(reference.ItemId, ImportName) is { } import
        && SharedAddons.Load(reference.ItemId) is { } published
            ? published.Pick(import.Category(0), import.Category(1), import.Category(2)) : null;

    private IReadOnlyDictionary<int, string> TextConnectorOf(WiredVariableDefinition definition) =>
        MetadataOn(definition.ItemId, "wf_xtra_var_text_connector")?.TextConnector
        ?? (ImportedAddons(definition) is { TextConnector.IsEmpty: false } imported ? imported.TextConnector.ToDictionary() : new Dictionary<int, string>());
}
