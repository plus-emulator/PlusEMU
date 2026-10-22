using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredBehaviorTests
{
    private static RoomUser[] UsersAt(params (int X, int Y)[] tiles) => tiles.Select(tile =>
    {
        var user = (RoomUser)RuntimeHelpers.GetUninitializedObject(typeof(RoomUser));
        (user.X, user.Y) = tile;

        return user;
    }).ToArray();

    // Observed on AIR WIN63-202610201032-107964540: with two users selected, two furni went one to each user in 30 of 30 firings and
    // three furni went two to one user and one to the other in 90 of 90 firings. Who got which furni changed from firing to firing.
    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 17)]
    [InlineData(3, 0)]
    [InlineData(3, 17)]
    public void FurniToUserGivesEachSelectedUserTheSameNumberOfFurniGiveOrTakeOne(int furni, int seed)
    {
        var users = UsersAt((3, 4), (8, 9));
        var movers = Enumerable.Range(1, furni).Select(id => Item((uint)id, 0, 0)).ToArray();
        var action = new WiredMovementActions(new Random(seed));
        var assignments = new HashSet<string>();

        for (var firing = 0; firing < 30; firing++) {
            var landings = new Dictionary<uint, (int X, int Y)>();
            action.Execute("wf_act_furni_to_user", new() { IntParams = [100, 0] }, movers, [], users,
                (item, x, y, _, _) => { landings[item.Id] = (x, y); return true; }, (_, _, _, _, _) => false, (_, _) => { });

            Assert.Equal(furni, landings.Count);
            var counts = users.Select(user => landings.Values.Count(tile => tile == (user.X, user.Y))).Order().ToArray();
            Assert.Equal(furni == 2 ? [1, 1] : new[] { 1, 2 }, counts);
            assignments.Add(string.Join(";", landings.OrderBy(pair => pair.Key).Select(pair => pair.Value)));
        }

        Assert.True(assignments.Count > 1);
    }

    // Observed on AIR WIN63-202610201032-107964540: with three furni and two users, the furni that had a user to itself changed between
    // firings (two different furni in each of three runs of 30).
    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void FurniToUserChangesWhichFurniHasAUserToItself(int seed)
    {
        var users = UsersAt((3, 4), (8, 9));
        var movers = new[] { Item(1, 0, 0), Item(2, 0, 0), Item(3, 0, 0) };
        var action = new WiredMovementActions(new Random(seed));
        var alone = new HashSet<uint>();

        for (var firing = 0; firing < 30; firing++) {
            var landings = new Dictionary<uint, (int X, int Y)>();
            action.Execute("wf_act_furni_to_user", new() { IntParams = [100, 0] }, movers, [], users,
                (item, x, y, _, _) => { landings[item.Id] = (x, y); return true; }, (_, _, _, _, _) => false, (_, _) => { });
            alone.Add(landings.Single(pair => landings.Values.Count(tile => tile == pair.Value) == 1).Key);
        }

        Assert.True(alone.Count > 1);
    }

    // Observed on AIR WIN63-202610201032-107964540: Teleport To with two users selected sent them to two different furni in 30 of 30
    // firings with two furni and 30 of 30 with three furni; which furni each user got changed from firing to firing.
    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 17)]
    [InlineData(3, 0)]
    [InlineData(3, 17)]
    public void TeleportToSendsEachSelectedUserToADifferentFurni(int furni, int seed)
    {
        var users = UsersAt((3, 4), (8, 9));
        var targets = Enumerable.Range(1, furni).Select(id => Item((uint)id, id, 0)).ToArray();
        var action = new WiredMovementActions(new Random(seed));
        var used = new HashSet<uint>();

        for (var firing = 0; firing < 30; firing++) {
            var destinations = new List<uint>();
            action.Execute("wf_act_teleport_to", new() { IntParams = [0, 100, 0] }, [], targets, users,
                (_, _, _, _, _) => false, (_, target, _, _, _) => { destinations.Add(target.Id); return true; }, (_, _) => { });

            Assert.Equal(2, destinations.Count);
            Assert.Equal(2, destinations.Distinct().Count());
            used.UnionWith(destinations);
        }

        Assert.Equal(furni, used.Count);
    }
}
