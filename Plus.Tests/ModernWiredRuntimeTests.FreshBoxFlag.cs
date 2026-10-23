using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// The last int of a move_rotate or match_to_sshot frame is 1 only while the box was never saved. Any accepted save reopens it as 0, and the int the client sends is ignored.
public partial class ModernWiredRuntimeTests
{
    private static async Task<int[]> SaveAndReopen(MovementOracleSession f, int[] ints, int source = 100, int[]? picks = null)
    {
        f.Client.Packets.Clear();
        await f.Save(ints, picks, source: source);
        Assert.Contains(f.Client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);

        return f.Open().Owned;
    }

    [Fact]
    public void ANeverSavedRotateBoxOpensWithTheFlagAtOne() =>
        Assert.Equal([0, 0, 1], new MovementOracleSession("wf_act_move_rotate").Open().Owned);

    [Fact]
    public async Task ARotateBoxSavedWithItsDefaultsAndNoPicksReopensWithTheFlagAtZero() =>
        Assert.Equal(new[] { 0, 0, 0 }, await SaveAndReopen(new MovementOracleSession("wf_act_move_rotate"), [0, 0]));

    [Fact]
    public async Task ARotateBoxSavedWithSource200ReopensWithTheFlagAtZero() =>
        Assert.Equal(new[] { 0, 0, 0 }, await SaveAndReopen(new MovementOracleSession("wf_act_move_rotate"), [0, 0], source: 200));

    [Fact]
    public async Task ARotateBoxSavedBackToItsDefaultsKeepsTheFlagAtZero()
    {
        var f = new MovementOracleSession("wf_act_move_rotate");

        Assert.Equal(new[] { 4, 1, 0 }, await SaveAndReopen(f, [4, 1], picks: [8]));
        Assert.Equal(new[] { 0, 0, 0 }, await SaveAndReopen(f, [0, 0]));
    }

    [Fact]
    public async Task TheFlagTheClientSendsIsIgnored()
    {
        var f = new MovementOracleSession("wf_act_move_rotate");

        Assert.Equal(new[] { 0, 0, 0 }, await SaveAndReopen(f, [0, 0, 1], source: 200));
        Assert.Equal(new[] { 0, 0, 0 }, await SaveAndReopen(f, [0, 0, 1]));
    }

    [Fact]
    public void ANeverSavedSnapshotBoxOpensWithTheFlagAtOne() =>
        Assert.Equal([0, 0, 0, 0, 1], new MovementOracleSession("wf_act_match_to_sshot").Open().Owned);

    [Fact]
    public async Task ASnapshotBoxSavedWithNoPicksReopensWithTheFlagAtZero() =>
        Assert.Equal(new[] { 0, 0, 0, 0, 0 }, await SaveAndReopen(new MovementOracleSession("wf_act_match_to_sshot"), [0, 0, 0]));

    [Fact]
    public async Task ASnapshotBoxSavedWithAPickReopensWithTheFlagAtZero() =>
        Assert.Equal(new[] { 1, 1, 1, 0, 0 }, await SaveAndReopen(new MovementOracleSession("wf_act_match_to_sshot"), [1, 1, 1], picks: [8]));

    // A row stored before this change has no flag of its own; any stored row is a saved box.
    [Theory]
    [InlineData("wf_act_move_rotate", new[] { 0, 0 }, new[] { 0, 0, 0 })]
    [InlineData("wf_act_match_to_sshot", new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 0, 0 })]
    public void ABoxLoadedFromAStoredRowWithDefaultsOpensWithTheFlagAtZero(string name, int[] stored, int[] frame)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var row = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [.. stored] };
        var f = new MovementOracleSession(name, row: new StoredRuntimeRow(100, name, 2, JsonSerializer.Serialize(row)));

        Assert.Equal(frame, f.Open().Owned);
    }
}
