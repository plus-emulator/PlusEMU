using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

// "Stuff type matches" saved from the hotel editor with its second furni pick list (source 101), then run in the room:
// once right after the save and once after the room loads the stored row again.
public sealed class WiredStuffIsSecondaryPicksTests
{
    private const int Picks = 100;
    private const int SecondPicks = 101;

    // The real store's row: the native record as JSON, read back through WiredConfigurationStore.Load.
    private sealed class RowStore : IWiredConfigurationStore
    {
        private readonly Dictionary<uint, ModernWiredRuntimeTests.StoredRuntimeRow> _rows = [];

        public WiredConfiguration? Load(uint itemId, WiredBoxDescriptor descriptor) =>
            new WiredConfigurationStore(new ModernWiredRuntimeTests.StoredRuntimeRowsDatabase(_rows.Values)).Load(itemId, descriptor);

        public void Save(uint itemId, WiredBoxDescriptor descriptor, WiredConfiguration configuration)
        {
            var native = configuration.Origin!.Native;
            _rows[itemId] = new(itemId, descriptor.CanonicalName, native.Version, JsonSerializer.Serialize(native));
        }

        public void Reset(IReadOnlyCollection<uint> itemIds) { }
    }

    /// <summary>
    /// Saves the condition with the two pick lists and sources, stacks it under "enter room" with a witness, and reports whether the
    /// witness ran: the condition passed.
    /// </summary>
    private static bool Passes(string name, int itemsSource, int comparisonSource, bool reload,
        Func<WiredRawSave, (uint[] First, uint[] Second)> picks)
    {
        using var save = new WiredRawSave(name, new RowStore());
        var (first, second) = picks(save);
        var result = save.Save(furni: [itemsSource, comparisonSource], picks: first, secondary: second);
        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(new[] { itemsSource, comparisonSource }, result.Furni);

        if (reload) {
            Assert.True(save.World.Wired.TryRemove(save.Item.Id));
            Assert.NotNull(save.World.Wired.LoadWiredBox(save.Item));
            Assert.Equal(new[] { itemsSource, comparisonSource }, save.Stored().Furni);
        }

        save.World.AddBox("wf_trg_enter_room");
        var witness = save.World.AddWitness(0, 0);
        save.World.Fire(new(WiredEventKind.Enter) { Actor = save.Player.User });

        return witness.Runs.Count == 1;
    }

    [Theory]
    [InlineData("wf_cnd_stuff_is", false)]
    [InlineData("wf_cnd_stuff_is", true)]
    [InlineData("wf_cnd_not_stuff_is", false)]
    [InlineData("wf_cnd_not_stuff_is", true)]
    public void SecondPickListOfAnotherTypeIsTheComparison(string name, bool reload)
    {
        var passes = Passes(name, Picks, SecondPicks, reload, save => ([save.Furni("chair_a")], [save.Furni("table_b")]));

        Assert.Equal(name == "wf_cnd_not_stuff_is", passes);
    }

    [Theory]
    [InlineData("wf_cnd_stuff_is", false)]
    [InlineData("wf_cnd_stuff_is", true)]
    [InlineData("wf_cnd_not_stuff_is", false)]
    [InlineData("wf_cnd_not_stuff_is", true)]
    public void SecondPickListHoldingTheSameFurniMatches(string name, bool reload)
    {
        var passes = Passes(name, Picks, SecondPicks, reload, save =>
        {
            var chair = save.Furni("chair_a");

            return ([chair], [chair]);
        });

        Assert.Equal(name == "wf_cnd_stuff_is", passes);
    }

    // First list [chair], second list [chair, table]: every subject matches only when the first list is the subject.
    [Theory]
    [InlineData("wf_cnd_stuff_is", Picks, SecondPicks, false, true)]
    [InlineData("wf_cnd_stuff_is", Picks, SecondPicks, true, true)]
    [InlineData("wf_cnd_stuff_is", SecondPicks, Picks, false, false)]
    [InlineData("wf_cnd_stuff_is", SecondPicks, Picks, true, false)]
    [InlineData("wf_cnd_not_stuff_is", SecondPicks, Picks, false, true)]
    [InlineData("wf_cnd_not_stuff_is", SecondPicks, Picks, true, true)]
    public void EachSourceReadsItsOwnPickList(string name, int itemsSource, int comparisonSource, bool reload, bool expected)
    {
        var passes = Passes(name, itemsSource, comparisonSource, reload, save =>
        {
            var chair = save.Furni("chair_a");

            return ([chair], [chair, save.Furni("table_b")]);
        });

        Assert.Equal(expected, passes);
    }
}
