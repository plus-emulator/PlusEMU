using Plus.HabboHotel.Items.Wired.Runtime;

namespace Plus.HabboHotel.Rooms.Instance;

public partial class WiredComponent
{
    // Click User runs its stacks at once, like speech: their flashes go out with the click.
    public WiredClickResult DispatchClickUser(RoomUser actor, RoomUser target)
    {
        var result = _engine.DispatchClickUser(actor, target);
        FlushFlashes();

        return result;
    }
}
