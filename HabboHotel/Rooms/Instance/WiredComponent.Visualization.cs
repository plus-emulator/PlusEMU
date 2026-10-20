using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Instance;

/// <summary>
/// Transparency and highlight that Set Furni Visibility / Set Furni Highlight showed the whole room on furni. A user entering the room
/// is sent the current state (class_3560) so late joiners see what everyone else sees. Effects shown to an audience of chosen users and
/// effects on users are not replayed.
/// </summary>
public partial class WiredComponent
{
    private readonly object _visualizationGate = new();
    private readonly Dictionary<int, WiredTransparencyState> _furniTransparency = [];
    private readonly Dictionary<int, WiredHighlightState> _furniHighlight = [];

    // Observed on AIR WIN63-202610201032-107964540: a furni made visible again and a highlight at strength 0 stay as entries; only
    // picking the furni up removes them.
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

        lock (_visualizationGate) {
            if (_furniTransparency.Count + _furniHighlight.Count > 0) {
                furni = new(false, [.. _furniTransparency.Values], [.. _furniHighlight.Values]);
            }
        }

        if (furni != null) {
            session.Send(furni);
        }
    }
}
