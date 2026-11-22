using Dapper;
using Plus.Communication.Packets.Outgoing.Quests;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Quests;

public interface IQuestProgressStore
{
    bool TryStart(int userId, Quest quest, out int progress);
    void Cancel(int userId, Quest quest);
    bool SaveProgress(int userId, Quest quest, int progress, bool completed);
}

public sealed class QuestProgressStore(IDatabase database) : IQuestProgressStore
{
    public bool TryStart(int userId, Quest quest, out int progress)
    {
        var questId = quest.Id;
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var isNew = connection.Execute("INSERT IGNORE INTO user_quests (user_id,quest_id) VALUES (@userId,@questId)", new { userId, questId }, transaction) == 1;
        progress = isNew ? 0 : connection.QuerySingle<int?>("SELECT progress FROM user_quests WHERE user_id=@userId AND quest_id=@questId FOR UPDATE",
            new { userId, questId }, transaction) ?? 0;

        // A completed quest stays completed, so its reward is paid only once.
        if (!isNew && quest.IsCompleted(progress)) {
            return false;
        }

        if (connection.Execute("UPDATE user_statistics SET quest_id=@questId WHERE id=@userId LIMIT 1", new { userId, questId }, transaction) != 1) {
            throw new InvalidOperationException("Quest state was not persisted.");
        }

        transaction.Commit();

        return true;
    }

    public void Cancel(int userId, Quest quest)
    {
        var questId = quest.Id;
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        connection.Execute("DELETE FROM user_quests WHERE user_id=@userId AND quest_id=@questId", new { userId, questId }, transaction);

        if (connection.Execute("UPDATE user_statistics SET quest_id=0 WHERE id=@userId LIMIT 1", new { userId }, transaction) != 1) {
            throw new InvalidOperationException("Quest cancellation was not persisted.");
        }

        transaction.Commit();
    }

    public bool SaveProgress(int userId, Quest quest, int progress, bool completed)
    {
        var questId = quest.Id;
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (connection.Execute("UPDATE user_quests SET progress=@progress WHERE user_id=@userId AND quest_id=@questId LIMIT 1",
                new { userId, questId, progress }, transaction) != 1) {
            throw new InvalidOperationException("Quest progress was not persisted.");
        }

        if (completed && connection.Execute("UPDATE user_statistics SET quest_id=0 WHERE id=@userId LIMIT 1", new { userId }, transaction) != 1) {
            throw new InvalidOperationException("Quest completion was not persisted.");
        }

        transaction.Commit();

        return true;
    }
}

public interface IQuestProgressService
{
    void Start(GameClient session, int questId);
    void Cancel(GameClient session);
    void StartNext(GameClient session);
}

public sealed class QuestProgressService(IQuestProgressStore store, IQuestManager quests) : IQuestProgressService
{
    public void Start(GameClient session, int questId)
    {
        var quest = quests.GetQuest(questId);

        if (quest == null) {
            return;
        }

        PublishStarted(session, quest);
    }

    public void Cancel(GameClient session)
    {
        var habbo = session.GetHabbo();

        if (habbo.HabboStats is not { } stats || quests.GetQuest(stats.QuestId) is not { } quest) {
            return;
        }

        store.Cancel(habbo.Id, quest);
        stats.QuestId = 0;
        session.Send(new QuestAbortedComposer(QuestWireDataFactory.Create(session, quest, quests.GetAmountOfQuestsInCategory(quest.Category))));
        quests.GetList(session, null);
    }

    public void StartNext(GameClient session)
    {
        var habbo = session.GetHabbo();

        if (!habbo.InRoom) {
            return;
        }

        var completed = quests.GetQuest(habbo.QuestLastCompleted);

        if (completed == null) {
            return;
        }

        var next = quests.GetNextQuestInSeries(completed.Category, completed.Number + 1);

        if (next == null) {
            return;
        }

        PublishStarted(session, next);
    }

    private void PublishStarted(GameClient session, Quest quest)
    {
        var habbo = session.GetHabbo();

        // The active quest lives in the loaded statistics; without them the start would not be tracked.
        if (habbo.HabboStats is not { } stats) {
            return;
        }

        var started = store.TryStart(habbo.Id, quest, out var progress);
        habbo.Quests[quest.Id] = progress;

        if (!started) {
            quests.GetList(session, null);

            return;
        }

        stats.QuestId = quest.Id;
        quests.GetList(session, null);
        session.Send(new QuestStartedComposer(QuestWireDataFactory.Create(session, quest, quests.GetAmountOfQuestsInCategory(quest.Category))));
    }
}
