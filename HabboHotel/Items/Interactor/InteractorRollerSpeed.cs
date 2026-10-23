using System.Runtime.CompilerServices;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// The roller speed controller. It has 12 states in four groups of three. A use or a Wired toggle jumps to the first state of the
/// next group (0, 3, 6, 9, 0). On every roller step the furni sends a data update that alternates between the two frames after its
/// group state; the stored state stays the group state. Only a use (or the state it was placed with) sets the roller step time:
/// a Wired toggle changes the state and leaves the step time as it was.
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
    /// <summary>The state a Wired Toggle State writes. The rollers keep the step time of the group they had.</summary>
    public static string WiredToggle(Item item, string current)
    {
        SpeedGroups.GetValue(item, _ => new(Group(item)));
        Steps.GetValue(item, _ => new()).Value = 0;

        return NextGroupState(current);
    }
    public static void AnimateStep(Room room, Item item)
    {
        // A controller fresh from the inventory holds no state; it shows the frames of the first group.
        var state = int.TryParse(item.LegacyDataString, out var stored) ? Math.Clamp(stored, 0, 11) : 0;
        var step = Steps.GetValue(item, _ => new());
        step.Value++;
        var frame = state / 3 * 3 + 1 + (step.Value % 2 == 0 ? 1 : 0);
        room.SendObjectData(item, frame.ToString(), false);
    }
    private static void NextGroup(Item item)
    {
        item.LegacyDataString = NextGroupState(item.LegacyDataString);
        Steps.GetValue(item, _ => new()).Value = 0;
        item.UpdateUsedState();
    }
    private static string NextGroupState(string current)
    {
        var state = int.TryParse(current, out var parsed) ? Math.Clamp(parsed, 0, 11) : 0;

        return ((state / 3 + 1) % 4 * 3).ToString();
    }
}
