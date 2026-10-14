using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Links;
using Plus.HabboHotel.Items.Wired.Runtime;

namespace Plus.HabboHotel.Rooms.Instance;

/// <summary>Room services of the October boxes (r88): link endpoints, room initialization and room-wide add-on lookups.</summary>
public partial class WiredComponent
{
    private IWiredLinkStore? _linkStore;

    /// <summary>The WIRED Link networks; tests replace the database store.</summary>
    public IWiredLinkStore LinkStore
    {
        get => _linkStore ??= new DatabaseWiredLinkStore(_database);
        internal set => _linkStore = value;
    }

    /// <summary>The room has loaded its furniture: Room Initialized triggers fire once.</summary>
    public bool RoomInitialized() => Dispatch(new(WiredEventKind.RoomInitialized));

    /// <summary>The placed, configured boxes of one canonical name, bottom of the room first.</summary>
    internal IEnumerable<IWiredConfiguredItem> ConfiguredBoxes(string canonicalName) => (_room.GetRoomItemHandler()?.GetFloor ?? [])
        .Where(item => DescriptorOf(item)?.CanonicalName == canonicalName).OrderBy(item => item.Id)
        .Select(item => TryGet(item.Id, out var box) ? box as IWiredConfiguredItem : null).OfType<IWiredConfiguredItem>()
        .Where(box => ReferenceEquals(_room.GetRoomItemHandler().GetItem(box.Item.Id), box.Item));

    /// <summary>Achievements an Achievement Enabler of this room lists, one per line (class_4012.as:27).</summary>
    internal bool AchievementEnabled(string achievement) => achievement.Length > 0 && ConfiguredBoxes("wf_xtra_achievement_enabler")
        .SelectMany(box => box.Configuration.Text.Split('\n'))
        .Any(line => string.Equals(line.Trim(), achievement, StringComparison.OrdinalIgnoreCase));

    /// <summary>Custom-text Global Placeholders of this room as $(name) to text; a later box with the same name wins.</summary>
    internal IReadOnlyDictionary<string, string> GlobalPlaceholders()
    {
        var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var box in ConfiguredBoxes("wf_xtra_global_placeholder")) {
            var parts = box.Configuration.Text.Split('\t', 2);

            if (box.Configuration.IntParams is [0, ..] && parts[0].Length > 0) {
                placeholders[parts[0]] = parts.Length > 1 ? parts[1] : "";
            }
        }

        return placeholders;
    }

    /// <summary>A box's new settings are live: leaderboards republish, the web API takes the saved keys.</summary>
    private void OctoberConfigurationPublished(IWiredItem box)
    {
        if (box is IWiredConfiguredItem { Descriptor.CanonicalName: "wf_xtra_var_leaderboard" } leaderboard) {
            LeaderboardSaved(leaderboard);
        }
    }

    internal bool SendLink(Item[] endpoints, WiredLinkPayload payload, WiredEventKind kind) =>
        WiredLinkTransport.Send(_room, endpoints, payload, kind, LinkStore, _room.Rooms);
}
