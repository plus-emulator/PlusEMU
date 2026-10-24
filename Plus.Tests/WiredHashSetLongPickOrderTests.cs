using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// A card reopens its picks in the order of a Java HashSet<Long> built in three steps, with tree bins.
public class WiredHashSetLongPickOrderTests
{
    public static TheoryData<int[], int[]> TreeBinOrders => new()
    {
        // A tree bin: the tree root moves to the front of its bin.
        { [2147418240, 2147418304, -2147418369, -2147418113, 2147418112, -2147418177, 2147418368, -2147418305, -2147418241, 2147418432, 2147418176],
            [2147418304, 2147418240, -2147418369, -2147418113, 2147418112, -2147418177, 2147418368, -2147418305, -2147418241, 2147418432, 2147418176] },
        // Tree order compares the Long hash, so negative ids sort apart from positive ones.
        { [-2147418113, 2147418432, 2147418304, -2147418241, -2147418305, 2147418176, -2147418177, 2147418112, 2147418368, 2147418240, -2147418369],
            [2147418304, -2147418113, 2147418432, -2147418241, -2147418305, 2147418176, -2147418177, 2147418112, 2147418368, 2147418240, -2147418369] },
        // The middle step sizes a second set from the first.
        { [2147418496, 2147418368, -2147418241, -2147418113, 2147418432, 2147418240, -2147418177, -2147418305, 2147418560, -2147418433, 2147418176, 2147418112],
            [2147418368, 2147418240, 2147418496, 2147418560, -2147418241, -2147418305, -2147418113, 2147418112, 2147418432, -2147418433, -2147418177, 2147418176] },
    };

    [Theory]
    [MemberData(nameof(TreeBinOrders))]
    public void DensePicksReopenInTheOrderOfTheJavaLongHashSet(int[] sent, int[] reopened)
    {
        Assert.Equal(reopened, Reopen(sent));
    }

    [Fact]
    public void EverySavedPickListReopensInHabboOrder()
    {
        var wrong = new List<string>();

        foreach (var line in File.ReadAllLines(HabbiconPacketTests.Repo("Plus.Tests/Fixtures/WiredHabboPickOrderSamples.txt"))) {
            var parts = line.Split('|');
            var sent = parts[0].Split(',').Select(int.Parse).ToArray();
            var expected = parts[1].Split(',').Select(int.Parse).ToArray();

            if (!Reopen(sent).SequenceEqual(expected)) {
                wrong.Add(parts[0]);
            }
        }

        Assert.True(wrong.Count == 0, $"{wrong.Count} pick lists reopen in another order, first: {wrong.FirstOrDefault()}");
    }

    private static int[] Reopen(int[] sent)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_toggle_state", out var descriptor));
        var distinct = sent.Distinct().ToArray();
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            PrimaryItems = [.. distinct.Select(id => new WiredNativeItemReference((uint)Math.Abs(id), id < 0))],
            PrimarySent = sent.Length > distinct.Length ? sent.Length : 0
        };
        var packet = new HabbiconTestSupport.RecordingPacket();
        WiredLegacyProtocol.Write(packet, 9, 100, descriptor, new(), 20, [], native);

        return [.. packet.Writes.Skip(2).Take(distinct.Length).Cast<int>()];
    }
}
