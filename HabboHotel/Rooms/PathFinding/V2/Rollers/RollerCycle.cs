using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms.PathFinding;

// One v2 roller cycle (§14.9): snapshot every load, plan transport groups, commit each group.
internal sealed class RollerCycle
{
    private readonly RollerTransport _transport;
    private readonly RollerLoadBuilder _loads;
    private readonly RollerTransportPlanner _planner;
    private readonly TransportGroupCommitter _committer;

    internal RollerCycle(Room room, RollerTransport transport)
    {
        _transport = transport;
        var admission = new RollerAdmission(room, transport);
        _loads = new(room, transport);
        _planner = new(new(admission), new());
        _committer = new(room, transport, new(room, transport, admission));
    }

    internal void Run(IEnumerable<Item> rollers)
    {
        try {
            foreach (var group in _planner.Plan(_loads.Build(rollers))) {
                _committer.TryCommit(group);
            }
        }
        finally {
            _transport.ReleaseClaims();
        }
    }
}
