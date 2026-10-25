using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Items.Wired.Variables;

namespace Plus.HabboHotel.Items.Wired.Runtime;

public enum WiredEventKind
{
    Enter, Speech, WalkOn, WalkOff, Use, StateChanged, GameStart, GameEnd, Collision,
    Score, AvatarAction, Leave, ClickFurni, ClickTile, ClickUser, BotReachedFurni,
    BotReachedUser, Counter, Signal, Variable, Periodic, Elapsed, TransactionComplete, TransactionFail, Inspection,
    RoomInitialized, LinkMessage, LinkDataframe
}

public sealed record WiredRuntimeEvent(WiredEventKind Kind)
{
    public RoomUser? Actor { get; init; }
    public Item? EventItem { get; init; }
    /// <summary>The leaver of the Leaves Room stack whose Send Signal raised this signal while the leaver's variables were readable.</summary>
    public RoomUser? SignalLeaver { get; init; }
    /// <summary>The contract (or the Initiate Transaction box of a custom contract) a transaction event is about, even once its furni has left the room.</summary>
    public uint SourceId { get; init; }
    public RoomUser? TargetUser { get; init; }
    public string Message { get; init; } = "";
    public int ChatType { get; init; }
    public int ChatStyle { get; init; }
    public int Action { get; init; }
    public int Code { get; init; }
    /// <summary>A transaction failure caused by a Cancel Transaction effect.</summary>
    public bool CancelledByWired { get; init; }
    public WiredVariableChange? VariableChange { get; init; }
    public long PreviousValue { get; init; }
    public long Value { get; init; }
    public Plus.HabboHotel.Items.Wired.Chests.WiredChestFigures? Transaction { get; init; }
    public int Team { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    /// <summary>A link message or dataframe received by <see cref="EventItem"/>, a WIRED Link endpoint of this room.</summary>
    public Plus.HabboHotel.Items.Wired.Links.WiredLinkPayload? Link { get; init; }
    /// <summary>The room's <see cref="RoomUserManager.Visits"/> when the event happened; a user whose visit started later did not see it.</summary>
    public int Visits { get; init; } = int.MaxValue;
}

public readonly record struct WiredClickResult(bool Triggered, bool BlockMenu, bool DoNotRotate);
