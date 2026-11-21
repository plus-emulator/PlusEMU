using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.UserData;

namespace Plus.HabboHotel.Moderation;

internal class BanLoginCheckTask : IAuthenticationTask
{
    private readonly IModerationManager _moderationManager;
    private readonly IUserDataFactory _userDataFactory;

    public BanLoginCheckTask(IModerationManager moderationManager, IUserDataFactory userDataFactory)
    {
        _moderationManager = moderationManager;
        _userDataFactory = userDataFactory;
    }

    public async Task<bool> CanLogin(GameClient session, int userId)
    {
        var username = await _userDataFactory.GetUsernameForHabboById(userId);

        if (string.IsNullOrWhiteSpace(username)) {
            return false;
        }

#pragma warning disable CS0618 // The handshake's machine id only lives on the session.
        if (_moderationManager.HasBan(ModerationBanType.Username, username) || _moderationManager.HasBan(ModerationBanType.Machine, session.MachineId)) {
            return false;
        }
#pragma warning restore CS0618

        // An IP ban holds the address as text, as users.ip_last records it.
        return session.RemoteAddress is not { IsVisitor: true } visitor || !_moderationManager.HasBan(ModerationBanType.Ip, visitor.Address.ToString());
    }
}
