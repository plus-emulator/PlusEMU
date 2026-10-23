using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Game;
using Plus.Communication.Packets.Outgoing.Inventory.Achievements;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Badges.Rarity;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

// Reads the achievement packets exactly the way the AIR client parses them; a payload with bytes left over or missing fails.
public class AchievementWireTests
{
    private static readonly AchievementSnapshotService Service = new();

    public sealed record Data(int AchievementId, int Level, string Badge, int ScoreAtStart, int ScoreLimit, int RewardAmount, int RewardType,
        int CurrentPoints, bool FinalLevel, string Category, string SubCategory, int LevelCount, int DisplayMethod, short State);

    public sealed record Unlock(int Type, int Level, int BadgeId, string Badge, int Points, int Reward, int RewardType, int BonusPoints,
        int AchievementId, string RemovedBadge, string Category, bool ShowDialog, int OwnerCount, int RarityId);

    [Fact]
    public void AchievementListIsTheAirAchievementDataWithItsCumulativeScoresAndState()
    {
        var habbo = User(("ACH_Ladder", 2, 7), ("ACH_Done", 1, 3));
        var ladder = Achievement("ACH_Ladder", "social", 30, (1, 1), (2, 5), (3, 10), (4, 50));
        var done = Achievement("ACH_Done", "explore", 20, (1, 3));
        done.State = AchievementState.Archived;
        var fresh = Achievement("ACH_Fresh", "identity", 10, (1, 2), (2, 4));

        var payload = Payload(new AchievementsComposer(Service.Capture(habbo, [ladder, done, fresh])));

        var reader = new FlashIncomingPacket { Buffer = payload };
        Assert.Equal(3, reader.ReadInt());
        var entries = Enumerable.Range(0, 3).Select(_ => ReadData(reader)).ToList();
        Assert.Equal("", reader.ReadString());
        Assert.False(reader.HasDataRemaining());
        // Listed in habbo.com order (official id), whatever order the dictionary holds.
        Assert.Equal(new Data(10, 1, "ACH_Fresh1", 0, 2, 10, 0, 0, false, "identity", "", 2, 0, 1), entries[0]);
        Assert.Equal(new Data(20, 1, "ACH_Done1", 0, 3, 10, 0, 3, true, "explore", "", 1, 0, 2), entries[1]);
        // Level 3 runs from the level 2 threshold to its own, so the bar shows (7 - 5) of (10 - 5).
        Assert.Equal(new Data(30, 3, "ACH_Ladder3", 5, 10, 10, 0, 7, false, "social", "", 4, 0, 1), entries[2]);
    }

    [Fact]
    public void ProgressedAndGameListUseTheSameAchievementData()
    {
        var habbo = User(("ACH_Ladder", 0, 0));
        var ladder = Achievement("ACH_Ladder", "games", 30, (1, 1), (2, 5));
        var snapshot = Service.Capture(habbo, [ladder]);
        var expected = new Data(30, 1, "ACH_Ladder1", 0, 1, 10, 0, 0, false, "games", "", 2, 0, 1);

        Assert.Equal(expected, ReadData(Payload(new AchievementProgressedComposer(snapshot[0])), whole: true));

        var game = new FlashIncomingPacket { Buffer = Payload(new GameAchievementListComposer(9, snapshot)) };
        Assert.Equal((9, 1), (game.ReadInt(), game.ReadInt()));
        Assert.Equal(expected, ReadData(game));
        Assert.Equal("", game.ReadString());
        Assert.False(game.HasDataRemaining());
    }

    [Fact]
    public void ProgressBelowTheHeldLevelNeverRunsTheBarBackwards()
    {
        // A total under the start of the level (an absolute value set lower) shows an empty bar.
        var low = Assert.Single(Service.Capture(User(("ACH_Ladder", 2, 1)), [Achievement("ACH_Ladder", "social", 30, (1, 1), (2, 5), (3, 10))]));
        // habbo.com's ACH_RecycledItems lists a level whose threshold is below the one before it.
        var dip = Achievement("ACH_Dip", "social", 31, (1, 10), (2, 5));
        var backwards = Assert.Single(Service.Capture(User(("ACH_Dip", 1, 10)), [dip]));

        Assert.Equal((5, 10, 5), (low.ScoreAtStartOfLevel, low.Requirement, low.Progress));
        Assert.Equal((5, 5, 10), (backwards.ScoreAtStartOfLevel, backwards.Requirement, backwards.Progress));
    }

    [Fact]
    public void HiddenAchievementsAreLeftOutOfTheListAndTheLimits()
    {
        var hidden = Achievement("ACH_BaseJumpWins", "games", 0, (1, 1));
        hidden.Visible = false;
        hidden.OfficialId = null;
        var shown = Achievement("ACH_Shown", "social", 5, (1, 1));
        var habbo = User(("ACH_BaseJumpWins", 1, 1));

        Assert.Equal("ACH_Shown1", Assert.Single(Service.Capture(habbo, [hidden, shown])).Badge);
        Assert.Equal("Shown", Assert.Single(Service.CaptureDefinitions([hidden, shown])).Name);
    }

    [Fact]
    public void BadgePointLimitsNameEveryLevelKeyTheWayAirComposesIt()
    {
        var ladder = Achievement("ACH_Ladder", "social", 30, (2, 5), (1, 1));
        // A one-level badge that is not ACH_<code><level> has no %limit% key to fill.
        var profile = new Achievement { Id = 282, OfficialId = 282, GroupName = "ACH_Profile1", Category = "identity" };
        profile.AddLevel(new AchievementLevel(1, 0, 10, 1, "ACH_Profile1"));

        var reader = new FlashIncomingPacket { Buffer = Payload(new BadgeDefinitionsComposer(Service.CaptureDefinitions([profile, ladder]))) };

        // Count, then per code its levels; the badge key is "ACH_" + code + level.
        Assert.Equal(1, reader.ReadInt());
        Assert.Equal(("Ladder", 2), (reader.ReadString(), reader.ReadInt()));
        Assert.Equal((1, 1, 2, 5), (reader.ReadInt(), reader.ReadInt(), reader.ReadInt(), reader.ReadInt()));
        Assert.False(reader.HasDataRemaining());
        Assert.Equal("ACH_Profile1", profile.BadgeCode(1));
    }

    [Fact]
    public void LevelUpCarriesTheAchievementIdRewardsAndTheReplacedBadge()
    {
        var ladder = Achievement("ACH_Ladder", "social", 30, (1, 1), (2, 5));
        var rarity = new BadgeRarityTable(BadgeRarityScale.Empty, new Dictionary<string, int> { ["ACH_Ladder2"] = 3 });
        var first = AchievementUnlockSnapshot.Capture(ladder, ladder.Levels[1], rarity);
        var second = AchievementUnlockSnapshot.Capture(ladder, new AchievementLevel(2, 4, 12, 5), rarity);

        Assert.Equal(new Unlock(30, 1, 1, "ACH_Ladder1", 1, 10, 0, 0, 30, "", "social", true, 1, 6), ReadUnlock(Payload(new AchievementUnlockedComposer(first))));
        Assert.Equal(new Unlock(30, 2, 1, "ACH_Ladder2", 12, 4, 0, 0, 30, "ACH_Ladder1", "social", true, 3, 5),
            ReadUnlock(Payload(new AchievementUnlockedComposer(second))));
    }

    internal static Data ReadData(byte[] payload) => ReadData(new FlashIncomingPacket { Buffer = payload }, whole: false);

    private static Data ReadData(FlashIncomingPacket packet, bool whole)
    {
        var data = ReadData(packet);

        if (whole) {
            Assert.False(packet.HasDataRemaining());
        }

        return data;
    }

    private static Data ReadData(byte[] payload, bool whole) => ReadData(new FlashIncomingPacket { Buffer = payload }, whole);

    private static Data ReadData(FlashIncomingPacket packet) => new(packet.ReadInt(), packet.ReadInt(), packet.ReadString(), packet.ReadInt(),
        packet.ReadInt(), packet.ReadInt(), packet.ReadInt(), packet.ReadInt(), packet.ReadBool(), packet.ReadString(), packet.ReadString(),
        packet.ReadInt(), packet.ReadInt(), packet.ReadShort());

    internal static Unlock ReadUnlock(byte[] payload)
    {
        var packet = new FlashIncomingPacket { Buffer = payload };
        var unlock = new Unlock(packet.ReadInt(), packet.ReadInt(), packet.ReadInt(), packet.ReadString(), packet.ReadInt(), packet.ReadInt(),
            packet.ReadInt(), packet.ReadInt(), packet.ReadInt(), packet.ReadString(), packet.ReadString(), packet.ReadBool(), packet.ReadInt(),
            packet.ReadInt());
        Assert.False(packet.HasDataRemaining());

        return unlock;
    }

    private static byte[] Payload(IServerPacket composer)
    {
        var (client, sent) = HabbiconTestSupport.Client(new Habbo { Id = 1, Username = "wire" });
        client.Send(composer);

        return Assert.Single(sent).Payload;
    }

    // Levels are (level, cumulative threshold); every level rewards 10 duckets and 1 point per level number.
    private static Achievement Achievement(string group, string category, int officialId, params (int Level, int Requirement)[] levels)
    {
        var achievement = new Achievement { Id = officialId, OfficialId = officialId, GroupName = group, Category = category };

        foreach (var (level, requirement) in levels) {
            achievement.AddLevel(new AchievementLevel(level, 10, level, requirement));
        }

        return achievement;
    }

    private static Habbo User(params (string Group, int Level, int Progress)[] achievements)
    {
        var habbo = new Habbo { Id = 7, Username = "u" };

        foreach (var (group, level, progress) in achievements) {
            habbo.Achievements.TryAdd(group, new UserAchievement(group, level, progress));
        }

        return habbo;
    }
}
