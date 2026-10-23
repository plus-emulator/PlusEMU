using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredConditionParityTests
{
    // The strict comparisons 0, 1 and 2 are unchanged, a stored comparison of 3 passes when the count is at most the amount, and
    // a stored comparison of 4 passes when it differs from the amount.
    [Theory]
    [InlineData(0, 3, false)]
    [InlineData(1, 3, false)]
    [InlineData(2, 3, true)]
    [InlineData(3, 3, true)]
    [InlineData(1, 4, true)]
    [InlineData(2, 4, false)]
    [InlineData(3, 4, true)]
    [InlineData(2, 0, false)]
    [InlineData(3, 0, true)]
    [InlineData(2, 1, true)]
    [InlineData(1, 2, true)]
    [InlineData(2, 2, false)]
    public void SelectionQuantityComparisonsThreeAndFourMeanAtMostAndDifferent(int amount, int comparison, bool expected)
    {
        var (room, _, items) = World();
        var first = Floor(1, "subject", 0, 0);
        var second = Floor(2, "subject", 1, 0);
        items[first.Id] = first;
        items[second.Id] = second;
        var box = Box(room, "wf_cnd_slc_quantity", _ => null);
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor) with
        {
            OwnedIntParams = [0, amount, comparison],
            FurniSourceTypes = [200],
            UserSourceTypes = [200]
        };
        Assert.True(WiredNativeEditorProjection.TryCompile(box.Item.Id, box.Descriptor, native, out var proposed));
        Assert.True(box.TryValidateConfiguration(proposed, out var config, out var error), error);
        box.ApplyConfiguration(config);
        var context = Context(room, [first, second], []);
        context.FurniIdentity[first.Id] = first;
        context.FurniIdentity[second.Id] = second;
        context.SelectorPool.FurniIds.Add(first.Id);
        context.SelectorPool.FurniIds.Add(second.Id);

        Assert.Equal(expected, box.Execute(context));
    }
}
