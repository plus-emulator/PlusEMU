using Plus.Database;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables.Fx;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>One room's production variable state. The room facade owns and calls these lifecycle methods.</summary>
public sealed partial class WiredRoomVariables
{
    private readonly Room _room;
    private readonly TimeProvider _clock;
    private readonly WiredVariableConfigurationPersistence _persistence;
    private readonly Dictionary<uint, WiredVariableDefinitionBox> _definitions = [];
    private readonly Plus.HabboHotel.Quests.IQuestManager? _quests;
    private readonly WiredSharedVariableBroadcast.Endpoint _broadcast;
    public WiredVariableModule Module { get; }
    public WiredVariableEditor Editor { get; }
    public WiredVariableFxTracker Fx { get; }
    public IReadOnlyCollection<WiredVariableDefinitionBox> Definitions => _definitions.Values.ToArray();
    public WiredVariableCatalog Catalog()
    {
        var definitions = Module.DescribeDefinitions(_definitions.Keys).Select(description =>
        {
            var connector = TextConnectorOf(description.Definition);

            return description with { TextConnector = connector, TextConnectorFallback = (connector as WiredTextConnector)?.Fallback };
        }).ToArray();

        return new(definitions.Concat(definitions.SelectMany(DerivedDescriptions)).ToArray());
    }
    public bool FxDirty { get; private set; } = true;
    private readonly HashSet<WiredVariableKey> _rewritten = [];

    /// <summary>What Share add-ons published to the references of this room.</summary>
    internal IWiredSharedAddonStore SharedAddons { get; set; }

    public WiredRoomVariables(Room room, IDatabase database, TimeProvider clock,
        Func<WiredVariableReference, WiredVariableHolder, WiredVariableFrame, long?>? builtinRead = null,
        Func<WiredVariableReference, WiredVariableHolder, int, WiredVariableFrame, bool>? builtinWrite = null,
        Action<Item, WiredVariableFrame>? stateChanged = null, IItemTravelStore? travelStore = null,
        Plus.HabboHotel.Quests.IQuestManager? quests = null)
    {
        _room = room;
        _clock = clock;
        _quests = quests;
        Module = new(room.Id, new RoomDirectory(new DatabaseWiredVariableDirectory(database), this), new DatabaseWiredVariableStore(database), clock,
            new RoomWiredBuiltinVariables(room, builtinRead, builtinWrite, stateChanged, new RoomItemMetadataStore(database), travelStore, quests, QuestBinding), ResolveDerived);
        Editor = new(Module);
        Fx = new(Module);
        _persistence = new(database, Module, clock);
        SharedAddons = new DatabaseWiredSharedAddonStore(database);
        _broadcast = new(Module, playerId => playerId is > 0 and <= int.MaxValue
            ? room.GetRoomUserManager()?.GetRoomUserByHabbo((int)playerId)?.VirtualId ?? 0 : 0, InvalidateFx);
        WiredSharedVariableBroadcast.Register(_broadcast);
    }

    /// <summary>The room's own variables come from its loaded boxes; every other question goes to the database.</summary>
    private sealed class RoomDirectory(IWiredVariableDirectory inner, WiredRoomVariables owner) : IWiredVariableDirectory
    {
        public WiredVariableDefinition? Find(uint itemId) => inner.Find(itemId);
        public uint? GetRoomOwner(uint roomId) => inner.GetRoomOwner(roomId);
        public IReadOnlyList<WiredSharedVariable> ListShared(uint ownerId, uint exceptRoomId) => inner.ListShared(ownerId, exceptRoomId);
        public IReadOnlyList<WiredVariableDefinition> ListRoom(uint roomId) => roomId == owner._room.Id ? owner.LoadedDefinitions() : inner.ListRoom(roomId);
        public IReadOnlyList<WiredVariableDefinition> ListReferences(uint sourceRoomId, uint sourceItemId) => inner.ListReferences(sourceRoomId, sourceItemId);
    }

    private IReadOnlyList<WiredVariableDefinition> LoadedDefinitions()
    {
        KeyValuePair<uint, WiredVariableDefinitionBox>[] loaded;

        lock (_membershipGate) {
            loaded = [.. _definitions];
        }

        var found = new List<WiredVariableDefinition>();

        foreach (var (id, box) in loaded) {
            if (box.HasPersistedConfiguration && WiredVariableDefinitions.TryDecode(box.Descriptor.CanonicalName, id, _room.Id,
                _room.OwnerId > 0 ? (uint)_room.OwnerId : 0, box.Configuration, out var definition, out _)) {
                found.Add(definition!);
            }
        }

        return found;
    }

    public IWiredConfiguredItem? CreateBox(Item item)
    {
        if (item.Definition.WiredDescriptor is { } descriptor) {
            if (descriptor.CanonicalName == "wf_trg_var_changed") {
                return new WiredVariableChangedTrigger(_room, item, descriptor);
            }

            if (descriptor.CanonicalName == "wf_xtra_text_input_variable") {
                return new WiredVariableTextInputBox(_room, item, descriptor);
            }

            if (WiredVariableMetadataBox.Supports(descriptor.CanonicalName)) {
                return new WiredVariableMetadataBox(_room, item, descriptor);
            }

            if (WiredVariableAddonBox.Supports(descriptor.CanonicalName)) {
                return new WiredVariableAddonBox(_room, item, descriptor, Module,
                id => MetadataOn(id, "wf_xtra_var_text_connector")?.TextConnector ?? new Dictionary<int, string>());
            }
        }

        return WiredVariableBoxFactory.Create(_room, item, Module, _clock, _persistence);
    }
    /// <summary>Call after hydration from the companion configuration store. Database failures must abort activation.</summary>
    public void ConfigurationLoaded(IWiredConfiguredItem box)
    {
        if (box is WiredVariableTextInputBox textInput) {
            lock (_membershipGate) {
                _textInputs[box.Item.Id] = textInput;
                _membershipEpoch++;
            }

            return;
        }

        if (box is WiredVariableMetadataBox metadata) {
            lock (_membershipGate) {
                _metadata[box.Item.Id] = metadata;
                _membershipEpoch++;
            }

            FxDirty = true;

            return;
        }

        if (box is not WiredVariableDefinitionBox definition || !definition.HasPersistedConfiguration) {
            return;
        }

        lock (_membershipGate) {
            _definitions[box.Item.Id] = definition;
            _membershipEpoch++;
        }

        if (definition.Descriptor.CanonicalName == "wf_var_room" && !Module.InitializeGlobal(box.Item.Id)) {
            throw new InvalidOperationException("The room variable could not be initialized from its authoritative definition.");
        }

        RememberReference(definition);
        FxDirty = true;
    }
    /// <summary>Publication hook, called after atomic persistence and ApplyConfiguration. It writes no database rows.</summary>
    public void ConfigurationSaved(IWiredConfiguredItem box)
    {
        lock (_membershipGate) {
            if (box is WiredVariableTextInputBox textInput) {
                _textInputs[box.Item.Id] = textInput;
            }

            if (box is WiredVariableMetadataBox metadata) {
                _metadata[box.Item.Id] = metadata;
            }

            if (box is WiredVariableDefinitionBox definition) {
                _definitions[box.Item.Id] = definition;
            }

            _membershipEpoch++;
        }

        if (box is WiredVariableDefinitionBox saved) {
            RememberReference(saved);
        }

        FxDirty = true;
    }
    // Only the listing after a lost source depends on this, so a database error must not fail the load or the save.
    private void RememberReference(WiredVariableDefinitionBox box)
    {
        if (box.Descriptor.CanonicalName != "wf_var_reference") {
            return;
        }

        try {
            Module.RememberReference(box.Item.Id);
        }
        catch (System.Data.Common.DbException) {
        }
    }
    public void HolderLeft(WiredVariableHolder holder)
    {
        Module.HolderLeft(holder);
        Fx.DetachHolder(holder);

        if (holder.Target == WiredVariableTarget.User && holder.CanPersist) {
            Fx.RemoveViewer(holder.StableId);
        }

        FxDirty = true;
    }
    public void ItemDetached(Item item) => ItemDetached(item.Id, WiredVariableRuntimeFrames.FurniHolder(item));
    public void ItemDetached(uint itemId)
    {
        var item = _room.GetRoomItemHandler()?.GetItem(itemId);

        if (item is not null) {
            ItemDetached(item);

            return;
        }

        // Without an attached Item, only durable identity is known. Temporary callers must pass the actual Item.
        ItemDetached(itemId, new(WiredVariableTarget.Furni, itemId, unchecked((int)itemId)));
    }
    private void ItemDetached(uint itemId, WiredVariableHolder? holder)
    {
        lock (_membershipGate) {
            _textInputs.Remove(itemId);
            _metadata.Remove(itemId);
            _definitions.Remove(itemId);
            _membershipEpoch++;
        }

        Module.DetachDefinition(itemId);

        if (holder is { } detached) {
            HolderLeft(detached);
        }

        FxDirty = true;
    }
    /// <summary>Only actual deletion of an owning item, never inventory pickup.</summary>
    public int DefinitionDeleted(uint itemId)
    {
        var removed = Module.DeleteDefinition(itemId);
        ItemDetached(itemId);

        return removed;
    }
    public IReadOnlyList<WiredVariableChange> DrainChanges()
    {
        var changes = Module.DrainChanges();

        if (changes.Count > 0) {
            FxDirty = true;

            // Only a Give with override creates a variable that already had a value.
            foreach (var change in changes.Where(x => x.Kind == WiredVariableChangeKind.Created && x.Before is not null)) {
                _rewritten.Add(change.Key);
            }

            WiredSharedVariableBroadcast.Deliver(Module.RoomId, changes);
        }

        return changes;
    }
    public void InvalidateFx()
    {
        ForgetPublished();
        FxDirty = true;
    }
    public void FxFlushed() => FxDirty = false;
}
