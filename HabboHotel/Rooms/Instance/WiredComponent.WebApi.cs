using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.WebApi;

namespace Plus.HabboHotel.Rooms.Instance;

/// <summary>Keys of the room's Variables Web API add-ons.</summary>
public partial class WiredComponent
{
    private IWiredWebApiStore? _webApiStore;

    public IWiredWebApiStore WebApiStore
    {
        get => _webApiStore ??= new DatabaseWiredWebApiStore(_database);
        internal set => _webApiStore = value;
    }

    private bool IsWebApi(uint itemId, out IWiredConfiguredItem box)
    {
        box = null!;

        return TryGet(itemId, out var found) && found is IWiredConfiguredItem { Descriptor.CanonicalName: "wf_xtra_var_web_api" } configured
            && ReferenceEquals(_room.GetRoomItemHandler().GetItem(itemId), configured.Item) && (box = configured) != null;
    }

    /// <summary>The editor's generate button: a new random key, live at once, for an editor of the room.</summary>
    public bool IssueWebApiKey(GameClient session, uint itemId, bool read, out string key)
    {
        key = "";

        if (!ReferenceEquals(session.GetHabbo()?.CurrentRoom, _room) || !Settings.CanModify(session) || !IsWebApi(itemId, out _)) {
            return false;
        }

        key = WiredWebApiKeyFormat.Create();
        WebApiStore.Issue(itemId, _room.Id, read, key);

        return true;
    }

    /// <summary>A save keeps no key, or the keys the server made last for this add-on.</summary>
    internal bool WebApiKeysAccepted(uint itemId, string text)
    {
        var keys = text.Split('\t');

        if (text.Length == 0) {
            return true;
        }

        if (keys.Length != 2) {
            return false;
        }

        var (read, write) = WebApiStore.Hashes(itemId);

        return (keys[0].Length == 0 || DatabaseWiredWebApiStore.Hash(keys[0]) == read)
            && (keys[1].Length == 0 || DatabaseWiredWebApiStore.Hash(keys[1]) == write);
    }

    private void WebApiSaved(IWiredConfiguredItem box)
    {
        var keys = box.Configuration.Text.Split('\t');
        WebApiStore.Saved(box.Item.Id, _room.Id, keys[0], keys.Length > 1 ? keys[1] : "", box.Configuration.IntParams is [1, ..]);
    }
}
