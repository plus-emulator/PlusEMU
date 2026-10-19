namespace Plus.HabboHotel.Rooms.PathFinding;

public sealed class PathExclusions
{
    private readonly HashSet<SurfaceRef> _surfaces = new();
    private readonly HashSet<Transition> _transitions = new();
    private readonly HashSet<SurfaceRef> _occupied = new();
    public int Count => _surfaces.Count + _transitions.Count + _occupied.Count;
    public bool Block(SurfaceRef target) => _surfaces.Add(target);
    public bool Block(NavPosition from, SurfaceRef target) => _transitions.Add(new(from.X, from.Y, from.Z, target));
    // Observed on AIR `WIN63-202610151745-318700621`: a tile an avatar blocked is usable again once the walker moves on.
    public bool BlockUntilNextStep(SurfaceRef target) => _occupied.Add(target);
    public void NextStep() => _occupied.Clear();
    public bool Allows(SurfaceRef target) => !_surfaces.Contains(target) && !_occupied.Contains(target);
    public bool Allows(NavPosition from, SurfaceRef target)
        => Allows(target) && !_transitions.Contains(new(from.X, from.Y, from.Z, target));

    private readonly record struct Transition(int X, int Y, double Z, SurfaceRef Target);
}
