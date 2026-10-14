using System.Drawing;
using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Group g3, Filter Users by Variable inside a real stack: enter-room trigger, all-players selector, the filter, then a teleport of the selected users.
public partial class ModernWiredRuntimeTests
{
    private sealed class G3ScoreCatalog : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 5;
        public WiredVariableDefinition? Find(uint id) => id == 10
            ? new(10, 1, 5, "score", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true) : null;
    }

    private static (TeleportFixture F, RoomUser[] Players, WiredVariableAddonBox Filter) G3FilteredStack(int count, int sort, params long?[] scores)
    {
        var f = new TeleportFixture();
        f.Room.Id = 1;
        f.Target.Definition.Walkable = true;
        f.Room.GetGameMap().AddToMap(f.Target);
        RoomUser[] players = [f.User, f.AddPlayer(8, 2, 0), f.AddPlayer(9, 0, 2)];
        f.SelectPlayers();
        f.Use(0, 200);
        var module = new WiredVariableModule(1, new G3ScoreCatalog(), new MemoryWiredVariableStore(), TimeProvider.System);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Room.GetWired().Variables, module);
        var empty = new WiredVariableFrame(1, players.Select(WiredVariableRuntimeFrames.UserHolder).ToArray());

        for (var i = 0; i < scores.Length; i++) {
            if (scores[i] is { } score) {
                Assert.True(module.Mutate(new(WiredVariableTarget.User, "custom:10"), WiredVariableRuntimeFrames.UserHolder(players[i]), WiredVariableMutation.Give, score, empty));
            }
        }

        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_filter_users_by_var", out var descriptor));
        var item = MakeItem(120, "wf_xtra_filter_users_by_var");
        var filter = new WiredVariableAddonBox(f.Room, item, descriptor, module, _ => new Dictionary<int, string>());
        WiredNativeTestSupport.Install(filter, WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [count, sort, 0, 1],
            VariableIds = ["user:10", "n"]
        });
        f.Items[120] = item;
        Assert.True(f.Engine.Add(filter));

        return (f, players, filter);
    }

    [Fact]
    public void G3TheFilterNarrowsWhoTheTeleportCarriesToTheHighestScorer()
    {
        var (f, players, _) = G3FilteredStack(1, 0, 5, 20, 10);
        using var _ = f;
        f.Fire();
        Assert.Equal(new Point(0, 0), players[0].Coordinate);
        Assert.Equal(new Point(1, 1), players[1].Coordinate);
        Assert.Equal(new Point(0, 2), players[2].Coordinate);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void G3TheFilterKeepsTheLowestTwoWhenAskedAndLeavesAnUnscoredPlayerOut()
    {
        var (f, players, _) = G3FilteredStack(2, 1, 5, 20, null);
        using var _ = f;
        f.Fire();
        Assert.Equal(new Point(1, 1), players[0].Coordinate);
        Assert.Equal(new Point(1, 1), players[1].Coordinate);
        Assert.Equal(new Point(0, 2), players[2].Coordinate);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void G3AFilterOnAVariableNobodyHoldsStopsTheTeleportWithoutAnError()
    {
        var (f, players, _) = G3FilteredStack(3, 0, null, null, null);
        using var _ = f;
        f.Fire();
        Assert.All(players, player => Assert.NotEqual(new Point(1, 1), player.Coordinate));
        Assert.Empty(f.Errors);
    }
}
