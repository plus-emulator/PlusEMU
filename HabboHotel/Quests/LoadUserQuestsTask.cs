using Plus.Database;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.UserData;

namespace Plus.HabboHotel.Quests;

internal sealed class LoadUserQuestsTask(IDatabase database) : IUserDataLoadingTask
{
    public Task Load(Habbo habbo) => Task.CompletedTask;
}
