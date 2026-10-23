using System.Drawing;
using System.Reflection;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    // Bot Teleport fires Bot Reaches Furni once, in the tick that republishes the bot. That is one room tick before the bot shows on the furni.
    [Fact]
    public void BotTeleportFiresBotReachesFurniWhenTheBotIsRepublished()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_teleport", intParams: [100, 100], text: "Bobby");
        var bot = AddNamedBot(f, 9, "Bobby", 2, 2);
        MakeTargetWalkable(f);
        var triggerItem = MakeItem(104, "wf_trg_bot_reached_stf");
        typeof(Plus.HabboHotel.Items.Item).GetField("_room", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(triggerItem, f.Room);
        triggerItem.RoomId = f.Room.Id;
        triggerItem.SetState(2, 0, 0);
        var trigger = new WiredModernTrigger(f.Room, triggerItem, Descriptor("wf_trg_bot_reached_stf"));
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(trigger,
            WiredTriggerConfiguration.Defaults("wf_trg_bot_reached_stf") with { SelectedItems = [1], Text = "Bobby" }, out var config, out var error), error);
        trigger.ApplyConfiguration(config);
        f.Items[104] = triggerItem;
        Assert.True(f.Engine.Add(trigger));
        var captureItem = MakeItem(105, "wf_act_toggle_state");
        captureItem.SetState(2, 0, 0);
        var capture = new SelectionCapture(f.Room, captureItem);
        f.Items[105] = captureItem;
        Assert.True(f.Engine.Add(capture));

        f.Fire(land: false);
        f.Advance(1499);

        Assert.Empty(capture.Contexts);

        f.Advance(1);

        Assert.Same(bot, Assert.Single(capture.Contexts).Event.Actor);
        Assert.Equal(new Point(2, 2), bot.Coordinate);

        f.Advance(1000);

        Assert.Equal(new Point(1, 1), bot.Coordinate);
        Assert.Single(capture.Contexts);
        Assert.Empty(f.Errors);
    }
}
