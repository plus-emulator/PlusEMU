using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Instance;

/// <summary>
/// Transparency and highlight that Set Furni Transparency / Set Furni Highlight showed to the whole room. A user entering the room is
/// sent the current state (class_3560 furni, class_3459 users) so late joiners see what everyone else sees. Effects shown to an
/// audience of chosen users are theirs alone and are not replayed.
/// </summary>
public partial class WiredComponent
{
    private readonly object _visualizationGate = new();
    private readonly Dictionary<int, WiredTransparencyState> _furniTransparency = [];
    private readonly Dictionary<int, WiredHighlightState> _furniHighlight = [];
    private readonly Dictionary<int, WiredTransparencyState> _userTransparency = [];
    private readonly Dictionary<int, WiredHighlightState> _userHighlight = [];

    // Observed on AIR WIN63-202610201032-107964540: a furni made visible again and a highlight at strength 0 stay as entries; only
    // picking the furni up removes them.
    internal void RememberTransparency(bool users, IEnumerable<int> ids, int transparency, bool clickThrough, int easing)
    {
        lock (_visualizationGate) {
            var states = users ? _userTransparency : _furniTransparency;

            foreach (var id in ids) {
                states[id] = new(id, transparency, clickThrough, easing);
            }
        }
    }

    internal void RememberHighlight(bool users, IEnumerable<int> ids, int type, int strength, int color, int animation, int easing)
    {
        lock (_visualizationGate) {
            var states = users ? _userHighlight : _furniHighlight;

            foreach (var id in ids) {
                states[id] = new(id, type, strength, color, animation, easing);
            }
        }
    }

    private void ForgetVisualization(bool users, int id)
    {
        lock (_visualizationGate) {
            (users ? _userTransparency : _furniTransparency).Remove(id);
            (users ? _userHighlight : _furniHighlight).Remove(id);
        }
    }

    /// <summary>The room-wide visual state, sent on room entry; nothing is sent while there is none.</summary>
    public void SendVisualizationState(GameClient session)
    {
        WiredVisualizationStateComposer? furni = null, users = null;

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
