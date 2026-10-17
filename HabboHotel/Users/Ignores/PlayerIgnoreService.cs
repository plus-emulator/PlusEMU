using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Action;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Users.Authentication;
using Plus.Utilities.DependencyInjection;

namespace Plus.HabboHotel.Users.Ignores;

[Singleton]
public interface IPlayerIgnoreService
{
    Task Ignore(GameClient session, int userId);
    Task Unignore(GameClient session, int userId);
}

public sealed class PlayerIgnoreService(
    IGameClientManager clients,
    IPlayerIgnoreStore store,
    IAchievementManager achievements,
    IAccountSessionGate accounts) : IPlayerIgnoreService
{
    public Task Ignore(GameClient session, int userId) => Change(session, userId, true);

    public Task Unignore(GameClient session, int userId) => Change(session, userId, false);

    private async Task Change(GameClient session, int userId, bool ignored)
    {
        var habbo = session.GetHabbo();
        using var held = await accounts.EnterAsync(habbo.Id);
        var room = habbo.CurrentRoom;

        if (room == null) {
            return;
        }

        var target = clients.GetClientByUserId(userId)?.GetHabbo();

        if (target == null || (ignored && target.Access.Can(PermissionKeys.ModerationTool))) {
            return;
        }

        // Ignores are loaded at login; there is nothing to change on a user without them.
        if (habbo.IgnoresComponent is not { } ignores || ignores.IsIgnored(target.Id) == ignored) {
            return;
        }

        if (habbo.CurrentRoom != room || !await store.SetIgnored(habbo.Id, target.Id, ignored)) {
            return;
        }

        if (ignored) {
            ignores.PublishIgnore(target.Id);
        }
        else {
            ignores.PublishUnignore(target.Id);
        }

        session.Send(new IgnoreStatusComposer(ignored ? IgnoreStatus.Added : IgnoreStatus.Removed, target.Id));

        if (ignored) {
            achievements.ProgressAchievement(session, "ACH_SelfModIgnoreSeen", 1);
        }
    }
}
