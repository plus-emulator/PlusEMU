using System.Data;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Badges;

namespace Plus.HabboHotel.Badges;

public interface IBadgeManager
{
    Task Init();
    Task GiveBadge(Habbo habbo, string code);

    // Checks and writes the badge row inside the caller's transaction; false when the user cannot be given the badge.
    bool StoreBadge(IDbConnection connection, IDbTransaction transaction, Habbo habbo, string code);

    // Adds a stored badge to the loaded inventory and tells the client.
    void PublishBadge(Habbo habbo, string code);

    Task RemoveBadge(Habbo habbo, string badge);
    Task<bool> ReplaceBadge(Habbo habbo, string? previous, string code);
    Task<List<Badge>> LoadBadgesForHabbo(int userId);
    IReadOnlyDictionary<string, BadgeDefinition> Badges { get; }
}
