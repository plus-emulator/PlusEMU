using System.Data.Common;
using Microsoft.Extensions.Logging;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.GameClients;
using Plus.Utilities.DependencyInjection;

namespace Plus.HabboHotel.Habbicons;

[Singleton]
public interface IHabbiconAchievementService
{
    void Reconcile(GameClient session, HabbiconSnapshot snapshot);
    void Used(GameClient session);
}

public sealed class HabbiconAchievementService(IAchievementManager achievements,
    ILogger<HabbiconAchievementService>? logger = null) : IHabbiconAchievementService
{
    public void Reconcile(GameClient session, HabbiconSnapshot snapshot)
    {
        Progress(session, "ACH_HabbiconCollected", snapshot.Items.Values.Count(item => item.Owned), absolute: true);
        Progress(session, "ACH_HabbiconCollectionComp", snapshot.Collections.Count(collection => collection.Completed), absolute: true);
    }

    public void Used(GameClient session) => Progress(session, "ACH_HabbiconUsed", 1);

    private void Progress(GameClient session, string group, int amount, bool absolute = false)
    {
        try {
            achievements.ProgressAchievement(session, group, amount, fromBeginning: absolute);
        }
        catch (DbException exception) {
            logger?.LogError(exception, "Unable to update Habbicon achievement {Group} for user {UserId}", group, session.GetHabbo().Id);
        }
    }
}
