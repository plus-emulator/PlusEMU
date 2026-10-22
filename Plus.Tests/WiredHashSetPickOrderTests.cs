using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: every card below reopens its picks in the iteration order of a Java HashSet<Integer>
// of the saved ids, not in the saved order. Orders were saved and reopened on Habbo with five floor ids.
public class WiredHashSetPickOrderTests
{
    private static readonly int[] HabboSent = [2147418128, 2147418114, 2147418112, 2147418130, 2147418113];
    private static readonly int[] HabboReopened = [2147418114, 2147418130, 2147418113, 2147418128, 2147418112];

    [Theory]
    [InlineData("wf_act_toggle_state")]
    [InlineData("wf_act_toggle_to_rnd")]
    [InlineData("wf_act_move_rotate")]
    [InlineData("wf_act_match_to_sshot")]
    [InlineData("wf_act_call_stacks")]
    [InlineData("wf_act_chase")]
    [InlineData("wf_act_furni_to_furni")]
    [InlineData("wf_cnd_has_furni_on")]
    [InlineData("wf_cnd_furnis_hv_avtrs")]
    [InlineData("wf_cnd_match_snapshot")]
    [InlineData("wf_cnd_stuff_is")]
    [InlineData("wf_trg_state_changed")]
    [InlineData("wf_trg_walks_on_furni")]
    [InlineData("wf_trg_walks_off_furni")]
    [InlineData("wf_trg_click_furni")]
    [InlineData("wf_slc_furni_picks")]
    [InlineData("wf_slc_furni_bytype")]
    [InlineData("wf_slc_furni_onfurni")]
    public void TheCardReopensItsPicksInHashSetOrder(string card)
    {
        var frame = Frame(card, HabboSent, []);

        Assert.Equal(HabboReopened, Primary(frame));
    }

    [Fact]
    public void FurniToFurniReopensItsTargetPicksInHashSetOrderToo()
    {
        int[] targets = [2147418113, 2147418130, 2147418112, 2147418114, 2147418128];
        int[] reopened = [2147418130, 2147418114, 2147418113, 2147418112, 2147418128];

        var frame = Frame("wf_act_furni_to_furni", HabboSent, targets);

        Assert.Equal(HabboReopened, Primary(frame));
        Assert.Equal(reopened, frame.Skip(2 + HabboSent.Length + 1).Take(targets.Length));
    }

    // Habbo, 9 and 10 ids in one bucket (ids 16 apart): the ninth id resizes the table, which splits the bucket.
    [Theory]
    [InlineData(new[] { 2147418131, 2147418147, 2147418163, 2147418179, 2147418195, 2147418211, -2147418116, -2147418132 },
        new[] { 2147418131, 2147418147, 2147418163, 2147418179, 2147418195, 2147418211, -2147418116, -2147418132 })]
    [InlineData(new[] { 2147418131, 2147418147, 2147418163, 2147418179, 2147418195, 2147418211, -2147418116, -2147418132, -2147418148 },
        new[] { 2147418131, 2147418163, 2147418195, -2147418132, 2147418147, 2147418179, 2147418211, -2147418116, -2147418148 })]
    [InlineData(new[] { 2147418131, 2147418147, 2147418163, 2147418179, 2147418195, 2147418211, -2147418116, -2147418132, -2147418148, 2147418129 },
        new[] { 2147418131, 2147418163, 2147418195, -2147418132, 2147418129, 2147418147, 2147418179, 2147418211, -2147418116, -2147418148 })]
    // Real JDK 21 java.util.HashSet, 18 ids 3, 19, ..., 275: the table doubles twice.
    [InlineData(new[] { 3, 19, 35, 51, 67, 83, 99, 115, 131, 147, 163, 179, 195, 211, 227, 243, 259, 275 },
        new[] { 3, 67, 131, 195, 259, 19, 83, 147, 211, 275, 35, 99, 163, 227, 51, 115, 179, 243 })]
    public void NineIdsInOneBucketResizeTheTableLikeJava(int[] sent, int[] reopened)
    {
        Assert.Equal(reopened, Primary(Frame("wf_act_toggle_state", sent, [])));
    }

    private static int[] Primary(int[] frame) => [.. frame.Skip(2).Take(frame[1])];

    private static int[] Frame(string card, int[] primary, int[] secondary)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            PrimaryItems = [.. primary.Select(Item)],
            SecondaryItems = [.. secondary.Select(Item)]
        };
        var packet = new HabbiconTestSupport.RecordingPacket();

        WiredLegacyProtocol.Write(packet, 9, 100, descriptor, new(), 20, [], native);

        return [.. packet.Writes.Select(write => write is int value ? value : 0)];
    }

    private static WiredNativeItemReference Item(int id) => new((uint)Math.Abs(id), id < 0);
}
