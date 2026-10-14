using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Leaderboard;
using Plus.HabboHotel.Items.Wired.Variables;

namespace Plus.HabboHotel.Rooms.Instance;

/// <summary>
/// Variable Leaderboard overlays (VariableLeaderboardController, WIN63-202610131318-860371703). On room entry the client asks with
/// [0, 0, "0", no chunks] and gets the snapshot of published boards (at most four); it then subscribes to at most two 50-row chunks
/// of the selected variable and gets them now and again when the variable changes. A save of a board republishes the snapshot.
/// </summary>
public partial class WiredComponent
{
    public const int MaxLeaderboards = 4;
    public const int MaxLeaderboardChoices = 10;

    private readonly object _leaderboardGate = new();
    private readonly Dictionary<uint, int> _leaderboardRevisions = [];
    private readonly Dictionary<(int HabboId, int OverlayId), LeaderboardSubscription> _leaderboardSubscriptions = [];
    private readonly HashSet<uint> _dirtyLeaderboardVariables = [];
    private IWiredLeaderboardProfiles? _leaderboardProfiles;

    private sealed record LeaderboardSubscription(GameClient Session, int Revision, string VariableId, int[] Chunks);

    public IWiredLeaderboardProfiles LeaderboardProfiles
    {
        get => _leaderboardProfiles ??= new DatabaseWiredLeaderboardProfiles(_database);
        internal set => _leaderboardProfiles = value;
    }

    private int LeaderboardRevision(uint itemId)
    {
        lock (_leaderboardGate) {
            return _leaderboardRevisions.TryGetValue(itemId, out var revision) ? revision : _leaderboardRevisions[itemId] = 1;
        }
    }

    private IReadOnlyList<WiredLeaderboardChoice> PersistentChoices(IWiredConfiguredItem box) =>
        [.. Variables.LeaderboardChoices(box.Item.GetX, box.Item.GetY).Where(choice => choice.Persistent).TakeLast(MaxLeaderboardChoices)];

    /// <summary>Section 8 of the add-on's editor: the stack's user variables, the bottom ten offered (Addon.updateCapabilities).</summary>
    public WiredLeaderboardContext LeaderboardContext(IWiredConfiguredItem box)
    {
        var candidates = Variables.LeaderboardChoices(box.Item.GetX, box.Item.GetY);
        var published = WiredLeaderboardConfig.Decode(box.Configuration.IntParams, box.Configuration.VariableIds, box.Configuration.Text)?.Published == true;

        return new(published ? checked((int)box.Item.Id) : 0, box.Item.GetX, box.Item.GetY, candidates.Count, [.. candidates.Take(MaxLeaderboardChoices)]);
    }

    /// <summary>The published boards of the room, lowest furni id first, each with its persisted user variables to choose from.</summary>
    public IReadOnlyList<WiredLeaderboardDescriptor> LeaderboardDescriptors()
    {
        var descriptors = new List<WiredLeaderboardDescriptor>();

        foreach (var box in ConfiguredBoxes("wf_xtra_var_leaderboard")) {
            if (WiredLeaderboardConfig.Decode(box.Configuration.IntParams, box.Configuration.VariableIds, box.Configuration.Text) is not { Published: true } config) {
                continue;
            }

            var choices = PersistentChoices(box).Select(choice => new WiredLeaderboardOverride(choice.VariableId,
                config.Overrides.FirstOrDefault(entry => entry.VariableId == choice.VariableId)?.Name
                ?? WiredLeaderboardConfig.SuggestName(choice.Description.Definition.Name))).ToArray();

            // A board without a persisted user variable has no default choice, which the client refuses (isDescriptorStructurallyValid).
            if (choices.Length == 0) {
                continue;
            }

            descriptors.Add(new(checked((int)box.Item.Id), LeaderboardRevision(box.Item.Id), config, choices, choices[0].VariableId));

            if (descriptors.Count == MaxLeaderboards) {
                break;
            }
        }

        return descriptors;
    }

    /// <summary>The subscription packet (package_221/class_3707): room-open, or the chunks of a selected variable.</summary>
    public void SubscribeLeaderboard(GameClient session, int overlayId, int revision, string variableId, int[] chunks)
    {
        if (session.GetHabbo() is not { } habbo || !ReferenceEquals(habbo.CurrentRoom, _room)) {
            return;
        }

        if (overlayId == 0) {
            if (revision == 0 && variableId == "0" && chunks.Length == 0) {
                session.Send(new WiredVariableLeaderboardSnapshotComposer(LeaderboardDescriptors()));
            }

            return;
        }

        var descriptor = LeaderboardDescriptors().FirstOrDefault(entry => entry.OverlayInstanceId == overlayId);

        lock (_leaderboardGate) {
            _leaderboardSubscriptions.Remove((habbo.Id, overlayId));
        }

        if (descriptor == null || descriptor.QueryRevision != revision) {
            // The client holds a board that changed or went away: it re-bootstraps from a fresh snapshot.
            session.Send(new WiredVariableLeaderboardSnapshotComposer(LeaderboardDescriptors()));

            return;
        }

        if (descriptor.Choices.All(choice => choice.VariableId != variableId)) {
            return;
        }

        lock (_leaderboardGate) {
            _leaderboardSubscriptions[(habbo.Id, overlayId)] = new(session, revision, variableId, chunks);
        }

        foreach (var chunk in chunks) {
            if (LeaderboardChunk(descriptor, variableId, chunk) is { } rows) {
                session.Send(new WiredVariableLeaderboardChunkComposer(rows));
            }
        }
    }

    private WiredLeaderboardChunk? LeaderboardChunk(WiredLeaderboardDescriptor descriptor, string variableId, int index)
    {
        if (!TryGet((uint)descriptor.OverlayInstanceId, out var found) || found is not IWiredConfiguredItem box
            || PersistentChoices(box).FirstOrDefault(choice => choice.VariableId == variableId) is not { } choice) {
            return null;
        }

        var config = descriptor.Config;
        var module = Variables.Module;
        var filter = config.Filter && DefinitionOf(config.FilterVariableId) is { } filterId ? module.GetStoredHolders(filterId) : null;
        long? operand = config.Comparison ? config.OperandKind == 0 ? config.Literal
            : DefinitionOf(config.GlobalOperandVariableId) is { } globalId ? module.GetStoredHolders(globalId).Values.FirstOrDefault()?.Value : null : null;
        var ranked = WiredLeaderboardRanking.Rank(config, module.GetStoredHolders(choice.Description.Definition.ItemId), filter, operand);

        return new(descriptor.OverlayInstanceId, descriptor.QueryRevision, variableId, index, ranked.Count,
            WiredLeaderboardRanking.Chunk(ranked, index, choice, LeaderboardProfiles));
    }

    private static uint? DefinitionOf(string variableId) => WiredVariableDescription.TryParseCatalogId(variableId, out _, out var token)
        && WiredVariableModule.TryDefinitionId(token, out var id) ? id : null;

    /// <summary>A board was saved: its revision moves on and everyone in the room gets the new snapshot.</summary>
    private void LeaderboardSaved(IWiredConfiguredItem box)
    {
        lock (_leaderboardGate) {
            _leaderboardRevisions[box.Item.Id] = LeaderboardRevisionUnlocked(box.Item.Id) + 1;
        }

        var snapshot = new WiredVariableLeaderboardSnapshotComposer(LeaderboardDescriptors());

        foreach (var client in _room.GetRoomUserManager()?.GetRoomUsers().Select(user => user.GetClient()).OfType<GameClient>() ?? []) {
            client.Send(snapshot);
        }
    }

    private int LeaderboardRevisionUnlocked(uint itemId) => _leaderboardRevisions.GetValueOrDefault(itemId, 1);

    private void LeaderboardValuesChanged(IReadOnlyList<WiredVariableChange> changes)
    {
        lock (_leaderboardGate) {
            if (_leaderboardSubscriptions.Count == 0) {
                return;
            }

            foreach (var change in changes) {
                _dirtyLeaderboardVariables.Add(change.Key.DefinitionId);
            }
        }
    }

    /// <summary>Subscribers whose selected variable changed get their chunks again; users who left the room are dropped.</summary>
    private void FlushLeaderboards()
    {
        KeyValuePair<(int HabboId, int OverlayId), LeaderboardSubscription>[] due;

        lock (_leaderboardGate) {
            if (_dirtyLeaderboardVariables.Count == 0) {
                return;
            }

            due = [.. _leaderboardSubscriptions.Where(pair => DefinitionOf(pair.Value.VariableId) is { } id && _dirtyLeaderboardVariables.Contains(id))];
            _dirtyLeaderboardVariables.Clear();
        }

        var descriptors = LeaderboardDescriptors();

        foreach (var (key, subscription) in due) {
            if (!ReferenceEquals(subscription.Session.GetHabbo()?.CurrentRoom, _room)) {
                lock (_leaderboardGate) {
                    _leaderboardSubscriptions.Remove(key);
                }

                continue;
            }

            if (descriptors.FirstOrDefault(entry => entry.OverlayInstanceId == key.OverlayId && entry.QueryRevision == subscription.Revision) is not { } descriptor) {
                continue;
            }

            foreach (var chunk in subscription.Chunks) {
                if (LeaderboardChunk(descriptor, subscription.VariableId, chunk) is { } rows) {
                    subscription.Session.Send(new WiredVariableLeaderboardChunkComposer(rows));
                }
            }
        }
    }
}
