namespace Plus.HabboHotel.Users;

public class HabboStats
{
    public HabboStats(int roomVisits, double onlineTime, int respect, int respectGiven, int dailyRespectPoints, int dailyPetRespectPoints, int achievementPoints,
        int questId, int groupId, string respectsTimestamp)
    {
        RoomVisits = roomVisits;
        OnlineTime = onlineTime;
        Respect = respect;
        RespectGiven = respectGiven;
        DailyRespectPoints = dailyRespectPoints;
        DailyPetRespectPoints = dailyPetRespectPoints;
        AchievementPoints = achievementPoints;
        QuestId = questId;
        FavouriteGroupId = groupId;
        RespectsTimestamp = respectsTimestamp;
    }

    public int RoomVisits { get; set; }
    public double OnlineTime { get; set; }
    public int Respect { get; set; }
    public int RespectGiven { get; set; }
    public int DailyRespectPoints { get; set; }
    public int DailyPetRespectPoints { get; set; }
    public int AchievementPoints { get; set; }
    public int QuestId { get; set; }
    public int FavouriteGroupId { get; set; }
    public string RespectsTimestamp { get; set; }
}
