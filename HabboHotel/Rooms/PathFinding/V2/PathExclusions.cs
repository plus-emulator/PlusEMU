namespace Plus.HabboHotel.Rooms.PathFinding;

public sealed class PathExclusions
{
    private readonly HashSet<SurfaceRef> _surfaces = new();
    private readonly HashSet<Transition> _transitions = new();
    public int Count => _surfaces.Count + _transitions.Count;
    public bool Block(SurfaceRef target) => _surfaces.Add(target);
    public bool Block(NavPosition from, SurfaceRef target) => _transitions.Add(new(from.X, from.Y, from.Z, target));
    public bool Allows(SurfaceRef target) => !_surfaces.Contains(target);
    public bool Allows(NavPosition from, SurfaceRef target)
        => Allows(target) && !_transitions.Contains(new(from.X, from.Y, from.Z, target));

    private readonly record struct Transition(int X, int Y, double Z, SurfaceRef Target);
}
