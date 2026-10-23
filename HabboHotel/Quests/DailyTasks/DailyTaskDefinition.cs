using System.Collections.Immutable;

namespace Plus.HabboHotel.Quests.DailyTasks;

/// <summary>
/// The status byte of a daily task: 0 in progress, 1 completed and claimable, 2 claimed.
/// </summary>
public enum DailyTaskStatus : byte
{
    InProgress = 0,
    Completed = 1,
    Claimed = 2
}

/// <summary>
/// One reward of a daily task as the client reads it. The client draws it with its product icon widget, keyed on <see cref="ProductItemTypeId"/>:
/// 8 is a currency whose type is <see cref="RewardTypeId"/> (-1 credits, 0 duckets, 5 diamonds), 4 a badge whose code
/// is <see cref="RewardTypeId"/>, 1 a floor and 0 a wall furni whose class id is <see cref="RewardTypeId"/>.
/// </summary>
public sealed record DailyTaskReward(short ProductItemTypeId, string RewardTypeId, string ExtraParams, int Amount)
{
    public const short WallItem = 0;
    public const short FloorItem = 1;
    public const short Badge = 4;
    public const short Currency = 8;
    public const int Credits = -1;
}

/// <summary>
/// A task the server can hand out. <see cref="Code"/> names the texts (dailytask.&lt;code&gt;.name/.desc/.hint) and the image
/// (&lt;image.library.dailytasks.url&gt;&lt;code&gt;&lt;image version&gt;.png).
/// <see cref="ActionType"/> is the progress action that advances it; a non-empty <see cref="Parameters"/> narrows it to
/// those action details (for example hand item ids).
/// </summary>
public sealed record DailyTaskDefinition(
    int Id,
    string Code,
    string ActionType,
    ImmutableHashSet<string> Parameters,
    string QuestTypeCode,
    int RequiredRepeats,
    string ImageVersion,
    string CatalogName,
    bool Bonus,
    int Weight,
    ImmutableArray<DailyTaskReward> Rewards)
{
    public bool Matches(string actionType, string? parameter) =>
        string.Equals(ActionType, actionType, StringComparison.Ordinal)
        && (Parameters.IsEmpty || parameter != null && Parameters.Contains(parameter));

    public static ImmutableHashSet<string> ParseParameters(string? text) =>
        (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToImmutableHashSet(StringComparer.Ordinal);
}

/// <summary>One task handed to one user for one day. <see cref="Id"/> is the task id the client sees and claims.</summary>
public sealed class UserDailyTask(long id, int definitionId, DateOnly day, DateTimeOffset expiresAt, int progress, DailyTaskStatus status)
{
    public long Id { get; } = id;
    public int DefinitionId { get; } = definitionId;
    public DateOnly Day { get; } = day;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public int Progress { get; set; } = progress;
    public DailyTaskStatus Status { get; set; } = status;
}

/// <summary>A task as the three daily task packets write it.</summary>
public sealed record DailyTaskWire(
    long TaskId,
    string TaskCode,
    string QuestTypeCode,
    bool IsBonus,
    string ImageVersion,
    string CatalogName,
    int RequiredRepeats,
    int Repeats,
    DailyTaskStatus Status,
    int SecondsLeft,
    ImmutableArray<DailyTaskReward> Rewards);
