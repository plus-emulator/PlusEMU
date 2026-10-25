using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public sealed class WiredPassDirectoryTests
{
    [Fact]
    public void InsideAPassEachDefinitionAndRoomOwnerIsReadOnce()
    {
        var inner = new CountingDirectory();
        var directory = new WiredPassDirectory(inner);

        using (directory.OpenPass()) {
            for (var i = 0; i < 3; i++) {
                Assert.Equal("v10", directory.Find(10)!.Name);
                Assert.Null(directory.Find(99));
                Assert.Equal(5u, directory.GetRoomOwner(1));
            }
        }

        Assert.Equal(2, inner.Finds);
        Assert.Equal(1, inner.Owners);
    }

    [Fact]
    public void OutsideAPassEveryLookupReadsAgain()
    {
        var inner = new CountingDirectory();
        var directory = new WiredPassDirectory(inner);

        using (directory.OpenPass()) {
            directory.Find(10);
        }

        directory.Find(10);
        directory.Find(10);
        directory.GetRoomOwner(1);
        directory.GetRoomOwner(1);

        Assert.Equal(3, inner.Finds);
        Assert.Equal(2, inner.Owners);
    }

    [Fact]
    public void ANestedPassKeepsRememberingUntilTheOutermostPassEnds()
    {
        var inner = new CountingDirectory();
        var directory = new WiredPassDirectory(inner);
        var outer = directory.OpenPass();

        using (directory.OpenPass()) {
            directory.Find(10);
        }

        directory.Find(10);
        outer.Dispose();
        outer.Dispose();
        directory.Find(10);

        Assert.Equal(2, inner.Finds);
    }

    [Fact]
    public void TheEngineOpensOneScopeForTheOutermostPass()
    {
        var engine = new WiredStackEngine(() => 0, _ => true, _ => true, _ => { }, _ => { });
        var opened = 0;
        var closed = 0;
        engine.PassScope = () =>
        {
            opened++;

            return new Scope(() => closed++);
        };

        Assert.True(engine.Mutate(() => engine.Mutate(() => opened == 1 && closed == 0)));
        Assert.Equal(1, opened);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void AFilteringVariableSelectorReadsOnlyTheSelectedFurni()
    {
        var world = new WiredSelectorWorld(8, 8,
            Enumerable.Range(1, 50).Select(i => new WiredSelectorFurniture((uint)i, 1, "f", "0", i % 8, i / 8, 0, 1, [(i % 8, i / 8)])).ToArray(), []);
        var pool = new WiredSelectedIds();
        pool.FurniIds.UnionWith([2, 3, 4, 5]);
        var read = new List<uint>();

        foreach (var invert in new[] { 0, 1 }) {
            read.Clear();
            var input = new WiredSelectorInputs(new(), pool, new(), FurniModified: true,
                FurniVariablePredicate: (_, _, id) =>
                {
                    read.Add(id);

                    return id % 2 == 0;
                });
            var config = new WiredConfiguration { IntParams = [0, 0, 0, 0, 0, 0, 0, 1, invert] };
            var result = WiredSelectorModule.SelectRaw("wf_slc_furni_with_var", config, world, input);

            Assert.Equal(invert == 0 ? [2u, 4u] : [3u, 5u], WiredSelectorModule.Compose(result, world, input).FurniIds.Order());
            Assert.Equal([2u, 3u, 4u, 5u], read.Order());
        }
    }

    private sealed class CountingDirectory : IWiredVariableDirectory
    {
        public int Finds;
        public int Owners;

        public WiredVariableDefinition? Find(uint id)
        {
            Finds++;

            return id == 10 ? new(id, 1, 5, "v10", WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, true) : null;
        }

        public uint? GetRoomOwner(uint roomId)
        {
            Owners++;

            return roomId == 1 ? 5u : null;
        }
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
