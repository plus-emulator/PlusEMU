using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: Bot Reaches Furni fires right after the UserUpdate that announces the bot's step onto
// the furni, also for a furni the bot only walks over, and not for a Bot Move to the furni the bot already stands on.
public partial class PlacedFurniRoomTests
{
    private List<(WiredEventKind Kind, RoomUser? Actor, Item? Item)> ObserveBotReached(uint id, int x, int y, Item picked)
    {
        var fired = new List<(WiredEventKind Kind, RoomUser? Actor, Item? Item)>();
        var triggerItem = Furni(id, InteractionType.WiredTrigger, WiredBoxType.None);
        var actionItem = Furni(id + 1, InteractionType.WiredEffect, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, triggerItem, x, y, 0, true, false, false));
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, actionItem, x, y, 0, true, false, false));
        Assert.True(WiredBoxRegistry.TryGet("wf_trg_bot_reached_stf", out var descriptor));
        var box = new WiredModernTrigger(_room, triggerItem, descriptor);
        var draft = WiredTriggerConfiguration.Defaults("wf_trg_bot_reached_stf") with { SelectedItems = [picked.Id], Text = "Bot" };
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(box, draft, out var config, out var error), error);
        box.ApplyConfiguration(config);
        Assert.True(_room.GetWired().AddBox(box));
        Assert.True(_room.GetWired().AddBox(new WalkRecordingAction(_room, actionItem, fired)));

        return fired;
    }

    [Fact]
    public void BotReachesFurniFiresInTheTickThatAnnouncesTheLastStep()
    {
        var landing = ExecutorFloor(11, 1, 1);
        var fired = ObserveBotReached(22, 3, 0, landing);
        var bot = WalkActor(WalkActorKind.Bot, 0, 1, v2: true);

        bot.MoveTo(1, 1);
        AnnounceTick();

        Assert.Equal((0, 1), (bot.X, bot.Y));
        Assert.True(bot.HasStatus("mv"));
        Assert.Equal(new[] { (WiredEventKind.BotReachedFurni, landing.Id) }, fired.Select(x => (x.Kind, x.Item!.Id)).ToArray());
        Assert.Same(bot, fired[0].Actor);

        AnnounceTick();
        AnnounceTick();

        Assert.Equal((1, 1), (bot.X, bot.Y));
        Assert.Single(fired);
    }

    [Fact]
    public void BotReachesFurniFiresForAFurniTheBotWalksOver()
    {
        var passed = ExecutorFloor(11, 1, 1);
        var fired = ObserveBotReached(22, 3, 0, passed);
        var bot = WalkActor(WalkActorKind.Bot, 0, 1, v2: true);

        bot.MoveTo(2, 1);
        AnnounceTick();

        Assert.Equal(new[] { (WiredEventKind.BotReachedFurni, passed.Id) }, fired.Select(x => (x.Kind, x.Item!.Id)).ToArray());

        for (var tick = 0; tick < 4; tick++) {
            AnnounceTick();
        }

        Assert.Equal((2, 1), (bot.X, bot.Y));
        Assert.Single(fired);
    }

    [Fact]
    public void BotMoveOntoTheFurniFiresOnceWithTheAnnounce()
    {
        var landing = ExecutorFloor(11, 1, 1);
        var fired = ObserveBotReached(22, 3, 0, landing);
        var bot = WalkActor(WalkActorKind.Bot, 0, 1, v2: true);

        WiredBotTargets.For(_room).Walk(bot, landing);
        bot.MoveTo(1, 1);
        AnnounceTick();

        Assert.Single(fired);

        for (var tick = 0; tick < 4; tick++) {
            AnnounceTick();
        }

        Assert.Equal((1, 1), (bot.X, bot.Y));
        Assert.Single(fired);
        Assert.False(WiredBotTargets.For(_room).HasTargets);
    }

    [Fact]
    public void BotMoveToTheFurniTheBotStandsOnDoesNotFire()
    {
        var landing = ExecutorFloor(11, 1, 1);
        var fired = ObserveBotReached(22, 3, 0, landing);
        var bot = WalkActor(WalkActorKind.Bot, 1, 1, v2: true);

        WiredBotTargets.For(_room).Walk(bot, landing);
        bot.MoveTo(1, 1);

        for (var tick = 0; tick < 4; tick++) {
            AnnounceTick();
        }

        Assert.Equal((1, 1), (bot.X, bot.Y));
        Assert.Empty(fired);
        Assert.False(WiredBotTargets.For(_room).HasTargets);
    }
}
