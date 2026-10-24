using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Instance;

/// <summary>
/// Transparency and highlight that Set Furni Visibility / Set Furni Highlight showed the whole room on furni. A user entering the room
/// is sent the current state so late joiners see what everyone else sees. Effects on users and bots are kept per room index and
/// forgotten when that user or bot leaves. Effects shown to an audience of chosen users are not replayed.
/// </summary>
public partial class WiredComponent
{
    private readonly object _visualizationGate = new();
    private readonly Dictionary<int, WiredTransparencyState> _furniTransparency = [];
    private readonly Dictionary<int, WiredHighlightState> _furniHighlight = [];
    private readonly Dictionary<int, WiredTransparencyState> _userTransparency = [];
    private readonly Dictionary<int, WiredHighlightState> _userHighlight = [];

    // A furni made visible again and a highlight at strength 0 stay as entries; only picking the furni up removes them.
    internal void RememberTransparency(IEnumerable<int> ids, int transparency, bool clickThrough, int easing)
    {
        lock (_visualizationGate) {
            foreach (var id in ids) {
                _furniTransparency[id] = new(id, transparency, clickThrough, easing);
            }
        }
    }

    internal void RememberHighlight(IEnumerable<int> ids, int type, int strength, int color, int animation, int easing)
    {
        lock (_visualizationGate) {
            foreach (var id in ids) {
                _furniHighlight[id] = new(id, type, strength, color, animation, easing);
            }
        }
    }

    internal void RememberUserTransparency(IEnumerable<int> indexes, int transparency, bool clickThrough, int easing)
    {
        lock (_visualizationGate) {
            foreach (var index in indexes.Where(IsInRoom)) {
                _userTransparency[index] = new(index, transparency, clickThrough, easing);
            }
        }
    }

    internal void RememberUserHighlight(IEnumerable<int> indexes, int type, int strength, int color, int animation, int easing)
    {
        lock (_visualizationGate) {
            foreach (var index in indexes.Where(IsInRoom)) {
                _userHighlight[index] = new(index, type, strength, color, animation, easing);
            }
        }
    }

    private bool IsInRoom(int index) => _room.GetRoomUserManager().GetRoomUserByVirtualId(index) != null;

    private void ForgetUserVisualization(int index)
    {
        lock (_visualizationGate) {
            _userTransparency.Remove(index);
            _userHighlight.Remove(index);
        }
    }

    private void ForgetVisualization(int id)
    {
        lock (_visualizationGate) {
            _furniTransparency.Remove(id);
            _furniHighlight.Remove(id);
        }
    }

    /// <summary>The room-wide visual state, sent on room entry; nothing is sent while there is none.</summary>
    public void SendVisualizationState(GameClient session)
    {
        WiredVisualizationStateComposer? furni = null;
        WiredVisualizationStateComposer? users = null;

        lock (_visualizationGate) {
            if (_furniTransparency.Count + _furniHighlight.Count > 0) {
                furni = new(false, [.. _furniTransparency.Values], [.. _furniHighlight.Values]);
            }

            if (_userTransparency.Count + _userHighlight.Count > 0) {
                users = new(true, [.. _userTransparency.Values], [.. _userHighlight.Values]);
            }
        }

        if (furni != null) {
            session.Send(furni);
        }

        if (users != null) {
            session.Send(users);
        }
    }
}
