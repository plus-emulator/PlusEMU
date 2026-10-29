using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI.Speech;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

// A stack without Execute in Order that moves a furni to a user and raises it with Change Variable sends one ObjectUpdate with the final state.
public partial class PlacedFurniRoomTests
{
    private const uint ClickUpdateTrigger = 975;

    [Fact]
    public async Task ChangeVariableAltitudeAfterFurniToUserWithoutExecuteInOrderSendsOneObjectUpdate()
    {
        var (actor, tile) = await OneUpdateRoom(noAnimation: true, executeInOrder: false);
        Stand(actor, 0, 3);

        FireRuntimeStack(actor);

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Equal([(0, 3, 0, "0.3")], SentObjectStates());
    }

    [Fact]
    public async Task AClickOnABotSendsOneObjectUpdateForAFurniMovedAndRaisedWithoutExecuteInOrder()
    {
        var (actor, tile) = await OneUpdateRoom(noAnimation: true, executeInOrder: false);
        Stand(actor, 0, 3);
        var trigger = WiredBox(ClickUpdateTrigger, "wf_trg_click_user", 1, 1);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(trigger, new() { IntParams = [0, 0] }, out var config, out var error), error);
        trigger.ApplyConfiguration(config);
        trigger.Item.GetZ = 0.1;
        Assert.True(_room.GetWired().AddBox(trigger));
        var speech = new List<RandomSpeech>();
        var bot = _room.GetRoomUserManager().DeployBot(new(1, 0, "generic", "stand", "Robbie", "", "hr-100", 3, 3, 0, 6, 0, 0, 0, 0, ref speech, "M", 0,
            7, false, 7, false, 0), null!);
        var clock = new GridClock();
        var wired = _room.GetWired();
        typeof(WiredComponent).GetField("_clock", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(wired, clock);
        var engine = typeof(WiredComponent).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wired)!;
        engine.GetType().GetField("_now", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, new Func<long>(() => clock.Ms));
        var manager = GridManager(clock);
        RunRoom(manager, clock, clock.Ms + 1000, period: 25);
        _client.Packets.Clear();

        await new ClickUserEvent().Parse(_room, _client, ClientPacket(bot.VirtualId));
        var atClick = _client.Packets.ToArray();
        var sent = atClick.Concat(RunRoom(manager, clock, clock.Ms + 1500, period: 25).SelectMany(pass => pass.Packets)).ToArray();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Single(sent, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }
}
