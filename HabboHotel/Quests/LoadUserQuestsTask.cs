using Dapper;
using Plus.Database;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.UserData;

namespace Plus.HabboHotel.Quests;

internal sealed class LoadUserQuestsTask(IDatabase database) : IUserDataLoadingTask
{
    public async Task Load(Habbo habbo)
    {
        using var connection = database.Connection();
        var rows = await connection.QueryAsync<QuestProgressRow>(
            "SELECT quest_id AS QuestId, COALESCE(progress,0) AS Progress FROM user_quests WHERE user_id=@userId",
            new { userId = habbo.Id });

        foreach (var row in rows) {
            habbo.Quests[row.QuestId] = row.Progress;
        }
    }

    private sealed class QuestProgressRow
    {
        public int QuestId { get; set; }
        public int Progress { get; set; }
    }
}
