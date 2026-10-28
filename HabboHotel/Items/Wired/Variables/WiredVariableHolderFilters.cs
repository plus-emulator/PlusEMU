namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>
/// The filters of a holders page request, as the AIR client sends them: sort 0 highest value, 1 lowest value, 2 and 3 oldest and newest
/// creation, 4 and 5 oldest and newest update; user type -1 all, 1 Habbo, 2 pet, 4 bot.
/// </summary>
internal static class WiredVariableHolderFilters
{
    public const int AllTypes = -1, Habbo = 1;

    public static bool IsValid(int sort, int userType) => sort is >= 0 and <= 5 && userType is AllTypes or Habbo or 2 or 4;

    public static int PagingSort(int sort) => sort switch
    {
        1 => WiredVariablePaging.ValueAscending,
        2 => WiredVariablePaging.CreatedAscending,
        3 => WiredVariablePaging.CreatedDescending,
        4 => WiredVariablePaging.UpdatedAscending,
        5 => WiredVariablePaging.UpdatedDescending,
        _ => WiredVariablePaging.ValueDescending
    };
}
