using Plus.HabboHotel.GameClients;
using Plus.Utilities.DependencyInjection;

namespace Plus.HabboHotel.Users.Authentication;

[Singleton]
public interface IAuthenticationTask
{
    /// <summary>
    /// Add additional login checking for a given user.
    /// </summary>
    /// <param name="session">The connection the user is logging in on.</param>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<bool> CanLogin(GameClient session, int userId) => Task.FromResult(true);

    /// <summary>
    /// Raised when the user is logged into the hotel.
    /// </summary>
    /// <param name="habbo"></param>
    /// <returns></returns>
    Task UserLoggedIn(Habbo habbo) => Task.CompletedTask;

    /// <summary>
    /// Raised while the user is disconnected from the hotel, before the user is saved.
    /// </summary>
    /// <param name="habbo"></param>
    void UserLoggedOut(Habbo habbo) { }
}
