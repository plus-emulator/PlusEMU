namespace Plus.HabboHotel.Items.Wired.Variables;

// These are Volt's scalar editor codes, not the unrelated array target codes.
public enum WiredVariableTarget
{
    User = 0, Furni = 1, Context = 2, Global = 3
}
public enum WiredVariableAvailability
{
    UserActive = 0, RoomActive = 1, Persistent = 10, Shared = 11
}
public enum WiredVariableMutation
{
    Give, Replace, Set, Remove
}
public enum WiredVariableChangeKind
{
    Created, Updated, Removed
}

public sealed record WiredVariableReference(WiredVariableTarget Target, string Token);
public sealed record WiredVariableLink(uint SourceRoomId, WiredVariableReference Source, bool ReadOnly);

public sealed record WiredVariableDefinition(uint ItemId, uint RoomId, uint OwnerId, string Name,
    WiredVariableTarget Target, WiredVariableAvailability Availability, bool HasValue, long InitialValue = 0,
    WiredVariableLink? Link = null, string? PointerAccessor = null)
{
    public string Token => $"custom:{ItemId}";
    /// <summary>A pointer variable: the token of the context variable whose value names the variable it stands for.</summary>
    public bool IsPointer => PointerAccessor is not null;
    /// <summary>
    /// A Variable Changed trigger never sees a change of a context variable, so the catalog lists it without interception.
    /// </summary>
    public bool InterceptsChanges => Target != WiredVariableTarget.Context;
    public bool IsDurable => Availability is WiredVariableAvailability.Persistent or WiredVariableAvailability.Shared;
}

/// <summary>The directory must report current item placement and room ownership, including unloaded rooms.</summary>
public interface IWiredVariableDirectory
{
    WiredVariableDefinition? Find(uint itemId);
    uint? GetRoomOwner(uint roomId);

    /// <summary>Variables other rooms of this owner currently share; directories that cannot enumerate share nothing.</summary>
    IReadOnlyList<WiredSharedVariable> ListShared(uint ownerId, uint exceptRoomId) => [];

    /// <summary>The variables defined in one room, for rules that compare names; directories that cannot enumerate list none.</summary>
    IReadOnlyList<WiredVariableDefinition> ListRoom(uint roomId) => [];

    /// <summary>The reference variables, in any room, that read one shared variable; directories that cannot enumerate list none.</summary>
    IReadOnlyList<WiredVariableDefinition> ListReferences(uint sourceRoomId, uint sourceItemId) => [];
}

public sealed record WiredSharedVariable(uint RoomId, string RoomName, WiredVariableDefinition Definition);
