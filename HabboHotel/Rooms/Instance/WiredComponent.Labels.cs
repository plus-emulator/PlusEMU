using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Labels;

namespace Plus.HabboHotel.Rooms.Instance;

/// <summary>
/// WIRED Label bubbles of the room (furni_label logic). Everyone gets the full list on entry and changes as they happen;
/// a label for inspectors only goes to users with wired inspection rights and leaves everyone else's room as a removal.
/// </summary>
public partial class WiredComponent
{
    private readonly object _labelGate = new();
    private readonly Dictionary<uint, WiredLabel> _labels = [];
    private bool _labelsLoaded;
    private IWiredLabelStore? _labelStore;

    public IWiredLabelStore LabelStore
    {
        get => _labelStore ??= new DatabaseWiredLabelStore(_database);
        internal set => _labelStore = value;
    }

    private IEnumerable<Item> LabelItems() => (_room.GetRoomItemHandler()?.GetFloor ?? []).Where(WiredLabel.IsLabel);

    private void EnsureLabels()
    {
        if (_labelsLoaded) {
            return;
        }

        var items = LabelItems().Select(item => item.Id).ToArray();
        var stored = LabelStore.Load(items);

        foreach (var id in items) {
            _labels[id] = stored.GetValueOrDefault(id) ?? WiredLabel.Default(id);
        }

        _labelsLoaded = true;
    }

    public IReadOnlyList<WiredLabel> Labels()
    {
        lock (_labelGate) {
            EnsureLabels();

            return [.. _labels.Values.OrderBy(label => label.FurniId)];
        }
    }

    // Everyone sees visibility 0; wired readers see every label.
    private bool CanSeeLabel(GameClient session, WiredLabel label) => label.Visibility == 0 || Settings.CanInspect(session);

    /// <summary>The room's labels this user may see, sent on room entry; empty when there are none.</summary>
    public void SendLabels(GameClient session) =>
        session.Send(new WiredLabelsComposer([.. Labels().Where(label => CanSeeLabel(session, label))]));

    private void PublishLabel(WiredLabel? changed, uint furniId)
    {
        foreach (var client in _room.GetRoomUserManager()?.GetRoomUsers().Select(user => user.GetClient()).OfType<GameClient>() ?? []) {
            client.Send(changed != null && CanSeeLabel(client, changed)
                ? new WiredLabelsUpdateComposer([changed], [])
                : new WiredLabelsUpdateComposer([], [furniId]));
        }
    }

    private bool TryLabel(GameClient session, uint furniId, out WiredLabel label)
    {
        label = null!;

        if (!ReferenceEquals(session.GetHabbo()?.CurrentRoom, _room) || !Settings.CanModify(session)
            || _room.GetRoomItemHandler().GetItem(furniId) is not { } item || !WiredLabel.IsLabel(item)) {
            return false;
        }

        lock (_labelGate) {
            EnsureLabels();
            label = _labels.GetValueOrDefault(furniId) ?? WiredLabel.Default(furniId);
        }

        return true;
    }

    private void StoreLabel(WiredLabel label)
    {
        LabelStore.Save(label);

        lock (_labelGate) {
            _labels[label.FurniId] = label;
        }

        PublishLabel(label, label.FurniId);
    }

    /// <summary>Label text from the bubble; saved only with wired write permission.</summary>
    public bool SaveLabelText(GameClient session, uint furniId, string text)
    {
        if (text.Length > WiredLabel.MaxTextLength || !TryLabel(session, furniId, out var label)) {
            return false;
        }

        StoreLabel(label with { Text = text });

        return true;
    }

    /// <summary>Label settings from the settings view: one of the eight colours, visibility, always open.</summary>
    public bool SaveLabelSettings(GameClient session, uint furniId, int color, int visibility, bool alwaysOpen)
    {
        if (!WiredLabel.ValidSettings(color, visibility, alwaysOpen) || !TryLabel(session, furniId, out var label)) {
            return false;
        }

        StoreLabel(label with { BubbleColor = color, Visibility = visibility, AlwaysOpen = alwaysOpen });

        return true;
    }

    private void LabelAttached(Item item)
    {
        WiredLabel? label;

        lock (_labelGate) {
            if (!_labelsLoaded || _labels.ContainsKey(item.Id)) {
                return;
            }

            label = LabelStore.Load([item.Id]).GetValueOrDefault(item.Id) ?? WiredLabel.Default(item.Id);
            _labels[item.Id] = label;
        }

        PublishLabel(label, item.Id);
    }

    private void LabelDetached(Item item)
    {
        lock (_labelGate) {
            if (!_labels.Remove(item.Id)) {
                return;
            }
        }

        PublishLabel(null, item.Id);
    }
}
