using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint MpTarget = 441, MpHolder = 442;

    // Observed on AIR WIN63-202610201032-107964540: in a stack with User To Furni or Furni To User and a Toggle State or Move and Rotate of
    // another furni, the toggled state goes out before the WiredMovements and the turned furni's ObjectUpdate goes out before both,
    // whatever the box order (12 of 12 firings for each case).
    [Theory]
    [InlineData("wf_act_user_to_furni", "wf_act_move_rotate", false)]
    [InlineData("wf_act_user_to_furni", "wf_act_move_rotate", true)]
    [InlineData("wf_act_user_to_furni", "wf_act_toggle_state", false)]
    [InlineData("wf_act_user_to_furni", "wf_act_toggle_state", true)]
    [InlineData("wf_act_furni_to_user", "wf_act_move_rotate", false)]
    [InlineData("wf_act_furni_to_user", "wf_act_move_rotate", true)]
    [InlineData("wf_act_furni_to_user", "wf_act_toggle_state", false)]
    [InlineData("wf_act_furni_to_user", "wf_act_toggle_state", true)]
    public async Task AMovedUserOrFurniIsSentAfterTheStateOrTurnOfAnotherFurniWhateverTheBoxOrder(string mover, string other, bool otherFirst)
    {
        var (wired, actor, clock) = await DelayedPointerRoom(executeInOrder: false);
        var engine = typeof(WiredComponent).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wired)!;
        var random = engine.GetType().GetField("_effectOrderRandom", BindingFlags.Instance | BindingFlags.NonPublic)!;
        random.SetValue(engine, new Random(0));
        var target = Furni(MpTarget, InteractionType.None, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, target, 3, 3, 0, true, false, false));
        var holder = Furni(MpHolder, InteractionType.None, WiredBoxType.None);
        holder.Definition.Modes = 5;
        holder.Definition.AdjustableHeights = new List<double>();
        holder.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, holder, 3, 2, 0, true, false, false));

        async Task Mover() => await DpEffect(mover, mover == "wf_act_user_to_furni" ? [0] : [], [], furni: [100], users: [0], picks: [MpTarget]);
        async Task Other() => await DpEffect(other, other == "wf_act_move_rotate" ? [0, 1] : [0], [], furni: [100], users: [], picks: [MpHolder]);

        if (otherFirst) {
            await Other();
            await Mover();
        }
        else {
            await Mover();
            await Other();
        }

        for (var firing = 0; firing < 12; firing++) {
            random.SetValue(engine, new Random(firing));

            using (RoomOwnerScope.Enter(_room)) {
                Assert.True(WiredRoomOperations.RelocateAvatar(_room, actor, firing % 3, 2, false, ignoreOccupants: true));
            }

            wired.FlushFlashes();
            _client.Packets.Clear();
            actor.OnChat(0, "go", false);

            for (var elapsed = 50; elapsed <= 1000; elapsed += 50) {
                clock.Now = clock.Now.AddMilliseconds(50);
                wired.OnFastCycle();
            }

            var headers = _client.Packets.Select(packet => packet.Header).ToList();
            var movement = headers.IndexOf(ServerPacketHeader.WiredMovementsComposer);
            Assert.True(movement >= 0, $"firing {firing}: WiredMovements; sent {string.Join(",", headers)}");

            if (other == "wf_act_move_rotate") {
                var turned = headers.IndexOf(ServerPacketHeader.ObjectUpdateComposer);
                Assert.True(turned >= 0 && turned < movement, $"firing {firing}: ObjectUpdate {turned} before WiredMovements {movement}");
            }
            else {
                var state = _client.Packets.FindIndex(packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer
                    && DataItems(packet.Body).Any(item => item.Id == (int)MpHolder));
                Assert.True(state >= 0 && state < movement, $"firing {firing}: state update {state} before WiredMovements {movement}");
            }
        }
    }
}
