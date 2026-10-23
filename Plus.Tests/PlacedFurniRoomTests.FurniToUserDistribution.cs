using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

// Furni To User with a users selector that picks both users of the room, saved through the client's raw packets and fired by chat.
public partial class PlacedFurniRoomTests
{
    private const uint DealtTrigger = 301, DealtSelector = 302, DealtAction = 303;

    private async Task<(WiredComponent Wired, RoomUser Owner, RoomUser Visitor, TestClient VisitorClient, SpeechClock Clock, Item[] Movers)>
        PrepareDealtFurni(int furni, int seed)
    {
        var (wired, owner, visitor, visitorClient, clock) = PrepareTwoUsers();
        Stand(owner, 0, 3);
        Stand(visitor, 3, 3);
        var movers = Enumerable.Range(0, furni).Select(index =>
        {
            var mover = Furni((uint)(401 + index), InteractionType.None, WiredBoxType.None);
            mover.Definition.Height = 1;
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, mover, index, 0, 0, true, false, false));

            return mover;
        }).ToArray();
        var service = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(),
            TestLogging.For<WiredConfigurationService>());
        AddSpeechBox(DealtTrigger, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "go" }, 0);
        PlaceUnsaved(DealtSelector, "wf_slc_users_bytype", 1);
        PlaceUnsaved(DealtAction, "wf_act_furni_to_user", 2);
        await new SaveWiredSelectorConfigEvent(service).Parse(_client, ClientPacket((int)DealtSelector, 1, 1, "", 0, false, false, 0, 0, 0, 0));
        var picks = new List<object> { (int)DealtAction, 0, "", furni };
        picks.AddRange(movers.Select(mover => (object)(int)mover.Id));
        picks.AddRange([0, 1, 100, 1, 200, 0, 0]);
        await new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket([.. picks]));
        Assert.True(wired.TryGet(DealtAction, out var action));
        action.GetType().GetField("_random", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(action, new Random(seed));

        return (wired, owner, visitor, visitorClient, clock, movers);
    }

    private void Stand(RoomUser user, int x, int y)
    {
        _room.GetGameMap().UpdateUserMovement(new(user.X, user.Y), new(x, y), user);
        (user.X, user.Y, user.Z) = (x, y, 0);
    }

    // Three furni and two users give one user two furni and the other one. Two furni go one to each user.
    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 17)]
    [InlineData(3, 0)]
    [InlineData(3, 17)]
    public async Task FurniToUserDealsTheFurniEvenlyBetweenTheSelectedUsers(int furni, int seed)
    {
        var (wired, owner, visitor, _, clock, movers) = await PrepareDealtFurni(furni, seed);
        var shares = new HashSet<string>();

        for (var firing = 0; firing < 30; firing++) {
            owner.OnChat(0, "go", false);
            Advance(wired, clock, 1000);

            var onOwner = movers.Count(mover => (mover.GetX, mover.GetY) == (owner.X, owner.Y));
            var onVisitor = movers.Count(mover => (mover.GetX, mover.GetY) == (visitor.X, visitor.Y));
            Assert.Equal(furni, onOwner + onVisitor);
            Assert.InRange(onOwner, furni / 2, (furni + 1) / 2);
            shares.Add(string.Join(",", movers.Select(mover => mover.GetX)));
        }

        Assert.True(shares.Count > 1);
    }

    // A furni dealt to the user it already stands under moves onto the top of the other furni there when one stands above it; a furni
    // already on top stays.
    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public async Task FurniToUserRestacksAFurniDealtTheUserItAlreadyStandsUnder(int seed)
    {
        var (wired, owner, _, visitorClient, clock, movers) = await PrepareDealtFurni(2, seed);
        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, movers[0], 0, 3, 0, false, false, false));
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, movers[1], 0, 3, 0, false, false, false));
        Assert.Equal((0.0, 1.0), (movers[0].GetZ, movers[1].GetZ));

        owner.OnChat(0, "go", false);
        Advance(wired, clock, 1000);

        Assert.Equal((0, 3, 2.0), (movers[0].GetX, movers[0].GetY, movers[0].GetZ));
        Assert.Equal((0, 3), (movers[1].GetX, movers[1].GetY));
        Assert.Contains(movers[1].GetZ, new[] { 1.0, 3.0 });
    }
}
