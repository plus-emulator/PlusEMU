using Plus.HabboHotel.Items.Wired.Variables;

namespace Plus.HabboHotel.Items.Wired.Leaderboard;

/// <summary>
/// Wired context section 8 of the add-on's edit-start message (package_127/VariableLeaderboardContext.as:18-36): the published overlay
/// (0 when unpublished), the stack's tile, how many user variables the stack has and the ones offered, bottom of the stack first.
/// </summary>
public sealed record WiredLeaderboardContext(int OverlayInstanceId, int TileX, int TileY, int TotalCandidateCount,
    IReadOnlyList<WiredLeaderboardChoice> Choices)
{
    public const int ContextType = 8;
}

/// <summary>One published leaderboard overlay as class_2737.fromMessage reads it (L48-83).</summary>
public sealed record WiredLeaderboardDescriptor(int OverlayInstanceId, int QueryRevision, WiredLeaderboardConfig Config,
    IReadOnlyList<WiredLeaderboardOverride> Choices, string DefaultVariableId);

/// <summary>One ranked row as class_3133.fromMessage reads it (L47-62); null level or text connection is "not available".</summary>
public sealed record WiredLeaderboardRow(int Rank, int UserId, string Username, string Figure, string RawValue,
    long CreationTime, long LastUpdateTime, string? Level, string? TextConnection);

/// <summary>Up to 50 rows of one selected variable (class_3018): the client pages through them itself.</summary>
public sealed record WiredLeaderboardChunk(int OverlayInstanceId, int QueryRevision, string VariableId, int ChunkIndex, int TotalCount,
    IReadOnlyList<WiredLeaderboardRow> Rows)
{
    public const int Size = 50;
}
