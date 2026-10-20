using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Variables.Fx;

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
                WiredSharedVariableBroadcast.Refresh(reference.RoomId);
            }
        }

        return true;
    }

    private WiredSharedAddons Publication(WiredVariableDefinition definition, WiredVariableMetadataBox share)
    {
        var connector = share.Category(0) ? MetadataOn(definition.ItemId, "wf_xtra_var_text_connector")?.TextConnector : null;
        var level = share.Category(1) ? MetadataOn(definition.ItemId, "wf_xtra_var_lvlup_system")?.Configuration.Text : null;
        var tile = _definitions[definition.ItemId].Item;
        // Variable FX draw on a user variable's holders only.
        var effects = share.Category(2) && definition.Target == WiredVariableTarget.User
            ? _metadata.Values.Where(box => WiredVariableMetadataBox.IsFx(box.Descriptor.CanonicalName) && IsAttached(box.Item)
                && box.Item.GetX == tile.GetX && box.Item.GetY == tile.GetY && WiredVariableFxSettings.UsesUserSource(box.Configuration))
                .OrderBy(box => box.Item.GetZ).ThenBy(box => box.Item.Id)
                .Select(box => new WiredSharedFx(box.Descriptor.CanonicalName, box.Configuration.IntParams, box.Configuration.VariableIds, box.Configuration.Text))
            : [];

        return new()
        {
            TextConnector = [.. (connector ?? new Dictionary<int, string>()).OrderBy(pair => pair.Key).Select(pair => new KeyValuePair<int, string>(pair.Key, pair.Value))],
            Level = level,
            Fx = [.. effects]
        };
    }

    /// <summary>
    /// What a reference shows of the add-ons published to it: the categories its stack's Import add-on has on, none without one.
    /// Only a reference to a variable of another room receives anything.
    /// </summary>
    private WiredSharedAddons? ImportedAddons(WiredVariableDefinition reference) =>
        reference.Link is { } link && link.SourceRoomId != _room.Id && MetadataOn(reference.ItemId, ImportName) is { } import
        && PublishedTo(reference.ItemId) is { } published
            ? published.Pick(import.Category(0), import.Category(1), import.Category(2)) : null;

    private readonly object _publishedGate = new();
    private readonly Dictionary<uint, WiredSharedAddons?> _published = [];
    private long _publishedEpoch = -1;
    private long _publishedGeneration;

    /// <summary>
    /// What the store holds for a reference, read once and kept. It is read again after a Refresh from another room or after a box was
    /// loaded, saved or removed in this room, so a tick never waits on the database.
    /// </summary>
    private WiredSharedAddons? PublishedTo(uint referenceItemId)
    {
        long epoch;
        long generation;

        lock (_membershipGate) {
            epoch = _membershipEpoch;
        }

        lock (_publishedGate) {
            if (_publishedEpoch != epoch) {
                _published.Clear();
                _publishedEpoch = epoch;
            }

            if (_published.TryGetValue(referenceItemId, out var kept)) {
                return kept;
            }

            generation = _publishedGeneration;
        }

        var loaded = SharedAddons.Load(referenceItemId);

        lock (_publishedGate) {
            // A Refresh that arrived during the read leaves this answer out, so the next read asks again.
            if (generation == _publishedGeneration && _publishedEpoch == epoch) {
                _published[referenceItemId] = loaded;
            }
        }

        return loaded;
    }

    private void ForgetPublished()
    {
        lock (_publishedGate) {
            _published.Clear();
            _publishedGeneration++;
        }
    }

    private WiredVariableDefinition? ReferenceOf(uint definitionId) => _definitions.TryGetValue(definitionId, out var box) && box.HasPersistedConfiguration
        && box.Descriptor.CanonicalName == "wf_var_reference" && IsAttached(box.Item)
        && WiredVariableDefinitions.TryDecode("wf_var_reference", definitionId, _room.Id, _room.OwnerId > 0 ? (uint)_room.OwnerId : 0, box.Configuration, out var definition, out _)
            ? definition : null;

    private IReadOnlyDictionary<int, string> TextConnectorOf(WiredVariableDefinition definition) =>
        MetadataOn(definition.ItemId, "wf_xtra_var_text_connector")?.TextConnector
        ?? (ImportedAddons(definition) is { TextConnector.IsEmpty: false } imported ? imported.TextConnector.ToDictionary() : new Dictionary<int, string>());

    /// <summary>The level-up system that adds sub-variables to a variable: the one stacked with it, or one a reference imports.</summary>
    private WiredVariableLevelSystem? LevelOf(uint definitionId)
    {
        if (DerivedMetadataOn(definitionId)?.LevelSystem is { } own) {
            return own;
        }

        return ReferenceOf(definitionId) is { } reference && ImportedAddons(reference)?.Level is { } text
            && WiredVariableLevelSystem.TryParse(text, out var imported) ? imported : null;
    }

    /// <summary>
    /// The imported Variable FX of the references that show them, at most <see cref="MaxFxBoxes"/>. Their configuration ids count down
    /// from -1 over the whole room, so two references never share one. Observed on AIR WIN63-202610201032-107964540: only the id -1
    /// was seen. Ids of -2 and lower, and the count over the whole room, were not checked.
    /// </summary>
    private IEnumerable<(WiredVariableDefinition Reference, WiredSharedFx Fx, int ConfigId)> ImportedFx(IReadOnlyDictionary<uint, WiredVariableDescription> authorized) =>
        _definitions.Keys.Order().Where(authorized.ContainsKey).Select(ReferenceOf).OfType<WiredVariableDefinition>()
            .SelectMany(reference => (ImportedAddons(reference)?.Fx ?? []).Select(fx => (reference, fx)))
            .Take(MaxFxBoxes).Select((pair, index) => (pair.reference, pair.fx, -(index + 1)));
}
