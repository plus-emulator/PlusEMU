using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Rooms.Instance;

/// <summary>
/// "Apply Furni to set conditions" (class_3027 [box id], UserDefinedRoomEventsCtrl.applySnapshot, AIR WIN63-202610131318-860371703):
/// every furni in the box's saved snapshot goes back to its recorded state, rotation, position and height. The saved snapshot is
/// never re-captured; furni that left the room or were replaced are skipped, and so is a floor move the tile does not allow.
/// </summary>
public partial class WiredComponent
{
    /// <summary>The boxes whose editor shows the button (hasStateSnapshot) and whose saves capture snapshots.</summary>
    internal static readonly IReadOnlySet<string> SnapshotBoxes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "wf_act_match_to_sshot", "wf_act_place_furni", "wf_trg_stuff_state", "wf_trg_state_changed",
        "wf_cnd_match_snapshot", "wf_cnd_not_match_snap"
    };

    public bool ApplySnapshot(GameClient session, uint boxId)
    {
        if (!ReferenceEquals(session.GetHabbo()?.CurrentRoom, _room) || !Settings.CanModify(session)
            || !TryGet(boxId, out var box) || box is not IWiredConfiguredItem configured
            || !SnapshotBoxes.Contains(configured.Descriptor.CanonicalName)) {
            return false;
        }

        var actor = FurnitureStateEvents.Actor(_room, session);
        var handler = _room.GetRoomItemHandler();
        var changed = false;

        foreach (var snapshot in configured.Configuration.Snapshots) {
            if (handler.GetItem(snapshot.ItemId) is not { IsTemporary: false } item || item.Definition.Id != snapshot.DefinitionId) {
                continue;
            }

            if (item.IsWallItem) {
                // Walls keep their raw recorded placement, as the match action with position and altitude on.
                changed |= snapshot.Wall is { } wall && wall.IsWithinLimits() && handler.MoveWallItem(item, wall.ToString());

                continue;
            }

            if (!item.IsFloorItem || snapshot.Wall != null) {
                continue;
            }

            changed |= RestoreState(item, snapshot.StateFor(item.Definition), actor);
            changed |= WiredRoomOperations.MoveItem(_room, item, snapshot.X, snapshot.Y, snapshot.Rotation, snapshot.Z, animate: false);
        }

        return changed;
    }

    // The same sequenced write the match action makes; the state change is reported with the user who applied it.
    private bool RestoreState(Item item, string state, RoomUser? actor) =>
        GateTransitionService.ToggleState(item, current => string.Equals(current ?? string.Empty, state, StringComparison.Ordinal) ? null : state,
            GateCloseReason.Wired, afterWrite: written => FurnitureStateEvents.PublishFollowed(_room, actor, written))
        is GateTransition.Applied or GateTransition.Queued;
}
