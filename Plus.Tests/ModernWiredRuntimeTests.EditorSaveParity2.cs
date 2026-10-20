using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

// More editor saves answered the way Habbo answers them, as observed on AIR WIN63-202610151745-318700621 (Give Score, Give Score To
// Team) and AIR WIN63-202610131318-860371703 (Chase).
public partial class ModernWiredRuntimeTests
{
    // Habbo saves a per-game limit of 99. The editor slider and the run time both stop at 10, so the limit is stored as 10.
    [Fact]
    public async Task GiveScoreSavesAPerGameLimitAboveTenAsTen()
    {
        var f = new MovementOracleSession("wf_act_give_score");

        await SaveEffect(f, [100, 2, 5, 99, "", 0, 0, 0, 1, 0, 0, 0]);

        Assert.Contains(f.Client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        var saved = Assert.Single(f.Store.Saves);
        Assert.Equal([5, 10], saved.Origin!.Native.OwnedIntParams.ToArray());
        Assert.Equal(10, saved.ScoreQuotaPerGame);
        Assert.Equal(10, f.Action.Configuration.ScoreQuotaPerGame);
    }

    [Fact]
    public async Task GiveScoreToTeamWithAnUnknownTeamIsDroppedWithoutAnyReply()
    {
        var f = new MovementOracleSession("wf_act_give_score_tm");
        var before = f.Action.Configuration;

        await SaveEffect(f, [100, 3, 1, 0, 9, "", 0, 0, 0, 0, 0, 0]);

        Assert.Empty(f.Client.Packets);
        Assert.Empty(f.Store.Saves);
        Assert.Same(before, f.Action.Configuration);
    }

    [Fact]
    public async Task ChaseSavesWithNoFurniPicked()
    {
        var f = new MovementOracleSession("wf_act_chase");

        await SaveEffect(f, [100, 0, "", 0, 0, 1, 100, 0, 0, 0]);

        Assert.Contains(f.Client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        Assert.Empty(Assert.Single(f.Store.Saves).Origin!.Native.PrimaryItems);
    }
}
