using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A split signal runs its receiving stack once per furni in one tick. Each run gives the same context variable, and its definition box
    // flips once per click, like a single give.
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public async Task ASplitSignalFlipsTheContextBoxItsReceiverGivesOncePerClick(int split)
    {
        const uint second = 421;
        var (wired, actor, clock) = await DelayedPointerRoom();
        var antenna = Furni(PointerSelectorAntenna, InteractionType.None, WiredBoxType.None);
        antenna.Definition.InteractionName = "antenna";
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, antenna, 3, 3, 0, true, false, false));
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, Furni(second, InteractionType.None, WiredBoxType.None), 2, 0, 0, true, false, false));
        AddSpeechBox(_dpNext++, "wf_slc_furni_picks", new() { IntParams = [0, 1], SelectedItems = [DpHolder, second] }, _dpZ++);
        await DpEffect("wf_act_send_signal", [split, 0], [], furni: [100, 200], users: [0], picks: [PointerSelectorAntenna]);
        _dpTile = (3, 1);
        _dpZ = 0;
        AddSpeechBox(_dpNext++, "wf_trg_recv_signal", new() { IntParams = [0, 100], SelectedItems = [PointerSelectorAntenna] }, _dpZ++, 3, 1);
        await DpGive(DpContext, "ctx:" + DpCt, 5);
        wired.FlushFlashes();
        var states = new List<string>();

        for (var click = 0; click < 3; click++) {
            _client.Packets.Clear();
            clock.Now = clock.Now.AddSeconds(3);
            actor.OnChat(0, "go", false);

            for (var step = 0; step < 40; step++) {
                clock.Now = clock.Now.AddMilliseconds(50);
                wired.OnFastCycle();
            }

            states.AddRange(DataUpdates(_client).SelectMany(batch => batch).Where(entry => entry.Id == DpCt).Select(entry => entry.State));
        }

        Assert.Equal(["6", "7", "6"], states);
    }
}
