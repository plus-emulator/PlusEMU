namespace Plus.HabboHotel.Rooms.PathFinding;

public sealed class ActorProfile
{
    public bool LegacyOverride { get; set; }
    public bool IgnoreStepHeight { get; set; }
    public bool IgnoreUsers { get; set; }
    // Plus's inverted RoomBlockingEnabled: true means walkthrough.
    public bool Walkthrough { get; set; }
    public bool DiagonalEnabled { get; set; } = true;
    // The room's :disablediagonal setting: a diagonal step may pass between two blocked tiles that
    // touch at a corner (furniture squeeze). Void flanks still follow the global corner rule.
    public bool DiagonalSqueeze { get; set; }
    public ActorAccess Access { get; } = new();
    public int CapabilityVersion => Access.CapabilityVersion;
    public bool IsMember(int group) => Access.IsMember(group);
    public void SetMembership(int group, bool member) => Access.SetMembership(group, member);
    public InteractionAuthorization? Interaction { get; set; }
}

public readonly record struct InteractionAuthorization(int FromX, int FromY, int ToX, int ToY)
{
    public bool Allows(in NavPosition from, in NavPosition to) => from.X == FromX && from.Y == FromY && to.X == ToX && to.Y == ToY;
}
