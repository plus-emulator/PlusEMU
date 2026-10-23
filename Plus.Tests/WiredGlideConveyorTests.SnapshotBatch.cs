using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms.Chat;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Rooms.Chat.Logs;
using Plus.HabboHotel.Rooms.Chat.Styles;
using Plus.Core.Settings;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Match to Snapshot with only the position option sends every furni it moves back in one WiredMovements per firing, the same
    // as with position and altitude.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void MatchToSnapshotSendsTheMovesOfOneFiringInOneWiredMovements(int altitude)
    {
        var f = new Fixture(
        [
            (90, 5, 13, 0.0, 0, "bc_tile_1", null),
            (91, 7, 13, 0.0, 0, "bc_tile_1", null),
            (92, 9, 13, 0.0, 0, "bc_tile_1", null),
            (93, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("back")),
            (94, 4, 14, 0.65, 0, "wf_act_match_to_sshot", SnapshotConfig(altitude, (90, 5, 12), (91, 7, 12), (92, 9, 12)))
        ], live: true);
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        f.Advance(1000);
        var packets = CapturePublication(rider);
        var chat = new ChatEvent(new RoomChatService(Silent<IChatStyleManager>(), Silent<IChatlogManager>(), new FakeWordFilter(),
            Silent<ICommandManager>(), Silent<IModerationManager>(), Silent<ISettingsManager>(), Silent<IQuestManager>(),
            Silent<IRewardTrackManager>(), Silent<IGameClientManager>(), TimeProvider.System));

        f.Room.MutedUsers = [];

        // The client's chat packet: the text, then the bubble style.
        f.Owned(() => chat.Parse(rider.GetClient()!, HabbiconTestSupport.Incoming("back", 0)).GetAwaiter().GetResult());
        f.Advance(200);

        var batch = Assert.Single(ReadMovementPackets(packets));
        Assert.Equal([(1, 90, 5, 5), (1, 91, 7, 7), (1, 92, 9, 9)], batch.Select(entry => (entry.Type, entry.Id, entry.FromX, entry.ToX)).Order());
        Assert.Equal([(5, 12), (7, 12), (9, 12)], new[] { f.Items[90], f.Items[91], f.Items[92] }.Select(item => (item.GetX, item.GetY)));
    }

    // The editor's options are state, direction, position and altitude; the furni are picked and each has its saved tile.
    private static string SnapshotConfig(int altitude, params (uint Id, int X, int Y)[] saved)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_match_to_sshot", out var descriptor));
        var picks = string.Join(",", saved.Select(entry => $$"""{"ItemId":{{entry.Id}},"Wall":false}"""));
        var snapshots = string.Join(",", saved.Select(entry =>
            $$"""{"ItemId":{{entry.Id}},"DefinitionId":29284,"X":{{entry.X}},"Y":{{entry.Y}},"Z":0,"Rotation":0,"State":""}"""));

        return "native:" + $$$"""{"Version":2,"Category":{{{(int)descriptor.Category}}},"NativeCode":{{{descriptor.EditorCode}}},"OwnedIntParams":[0,0,1,{{{altitude}}}],"Text":"","PrimaryItems":[{{{picks}}}],"SecondaryItems":[],"FurniSourceTypes":[100],"UserSourceTypes":[],"VariableIds":[],"Delay":0,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[{{{snapshots}}}]}}""";
    }

    private static T Silent<T>() where T : class => DispatchProxy.Create<T, SilentService<T>>();

    // A service the chat path only passes through: every call does nothing and returns the default value.
    private class SilentService<T> : DispatchProxy where T : class
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType == typeof(Task) ? Task.CompletedTask :
            targetMethod?.ReturnType.IsValueType == true && targetMethod.ReturnType != typeof(void) ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }
}
