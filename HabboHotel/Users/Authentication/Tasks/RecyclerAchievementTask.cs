using Plus.HabboHotel.Recycler;

namespace Plus.HabboHotel.Users.Authentication.Tasks;

public sealed class RecyclerAchievementTask(IRecyclerAchievementService recycler) : IAuthenticationTask
{
    public Task UserLoggedIn(Habbo habbo)
    {
        if (habbo.Client is { } session) {
            recycler.Sync(session);
        }

        return Task.CompletedTask;
    }
}
