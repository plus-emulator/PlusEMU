using System.Runtime.CompilerServices;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// The roller speed controller. Observed on AIR WIN63-202610201032-107964540: it has 12 states in four groups of three. A use or a
/// Wired toggle jumps to the first state of the next group (0, 3, 6, 9, 0). On every roller step the furni sends a data update that
/// alternates between the two frames after its group state; the stored state stays the group state. Only a use (or the state it was
/// placed with) sets the roller step time: a Wired toggle changes the state and leaves the step time as it was.
/// </summary>
public sealed class InteractorRollerSpeed : IFurniInteractor
{
    private static readonly ConditionalWeakTable<Item, StrongBox<int>> Steps = new();
    // The group the rollers follow, once a Wired toggle has moved the state away from it.
    private static readonly ConditionalWeakTable<Item, StrongBox<int>> SpeedGroups = new();

    /// <summary>The roller step time the controller sets, in milliseconds.</summary>
    public static int StepMilliseconds(Item controller) =>
        (SpeedGroups.TryGetValue(controller, out var group) ? group.Value : Group(controller)) * 500 + 500;

    private static int Group(Item item) => int.TryParse(item.LegacyDataString, out var state) ? Math.Clamp(state, 0, 11) / 3 : 0;

    public void OnPlace(GameClient? session, Item item) => SpeedGroups.Remove(item);
    public void OnRemove(GameClient? session, Item item) => SpeedGroups.Remove(item);
    public void OnTrigger(GameClient? session, Item item, int request, bool hasRights)
    {
        if (session != null && hasRights) {
            NextGroup(item);
            SpeedGroups.Remove(item);
        }
    }
    public void OnWiredTrigger(Item item)
    {
        SpeedGroups.GetValue(item, _ => new(Group(item)));
        NextGroup(item);
    }
    public static void AnimateStep(Room room, Item item)
    {
        if (!int.TryParse(item.LegacyDataString, out var state)) {
            return;
        }

        var step = Steps.GetValue(item, _ => new());
        step.Value++;
        var frame = state / 3 * 3 + 1 + (step.Value % 2 == 0 ? 1 : 0);
        room.SendObjectData(item, frame.ToString(), false);
    }
    private static void NextGroup(Item item)
    {
        var state = int.TryParse(item.LegacyDataString, out var current) ? Math.Clamp(current, 0, 11) : 0;
        item.LegacyDataString = ((state / 3 + 1) % 4 * 3).ToString();
        Steps.GetValue(item, _ => new()).Value = 0;
        item.UpdateUsedState();
    }
}
