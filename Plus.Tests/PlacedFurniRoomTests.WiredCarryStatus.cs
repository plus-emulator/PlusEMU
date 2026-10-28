using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

// A queue tile carries a standing user down a column with a Move To Direction firing every 200 ms and an animation time of 200 ms.
// The "wf" status stays on a user while the animation of its last wired move runs. Later status updates carry no "wf".
public partial class PlacedFurniRoomTests
{
    private const uint CarryTile = 90, CarryStackId = 91;

    [Theory]
    [InlineData(200)]
    [InlineData(1000)]
    public void ACarriedUserHasTheWfStatusOnlyOnUpdatesInsideTheAnimationOfItsLastMove(int animationMs)
    {
        var seen = RunCarry(animationMs);

        Assert.Contains(seen, entry => entry.Wf);
        Assert.Contains(seen, entry => !entry.Wf && entry.Gap > entry.Duration);
        Assert.All(seen, entry => Assert.Equal(entry.Gap <= entry.Duration, entry.Wf));
    }

    [Fact]
    public void AfterTheLastMoveOneUpdateWithoutWfClearsTheStatusAndNoMoreUpdatesFollow()
    {
        var seen = RunCarry(1000);

        var last = seen[^1];
        Assert.False(last.Wf);
        Assert.True(last.Gap >= 1000 && last.Gap < 1500, $"gap {last.Gap}");
        Assert.Single(seen, entry => !entry.Wf);
    }

    private List<(long At, long Gap, int Duration, bool Wf)> RunCarry(int animationMs)
    {
        var (clock, manager) = GridRoom();
        var tile = ExecutorFloor(CarryTile, 1, 0);
        var actor = ExecutorActor(1, 0);
        CarryStack(tile, animationMs);

        var passes = RunRoom(manager, clock, clock.Ms + 4000, period: 25);

        var lastMove = (At: long.MinValue, Duration: 0);
        var seen = new List<(long At, long Gap, int Duration, bool Wf)>();

        foreach (var pass in passes) {
            foreach (var packet in pass.Packets) {
                if (packet.Header == ServerPacketHeader.WiredMovementsComposer
                    && MovementEntries(packet).FirstOrDefault(entry => entry.Type == 0 && entry.Id == actor.VirtualId) is { Type: 0 } move) {
                    lastMove = (pass.At, move.DurationMs);
                }
                else if (packet.Header == ServerPacketHeader.UserUpdateComposer
                         && UserUpdateEntry(packet, actor) is { } update && lastMove.At != long.MinValue) {
                    seen.Add((pass.At, pass.At - lastMove.At, lastMove.Duration, update.Status.Contains("/wf/")));
                }
            }
        }

        return seen;
    }

    private void CarryStack(Item tile, int animationMs)
    {
        AddFollowBox(CarryStackId, "wf_trg_period_short", new() { IntParams = [4] });
        AddFollowBox(CarryStackId + 1, "wf_act_move_to_dir", new() { IntParams = [4, 0, 100, 1], SelectedItems = [tile.Id] });
        AddFollowBox(CarryStackId + 2, "wf_xtra_mov_carry_users", new() { IntParams = [0, 900] });
        AddFollowBox(CarryStackId + 3, "wf_xtra_anim_time", new() { IntParams = [animationMs] });
    }
}
