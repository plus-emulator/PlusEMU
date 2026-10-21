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
/// Observed on hhus (r90, pair-leaderboard): every room entry carries the snapshot (10 bytes when empty), a save republishes it at
/// once, a pickup republishes it without the board, and row updates after value writes arrive seconds later (+5.3 s, +9.4 s), not in
/// the write's tick. Observed on AIR WIN63-202610151745-318700621: writes at +0.3, +12.3, +24.3 and +36.3 s were pushed at +10.3, +20.3,
/// +30.3 and +40.3 s, so changed variables go out on a fixed <see cref="LeaderboardPushPeriod"/> grid.
/// </summary>
public partial class WiredComponent
{
    public const int MaxLeaderboards = 4;
    public const int MaxLeaderboardChoices = 10;
    public static readonly TimeSpan LeaderboardPushPeriod = TimeSpan.FromSeconds(10);
    private DateTimeOffset? _leaderboardPushDue;

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

    // Observed on AIR WIN63-202610151745-318700621: a board's QueryRevision comes from one counter for the whole room (3, 4, ... 10 over
    // successive saves of different boards), and the room's boards are numbered from 1 as overlays.
    private int _leaderboardRoomRevision;
    private readonly Dictionary<int, uint> _leaderboardOverlayItems = [];

    private int LeaderboardRevision(uint itemId)
    {
        lock (_leaderboardGate) {
            return _leaderboardRevisions.TryGetValue(itemId, out var revision) ? revision : _leaderboardRevisions[itemId] = ++_leaderboardRoomRevision;
        }
    }

    private int LeaderboardOverlay(uint itemId)
    {
        LeaderboardDescriptors();

        lock (_leaderboardGate) {
            if (_leaderboardOverlayItems.FirstOrDefault(pair => pair.Value == itemId) is { Key: > 0 } found) {
                return found.Key;
            }
        }

        // Observed on AIR WIN63-202610201032-107964540: a published board is overlay 1 in its own editor even when its stack has no user
        // variable yet, so no descriptor is sent. Published boards are numbered in furni id order.
        var published = ConfiguredBoxes("wf_xtra_var_leaderboard")
            .Where(box => WiredLeaderboardConfig.Decode(box.Configuration.IntParams, box.Configuration.VariableIds, box.Configuration.Text)?.Published == true)
            .Select(box => box.Item.Id);

        return published.Count(id => id < itemId) + 1;
    }

    // Observed on AIR WIN63-202610151745-318700621: a "while in room" user variable is offered too, but ranks nobody.
    private IReadOnlyList<WiredLeaderboardChoice> BoardChoices(IWiredConfiguredItem box) =>
        [.. Variables.LeaderboardChoices(box.Item.GetX, box.Item.GetY).TakeLast(MaxLeaderboardChoices)];

    /// <summary>Section 8 of the add-on's editor: the stack's user variables, the bottom ten offered (Addon.updateCapabilities).</summary>
    public WiredLeaderboardContext LeaderboardContext(IWiredConfiguredItem box)
    {
        var candidates = Variables.LeaderboardChoices(box.Item.GetX, box.Item.GetY);
        var published = WiredLeaderboardConfig.Decode(box.Configuration.IntParams, box.Configuration.VariableIds, box.Configuration.Text)?.Published == true;

        return new(published ? LeaderboardOverlay(box.Item.Id) : 0, box.Item.GetX, box.Item.GetY, candidates.Count, [.. candidates.Take(MaxLeaderboardChoices)]);
    }

    /// <summary>The room's leaderboard snapshot for one user, as on room entry.</summary>
    public void SendLeaderboardSnapshot(GameClient session) => session.Send(new WiredVariableLeaderboardSnapshotComposer(LeaderboardDescriptors()));

    /// <summary>The published boards of the room, lowest furni id first, each with its stack's user variables to choose from.</summary>
    public IReadOnlyList<WiredLeaderboardDescriptor> LeaderboardDescriptors(uint? leaving = null)
    {
        var descriptors = new List<WiredLeaderboardDescriptor>();
        var overlays = new Dictionary<int, uint>();

        foreach (var box in ConfiguredBoxes("wf_xtra_var_leaderboard").Where(box => box.Item.Id != leaving).OrderBy(box => box.Item.Id)) {
            if (WiredLeaderboardConfig.Decode(box.Configuration.IntParams, box.Configuration.VariableIds, box.Configuration.Text) is not { Published: true } config) {
                continue;
            }

            var choices = BoardChoices(box).Select(choice => new WiredLeaderboardOverride(choice.VariableId,
                config.Overrides.FirstOrDefault(entry => entry.VariableId == choice.VariableId)?.Name
                ?? WiredLeaderboardConfig.SuggestName(choice.Description.Definition.Name))).ToArray();

            // A board without a user variable has no default choice, which the client refuses (isDescriptorStructurallyValid).
            if (choices.Length == 0) {
                continue;
            }

            descriptors.Add(new(descriptors.Count + 1, LeaderboardRevision(box.Item.Id), config, choices, choices[0].VariableId));
            overlays[descriptors.Count] = box.Item.Id;

            if (descriptors.Count == MaxLeaderboards) {
                break;
            }
        }

        if (leaving == null) {
            lock (_leaderboardGate) {
                _leaderboardOverlayItems.Clear();

                foreach (var (overlay, item) in overlays) {
                    _leaderboardOverlayItems[overlay] = item;
                }
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
        uint boardId;

        lock (_leaderboardGate) {
            boardId = _leaderboardOverlayItems.GetValueOrDefault(descriptor.OverlayInstanceId);
        }

        if (boardId == 0 || !TryGet(boardId, out var found) || found is not IWiredConfiguredItem box
            || BoardChoices(box).FirstOrDefault(choice => choice.VariableId == variableId) is not { } choice) {
            return null;
        }

        var config = descriptor.Config;
        var module = Variables.Module;
        var filter = config.Filter && DefinitionOf(config.FilterVariableId) is { } filterId ? module.GetStoredHolders(filterId) : null;
        long? operand = config.Comparison ? config.OperandKind == 0 ? config.Literal
            : DefinitionOf(config.GlobalOperandVariableId) is { } globalId ? module.GetStoredHolders(globalId).Values.FirstOrDefault()?.Value : null : null;
        var ranked = choice.Persistent ? WiredLeaderboardRanking.Rank(config, module.GetStoredHolders(choice.Description.Definition.ItemId), filter, operand)
            : WiredLeaderboardRanking.Rank(config, new Dictionary<WiredVariableKey, WiredVariableValue>(), filter, operand);

        return new(descriptor.OverlayInstanceId, descriptor.QueryRevision, variableId, index, ranked.Count,
            WiredLeaderboardRanking.Chunk(ranked, index, choice, LeaderboardProfiles, config));
    }

    private static uint? DefinitionOf(string variableId) => WiredVariableDescription.TryParseCatalogId(variableId, out _, out var token)
        && WiredVariableModule.TryDefinitionId(token, out var id) ? id : null;

    /// <summary>A board was saved: its revision moves on and everyone in the room gets the new snapshot.</summary>
    private void LeaderboardSaved(IWiredConfiguredItem box)
    {
        lock (_leaderboardGate) {
            _leaderboardRevisions[box.Item.Id] = ++_leaderboardRoomRevision;
        }

        BroadcastLeaderboards(null);
    }

    /// <summary>A board left the room: everyone gets the snapshot without it.</summary>
    private void LeaderboardRemoved(uint itemId) => BroadcastLeaderboards(itemId);

    private void BroadcastLeaderboards(uint? leaving)
    {
        // An unloading room has dropped its users before its furniture; with nobody to tell, the boxes are not read.
        var clients = _room.GetRoomUserManager()?.GetRoomUsers().Select(user => user.GetClient()).OfType<GameClient>().ToList() ?? [];

        if (clients.Count == 0) {
            return;
        }

        var snapshot = new WiredVariableLeaderboardSnapshotComposer(LeaderboardDescriptors(leaving));

        foreach (var client in clients) {
            client.Send(snapshot);
        }
    }


    private void LeaderboardValuesChanged(IReadOnlyList<WiredVariableChange> changes)
    {
        lock (_leaderboardGate) {
            if (_leaderboardSubscriptions.Count == 0) {
                return;
            }

            foreach (var change in changes) {
                _dirtyLeaderboardVariables.Add(change.Key.DefinitionId);
            }

            _leaderboardPushDue ??= NextLeaderboardPush(_clock.GetUtcNow());
        }
    }

    internal static DateTimeOffset NextLeaderboardPush(DateTimeOffset now) =>
        DateTimeOffset.FromUnixTimeMilliseconds((now.ToUnixTimeMilliseconds() / (long)LeaderboardPushPeriod.TotalMilliseconds + 1)
            * (long)LeaderboardPushPeriod.TotalMilliseconds);

    /// <summary>Subscribers whose selected variable changed get their chunks again; users who left the room are dropped.</summary>
    private void FlushLeaderboards()
    {
        KeyValuePair<(int HabboId, int OverlayId), LeaderboardSubscription>[] due;

        lock (_leaderboardGate) {
            if (_dirtyLeaderboardVariables.Count == 0 || _leaderboardPushDue is not { } pushAt || _clock.GetUtcNow() < pushAt) {
                return;
            }

            _leaderboardPushDue = null;

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
