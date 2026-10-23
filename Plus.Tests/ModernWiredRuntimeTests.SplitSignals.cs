using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    [Theory]
    [InlineData(false, false, 0, 0, 1)]
    [InlineData(false, true, 0, 0, 0)]
    [InlineData(true, false, 0, 0, 0)]
    [InlineData(true, true, 0, 0, 0)]
    [InlineData(false, true, 0, 2, 2)]
    [InlineData(true, false, 2, 0, 2)]
    [InlineData(true, true, 2, 2, 4)]
    [InlineData(false, false, 2, 2, 1)]
    [InlineData(false, true, 2, 0, 0)]
    [InlineData(true, false, 0, 2, 0)]
    public void SplitSignalsEmitOnlyForSelectedTargets(bool splitFurni, bool splitUsers, int furniCount, int userCount, int expected)
    {
        var (room, _, items) = World();
        var antenna = MakeItem(1, "antenna");
        items[antenna.Id] = antenna;

        for (var i = 0; i < furniCount; i++) {
            var item = MakeItem((uint)i + 2, "forwarded");
            items[item.Id] = item;
        }

        var users = Enumerable.Range(0, userCount).Select(i =>
            new RoomUser(i + 1, 0, i + 7, room, null, TestChatEmotions.Unused, TestRewardProgress.Unused)).ToArray();
        var operations = new RecordingOperations();
        var context = new WiredRuntimeContext(room, new(WiredEventKind.Use), new(() => items.Values.ToArray(), () => users), operations);
        context.SelectorPool.FurniIds.UnionWith(items.Keys.Where(id => id != antenna.Id));
        context.SelectorPool.UserIds.UnionWith(users.Select(user => user.VirtualId));
        var action = ActionBox(room, "wf_act_send_signal");
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(action, new()
        {
            IntParams = [0, WiredSources.Selector, WiredSources.Selector, splitFurni ? 1 : 0, splitUsers ? 1 : 0, 0],
            SelectedItems = [antenna.Id]
        }, out var config, out var error), error);
        action.ApplyConfiguration(config);

        Assert.Equal(expected != 0, action.Execute(context));
        Assert.Equal(expected, operations.Signals.Count);
        Assert.All(operations.Signals, signal =>
        {
            Assert.Equal([antenna.Id], signal.Receivers);
            Assert.Equal(splitFurni ? 1 : furniCount, signal.Selection.FurniIds.Count);
            Assert.Equal(splitUsers ? 1 : userCount, signal.Selection.UserIds.Count);
        });
    }

    // With two users split into signals, the user whose signal runs last changes from
    // firing to firing, whatever the order of the selectors.
    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void SplitUserSignalsGoOutInARandomOrderEachFiring(int seed)
    {
        var (room, _, items) = World();
        var antenna = MakeItem(1, "antenna");
        items[antenna.Id] = antenna;
        var users = Enumerable.Range(0, 3).Select(i =>
            new RoomUser(i + 1, 0, i + 7, room, null, TestChatEmotions.Unused, TestRewardProgress.Unused)).ToArray();
        var operations = new RecordingOperations();
        var action = ActionBox(room, "wf_act_send_signal", random: new Random(seed));
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(action, new()
        {
            IntParams = [0, WiredSources.Selector, WiredSources.Selector, 0, 1, 0],
            SelectedItems = [antenna.Id]
        }, out var config, out var error), error);
        action.ApplyConfiguration(config);
        var orders = new HashSet<string>();

        for (var firing = 0; firing < 20; firing++) {
            operations.Signals.Clear();
            var context = new WiredRuntimeContext(room, new(WiredEventKind.Use), new(() => items.Values.ToArray(), () => users), operations);
            context.SelectorPool.UserIds.UnionWith(users.Select(user => user.VirtualId));

            Assert.True(action.Execute(context));
            var order = operations.Signals.Select(signal => Assert.Single(signal.Selection.UserIds)).ToArray();
            Assert.Equal(users.Select(user => user.VirtualId).Order(), order.Order());
            orders.Add(string.Join(",", order));
        }

        Assert.True(orders.Count > 1, "every firing sent the split signals in the same order");
    }
}
