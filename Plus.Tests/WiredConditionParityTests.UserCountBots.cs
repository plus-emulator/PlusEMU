using System.Runtime.CompilerServices;
using Plus.Communication.Flash;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredConditionParityTests
{
    // With the owner and one bot in the room, the user count conditions see one user. Selection Quantity is a different rule: it
    // does count a bot.
    [Theory]
    [InlineData("wf_cnd_user_count_in", 1, 1, true)]
    [InlineData("wf_cnd_user_count_in", 2, 5, false)]
    [InlineData("wf_cnd_not_user_count", 1, 1, false)]
    [InlineData("wf_cnd_not_user_count", 2, 5, true)]
    [InlineData("wf_cnd_not_user_count", 2, 2, true)]
    [InlineData("wf_cnd_not_user_count", 0, 0, true)]
    [InlineData("wf_cnd_not_user_count", 0, 1, false)]
    public void UserCountConditionsDoNotCountABot(string name, int minimum, int maximum, bool expected)
    {
        var (room, _, _) = World();
        var client = new FlashGameClient(TestGameServer.Instance, new FlashPacketFactory(), TestLogging.GameClient);
        client.SetHabbo(new Habbo { Id = 1, Username = "Objecto", CurrentRoom = room, Client = client });
        var owner = new RoomUser(1, 0, 1, room, client, TestChatEmotions.Unused, TestRewardProgress.Unused);
        owner.InitializePosition(1, 1, 0);
        var bot = new RoomUser(0, 0, 2, room, null, TestChatEmotions.Unused, TestRewardProgress.Unused) { BotData = (RoomBot)RuntimeHelpers.GetUninitializedObject(typeof(RoomBot)) };
        var box = Box(room, name, _ => null);
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor) with { OwnedIntParams = [minimum, maximum] };
        Assert.True(WiredNativeEditorProjection.TryCompile(box.Item.Id, box.Descriptor, native, out var proposed));
        Assert.True(box.TryValidateConfiguration(proposed, out var config, out var error), error);
        box.ApplyConfiguration(config);

        Assert.Equal(expected, box.Execute(Context(room, [], [owner, bot])));
    }
}
