using System.Runtime.CompilerServices;
using Plus.Communication.Flash;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredConditionParityTests
{
    // Observed on AIR WIN63-202610201032-107964540: the name of a user is compared with the case kept ("objecto" does not match
    // "Objecto"), and the name of a bot is compared without regard to case ("robbie" matches "Robbie").
    [Theory]
    [InlineData(false, "Objecto", true)]
    [InlineData(false, "objecto", false)]
    [InlineData(false, "OBJECTO", false)]
    [InlineData(true, "Robbie", true)]
    [InlineData(true, "robbie", true)]
    [InlineData(true, "ROBBIE", true)]
    [InlineData(true, "Other", false)]
    public void TriggererMatchComparesUserNamesWithCaseAndBotNamesWithoutCase(bool bot, string text, bool expected)
    {
        Assert.Equal((expected, !expected), (MatchesName("wf_cnd_triggerer_match", bot, text), MatchesName("wf_cnd_not_triggerer_match", bot, text)));
    }

    private static bool MatchesName(string name, bool bot, string text)
    {
        var (room, _, _) = World();
        RoomUser triggerer;

        if (bot) {
            var data = (RoomBot)RuntimeHelpers.GetUninitializedObject(typeof(RoomBot));
            data.Name = "Robbie";
            data.AiType = BotAiType.Generic;
            triggerer = new RoomUser(0, 0, 2, room, null, TestChatEmotions.Unused, TestRewardProgress.Unused) { BotData = data };
        }
        else {
            var client = new FlashGameClient(TestGameServer.Instance, new FlashPacketFactory(), TestLogging.GameClient);
            client.SetHabbo(new Habbo { Id = 1, Username = "Objecto", CurrentRoom = room, Client = client });
            triggerer = new RoomUser(1, 0, 1, room, client, TestChatEmotions.Unused, TestRewardProgress.Unused);
        }

        triggerer.InitializePosition(1, 1, 0);
        var box = Box(room, name, _ => null);
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor) with
        {
            OwnedIntParams = [bot ? 4 : 1],
            UserSourceTypes = [0, 101],
            Text = text
        };
        Assert.True(WiredNativeEditorProjection.TryCompile(box.Item.Id, box.Descriptor, native, out var proposed));
        Assert.True(box.TryValidateConfiguration(proposed, out var config, out var error), error);
        box.ApplyConfiguration(config);
        var context = Context(room, [], [triggerer]);
        context.Triggering.UserIds.Add(triggerer.VirtualId);

        return box.Execute(context);
    }
}
