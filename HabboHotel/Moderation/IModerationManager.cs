using System.Diagnostics.CodeAnalysis;
namespace Plus.HabboHotel.Moderation;

public interface IModerationManager
{
    ICollection<string> UserMessagePresets { get; }
    ICollection<string> RoomMessagePresets { get; }
    ICollection<ModerationTicket> GetTickets { get; }
    Dictionary<string, List<ModerationPresetActions>> UserActionPresets { get; }
    void Init();
    void ReCacheBans();
    /// <summary>
    /// Writes the ban, then signs out every account it covers: covered sessions close at once, and under each account's
    /// session gate, while this ban is still in force, its credentials are revoked and any session that attached meanwhile
    /// closes. Work that outlives the deadline continues in the background.
    /// </summary>
    /// <param name="deadline">Shared by every step of one action; defaults to <see cref="ModerationManager.BanBudget"/>.</param>
    Task BanUser(string mod, ModerationBanType type, string banValue, string reason, DateTimeOffset? expiresAt, CancellationToken deadline = default);

    /// <summary>
    /// Bans an account (and counts it), optionally with its recorded address and a device id, as one action on one deadline.
    /// <paramref name="heldUserId"/> names an account whose session gate the caller already holds.
    /// </summary>
    Task BanAccount(string mod, int userId, string username, string reason, DateTimeOffset? expiresAt, CancellationToken deadline = default,
        bool includeAddress = false, string? machineId = null, int heldUserId = 0);

    bool TryAddTicket(ModerationTicket ticket);
    bool TryGetTicket(int ticketId, [NotNullWhen(true)] out ModerationTicket? ticket);
    bool UserHasTickets(int userId);
    ModerationTicket? GetTicketBySenderId(int userId);

    /// <summary>Whether an unexpired ban of this type and value is cached.</summary>
    bool IsBanned(ModerationBanType type, string value, [NotNullWhen(true)] out ModerationBan? ban);

    /// <summary>Whether an unexpired ban of this type and value is cached and its row still exists.</summary>
    bool HasBan(ModerationBanType type, string value);
}
