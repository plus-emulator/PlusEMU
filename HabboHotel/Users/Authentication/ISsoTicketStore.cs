namespace Plus.HabboHotel.Users.Authentication;

/// <summary>
/// Game login tickets kept in users.auth_ticket. A ticket expires at auth_ticket_expires_at,
/// matches case-exactly and can log in once.
/// </summary>
public interface ISsoTicketStore
{
    /// <summary>Uses up a live ticket. Of several concurrent callers only one gets the user id.</summary>
    Task<int?> Consume(string ticket);

    /// <summary>Gives the user a new ticket that expires after <paramref name="lifetime"/>, replacing any ticket they
    /// still hold, the way the website issues one for the client. Null when the user does not exist.</summary>
    Task<string?> Issue(int userId, TimeSpan lifetime);

    /// <summary>Clears whatever ticket the user still holds.</summary>
    Task Revoke(int userId, CancellationToken cancellationToken = default);
}
