using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>
/// The Wired context entry of a user, furni or room variable box editor (types 1 to 3): the box's variable as
/// the catalog lists it, then each holder in the room with its value or the room variable's value.
/// A user holder is named by its room index, a furni holder by the id the client sees.
/// </summary>
public sealed record WiredVariableBoxContext(int ContextType, WiredNativeVariable Variable, ImmutableArray<KeyValuePair<int, int>> Holders, int? Value)
{
    public const int Furni = 1, User = 2, Room = 3;
}
