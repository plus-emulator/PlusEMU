using Plus.HabboHotel.GameClients;
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
    internal IEnumerable<IWiredConfiguredItem> ConfiguredBoxes(string canonicalName) => _room.GetRoomItemHandler() is not { } handler ? [] : handler.GetFloor
        .Where(item => item?.Definition != null && DescriptorOf(item)?.CanonicalName == canonicalName).OrderBy(item => item.Id)
        .Select(item => TryGet(item.Id, out var box) ? box as IWiredConfiguredItem : null).OfType<IWiredConfiguredItem>()
        .Where(box => ReferenceEquals(handler.GetItem(box.Item.Id), box.Item));

    /// <summary>Achievements the room's Achievement Enablers list, one per line (class_4012.as:27), first mention first.</summary>
    internal IReadOnlyList<string> EnabledAchievements() => [.. ConfiguredBoxes("wf_xtra_achievement_enabler")
        .SelectMany(box => box.Configuration.Text.Split('\n')).Select(line => line.Trim()).Where(line => line.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)];

    internal bool AchievementEnabled(string achievement) => achievement.Length > 0
        && EnabledAchievements().Contains(achievement, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Observed on AIR WIN63-202610131318-860371703: when any box text in the room uses $(trigger_text), every user entering
    /// (owner or visitor) gets NotificationDialog 2356 "wired.error" right after the room-enter packet 1647.
    /// </summary>
    public void SendTriggerTextNotice(GameClient session)
    {
        var usesTriggerText = (_room.GetRoomItemHandler()?.GetFloor ?? [])
            .Select(item => TryGet(item.Id, out var box) ? box as IWiredConfiguredItem : null).OfType<IWiredConfiguredItem>()
            .Any(box => WiredTextPlaceholders.UsesTriggerText(box.Configuration.Text));

        if (usesTriggerText) {
            session.Send(new Plus.Communication.Packets.Outgoing.Rooms.Notifications.RoomNotificationComposer("wired.error", new Dictionary<string, string>
            {
                { "image", "${image.library.url}album1584/WIRD1.png" },
                { "message", "${room.error.wired.trigger_text_capture}" }
            }));
        }
    }

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

        if (box is IWiredConfiguredItem { Descriptor.CanonicalName: "wf_xtra_var_web_api" } webApi) {
            WebApiSaved(webApi);
        }

        // The editors of Progress Achievement list the enabled achievements from the environment packet.
        if (box is IWiredConfiguredItem { Descriptor.CanonicalName: "wf_xtra_achievement_enabler" }) {
            foreach (var viewer in _fxViewers.Keys.Where(viewer => ReferenceEquals(_room.GetRoomUserManager().GetRoomUserByVirtualId(viewer.VirtualId), viewer))) {
                viewer.GetClient()?.Send(new Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired.WiredEnvironmentComposer(
                    _clickUserTriggers.Count != 0, EnabledAchievements()));
            }
        }
    }

    /// <summary>
    /// The Share add-on's Synchronize button (header 397, VariableAddonShareAddon.as:31, write permission only): the add-ons of the shared
    /// variables stacked with it are published to their references. Observed on AIR WIN63-202610201032-107964540.
    /// </summary>
    public bool SynchronizeShare(Plus.HabboHotel.GameClients.GameClient session, uint itemId)
    {
        return ReferenceEquals(session.GetHabbo()?.CurrentRoom, _room) && Settings.CanModify(session)
            && _room.GetRoomItemHandler().GetItem(itemId) is { } item && Variables.Synchronize(item);
    }

    /// <summary>
    /// A permanent or shared variable box (availability 10 or 11): hhus asks before picking it up, since its stored values go with it
    /// (r90, wf_var_user; in-room variables were picked up directly).
    /// </summary>
    public bool StoresVariableData(Item item) => item.Definition.WiredDescriptor?.Category == WiredBoxCategory.Variable
        && TryGet(item.Id, out var box) && box is IWiredConfiguredItem
        && Variables.Module.DescribeDefinitions([item.Id]).FirstOrDefault()?.Definition.IsDurable == true;

    internal bool SendLink(Item[] endpoints, WiredLinkPayload payload, WiredEventKind kind) =>
        WiredLinkTransport.Send(_room, endpoints, payload, kind, LinkStore, _room.Rooms);
}
