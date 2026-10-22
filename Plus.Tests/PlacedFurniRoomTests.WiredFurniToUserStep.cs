using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// A 50 ms Repeat Short with Furni to User (users from a selector) and physics through furni and users moves a 0.15 high tile.
// Observed on AIR WIN63-202610201032-107964540: on every step tick the furni goes to the step target (the mv tile of that tick's
// UserUpdate), in the same ms, after that UserUpdate.
public partial class PlacedFurniRoomTests
{
    private const uint FollowTile = 50, FollowStackId = 51;

    [Fact]
    public void FurniToUserSendsTheFurniToAWalkingUsersStepTargetAndSendsTheStepAgainWithTheRaisedHeight()
    {
        var (clock, manager) = GridRoom();
        var tile = ExecutorFloor(FollowTile, 1, 0, height: 0.15);
        var actor = ExecutorActor(1, 0);
        FollowStack(tile);
        RunRoom(manager, clock, clock.Ms + 1000, period: 25);
        actor.MoveTo(1, 3);

        var passes = RunRoom(manager, clock, clock.Ms + 2500, period: 25);

        var steps = passes.Where(pass => UserStatuses(pass, actor).Any(status => status.Status.Contains("mv "))).ToArray();
        Assert.Equal([1, 2, 3], steps.Select(pass => StepTarget(UserStatuses(pass, actor)[0].Status).Y));

        foreach (var pass in steps) {
            var statuses = UserStatuses(pass, actor);
            var target = StepTarget(statuses[0].Status);
            var moves = pass.Packets.Select((packet, index) => (packet, index))
                .Where(entry => entry.packet.Header == ServerPacketHeader.WiredMovementsComposer).ToArray();
            var move = Assert.Single(moves);
            Assert.Equal((tile.Id, target.X, target.Y), FirstMovement(move.packet));
            Assert.Equal(2, statuses.Length);
            Assert.True(statuses[0].Index < move.index && move.index < statuses[1].Index,
                $"pass at {pass.At}: " + string.Join(",", pass.Packets.Select(packet => packet.Header)));
            Assert.All(pass.Packets.Select((packet, index) => (packet, index))
                    .Where(entry => entry.packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer),
                entry => Assert.InRange(entry.index, statuses[0].Index, move.index));
            Assert.Equal($"mv {target.X},{target.Y},0.15", MvPart(statuses[1].Status));
            Assert.Equal(StatusLocation(pass, actor, statuses[0].Index), StatusLocation(pass, actor, statuses[1].Index));
        }

        Assert.Equal((1, 3, 0.15), (actor.X, actor.Y, actor.Z));
    }

    [Fact]
    public void FurniToUserMovingTheFurniUnderAUserStandingStillSendsNoExtraUserUpdate()
    {
        var (clock, manager) = GridRoom();
        var tile = ExecutorFloor(FollowTile, 1, 2, height: 0.15);
        var actor = ExecutorActor(1, 0);
        FollowStack(tile);

        var passes = RunRoom(manager, clock, clock.Ms + 1500, period: 25);

        var moved = Assert.Single(passes, pass => pass.Packets.Any(packet => packet.Header == ServerPacketHeader.WiredMovementsComposer));
        var move = moved.Packets.ToList().FindIndex(packet => packet.Header == ServerPacketHeader.WiredMovementsComposer);
        Assert.Equal((tile.Id, 1, 0), FirstMovement(moved.Packets[move]));
        Assert.DoesNotContain(moved.Packets.Skip(move), packet => packet.Header == ServerPacketHeader.UserUpdateComposer);
        Assert.All(passes.Where(pass => !pass.Full), pass =>
            Assert.DoesNotContain(pass.Packets, packet => packet.Header == ServerPacketHeader.UserUpdateComposer));
    }

    private void FollowStack(Item tile)
    {
        AddFollowBox(FollowStackId, "wf_trg_period_short", new() { IntParams = [1] });
        AddFollowBox(FollowStackId + 1, "wf_act_furni_to_user", new() { IntParams = [100, 200], SelectedItems = [tile.Id] });
        AddFollowBox(FollowStackId + 2, "wf_slc_users_bytype", new() { IntParams = [1, 0, 0] });
        AddFollowBox(FollowStackId + 3, "wf_xtra_mov_physics", new() { IntParams = [0, 1, 1, 0, 900, 900, 900] });
    }

    private void AddFollowBox(uint id, string name, Plus.HabboHotel.Items.Wired.Configuration.WiredConfiguration proposed)
    {
        var box = WiredBox(id, name, 3, 2);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(box, proposed, out var config, out var error), error);
        box.ApplyConfiguration(config);
        Assert.True(_room.GetWired().AddBox(box));
    }

    private static (int Index, string Status)[] UserStatuses(GridPass pass, RoomUser actor) => pass.Packets
        .Select((packet, index) => (packet, index))
        .Where(entry => entry.packet.Header == ServerPacketHeader.UserUpdateComposer)
        .Select(entry => (entry.index, Status: StatusOf(new(pass.At, pass.Full, [entry.packet]), actor)))
        .Where(entry => entry.Status != "").ToArray();

    private static (int X, int Y) StepTarget(string status)
    {
        var parts = MvPart(status)["mv ".Length..].Split(',');

        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    private static string MvPart(string status) => status.Split('/', StringSplitOptions.RemoveEmptyEntries).Single(part => part.StartsWith("mv "));

    private static (int X, int Y, string Z) StatusLocation(GridPass pass, RoomUser actor, int index)
    {
        var body = new FlashIncomingPacket { Buffer = pass.Packets[index].Body };

        for (var count = body.ReadInt(); count > 0; count--) {
            var id = body.ReadInt();
            var location = (body.ReadInt(), body.ReadInt(), body.ReadString());
            body.ReadInt();
            body.ReadInt();
            body.ReadInt();
            body.ReadString();

            if (id == actor.VirtualId) {
                return location;
            }
        }

        throw new InvalidOperationException($"No status for actor {actor.VirtualId}");
    }

    private static (uint Id, int X, int Y) FirstMovement((uint Header, byte[] Body) packet)
    {
        var body = new FlashIncomingPacket { Buffer = packet.Body };
        Assert.True(body.ReadInt() > 0);
        body.ReadInt();
        body.ReadInt();
        body.ReadInt();
        var x = body.ReadInt();
        var y = body.ReadInt();
        body.ReadString();
        body.ReadString();

        return ((uint)body.ReadInt(), x, y);
    }
}
