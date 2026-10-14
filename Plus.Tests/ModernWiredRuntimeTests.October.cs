using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Links;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

/// <summary>The October boxes (r88) firing in a room: furni visuals and link delivery between WIRED Link furni.</summary>
public partial class ModernWiredRuntimeTests
{
    [Fact]
    public void SetFurniTransparencyShowsThePickedFurniToTheAudience()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        // Audience: the triggering user; targets: the picked furni; invisible, click through, easing 2.
        OctoberBox<WiredModernAction>(f, 110, "wf_act_set_visibility", 0, 0, native => native with
        {
            OwnedIntParams = [0, 100, 1, 2, 0], PrimaryItems = [new(1, false)], FurniSourceTypes = [100], UserSourceTypes = [0, 0]
        });
        var sent = Capture(f.Client);

        f.Fire(land: false);

        var packet = Reply(sent, ServerPacketHeader.WiredFurniTransparencyComposer);
        Assert.Equal((1, 1, 100, true, 2), (packet.Int(), packet.Int(), packet.Int(), packet.Bool(), packet.Int()));
        packet.End();
    }

    [Fact]
    public void SetFurniHighlightSendsBrightenAsAWhitePulseAtTheStrengthTheEditorShowed()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        OctoberBox<WiredModernAction>(f, 110, "wf_act_set_highlight", 0, 0, native => native with
        {
            OwnedIntParams = [0, 4, 38, 123, 0, 3, 0], PrimaryItems = [new(1, false)], FurniSourceTypes = [100], UserSourceTypes = [0, 0]
        });
        var sent = Capture(f.Client);

        f.Fire(land: false);

        var packet = Reply(sent, ServerPacketHeader.WiredFurniHighlightComposer);
        // Saved 38 (class_4214.as:117 stores On 76 / 2); the room gets 76.
        Assert.Equal((1, 1, 4, 76, 0xFFFFFF, 1, 0), (packet.Int(), packet.Int(), packet.Int(), packet.Int(), packet.Int(), packet.Int(), packet.Int()));
        packet.End();
    }

    [Fact]
    public void SetFurniHighlightOnTargetUsersSendsTheUserHighlightPacket()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        // Target users (the triggering user, room index 7), audience the triggering user; colour tint 50.
        OctoberBox<WiredModernAction>(f, 110, "wf_act_set_highlight", 0, 0, native => native with
        {
            OwnedIntParams = [0, 3, 50, 0xFF0000, 0, 2, 1], FurniSourceTypes = [100], UserSourceTypes = [0, 0]
        });
        var sent = Capture(f.Client);

        f.Fire(land: false);

        var packet = Reply(sent, ServerPacketHeader.WiredUserHighlightComposer);
        Assert.Equal((1, 7, 3, 50, 0xFF0000, 0, 2), (packet.Int(), packet.Int(), packet.Int(), packet.Int(), packet.Int(), packet.Int(), packet.Int()));
        packet.End();
    }

    [Fact]
    public void RoomWideVisualsAreReplayedToUsersEnteringAndForgottenWithTheirFurni()
    {
        using var f = new TeleportFixture();
        var wired = f.Room.GetWired();
        wired.RememberTransparency(false, [1, 2], 100, true, 3);
        wired.RememberTransparency(false, [2], 0, false, 0);
        wired.RememberHighlight(true, [7], 4, 76, 0xFFFFFF, 1, 0);
        var sent = Capture(f.Client);

        wired.SendVisualizationState(f.Client);

        var furni = Reply(sent, ServerPacketHeader.WiredFurniVisualizationStateComposer);
        Assert.Equal((1, 1, 100, true, 3, 0), (furni.Int(), furni.Int(), furni.Int(), furni.Bool(), furni.Int(), furni.Int()));
        furni.End();
        var users = Reply(sent, ServerPacketHeader.WiredUserVisualizationStateComposer);
        Assert.Equal((0, 1, 7, 4, 76, 0xFFFFFF, 1, 0), (users.Int(), users.Int(), users.Int(), users.Int(), users.Int(), users.Int(), users.Int(), users.Int()));
        users.End();

        wired.DetachRoomItem(f.Target);
        wired.RememberHighlight(true, [7], 4, 0, 0, 0, 0);
        wired.SendVisualizationState(f.Client);
        Assert.Empty(sent);
    }

    [Fact]
    public void LinkMessageReachesAReceiverOfTheSameNetworkThatReceivesLocally()
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        var store = new MemoryLinkStore();
        f.Room.GetWired().LinkStore = store;
        var sender = LinkFurni(f, 50, 3, 0);
        var receiver = LinkFurni(f, 51, 3, 2);
        store.Join(sender.Id, 7, false);
        store.Join(receiver.Id, 7, true);

        OctoberBox<WiredModernAction>(f, 110, "wf_act_send_message", 0, 0, native => native with
        {
            OwnedIntParams = [1], Text = "door open", PrimaryItems = [new(sender.Id, false)], FurniSourceTypes = [100]
        });
        OctoberBox<WiredModernTrigger>(f, 120, "wf_trg_receive_message", 2, 2, native => native with
        {
            PrimaryItems = [new(receiver.Id, false)], FurniSourceTypes = [100]
        });
        var log = Assert.IsType<WiredModernAction>(f.Room.GetWired().CreateConfiguredBox(Placed(f, MakeItem(121, "wf_act_log"), 2, 2), Descriptor("wf_act_log")));
        Assert.True(WiredNativeEditorProjection.TryCompile(121, Descriptor("wf_act_log"),
            WiredNativeEditorProjection.DefaultNative(Descriptor("wf_act_log")) with { OwnedIntParams = [0], Text = "got $(trigger_text)" }, out var logged));
        log.ApplyConfiguration(logged);
        Assert.True(f.Engine.Add(log));

        f.Fire(land: false);
        f.Advance(50);

        Assert.Equal("got door open", f.Room.GetWired().ReadLogs(1, 50).Entries.Single().Reason);

        // Without "receive local" the furni of the sending room hears nothing.
        store.Join(receiver.Id, 7, false);
        f.Room.GetWired().ClearLogs();
        f.Fire(land: false);
        f.Advance(50);
        Assert.Empty(f.Room.GetWired().ReadLogs(1, 50).Entries);
    }

    private static Item Placed(TeleportFixture f, Item item, int x, int y)
    {
        item.SetState(x, y, 0, Gamemap.GetAffectedTiles(1, 1, x, y, 0));
        typeof(Item).GetField("_room", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(item, f.Room);
        item.RoomId = f.Room.Id;
        f.Items[item.Id] = item;

        return item;
    }

    private static Item LinkFurni(TeleportFixture f, uint id, int x, int y)
    {
        var item = MakeItem(id, "wf_link");
        item.Definition.InteractionName = WiredLinkTransport.Interaction;

        return Placed(f, item, x, y);
    }

    private static T OctoberBox<T>(TeleportFixture f, uint id, string name, int x, int y,
        Func<WiredNativeEditorConfiguration, WiredNativeEditorConfiguration> edit) where T : class, IWiredConfiguredItem
    {
        var descriptor = Descriptor(name);
        var box = Assert.IsType<T>(f.Room.GetWired().CreateConfiguredBox(Placed(f, MakeItem(id, name), x, y), descriptor));
        Assert.True(WiredNativeEditorProjection.TryCompile(id, descriptor, edit(WiredNativeEditorProjection.DefaultNative(descriptor)), out var runtime));
        box.ApplyConfiguration(runtime);
        Assert.True(f.Engine.Add(box));

        return box;
    }

    private sealed class MemoryLinkStore : IWiredLinkStore
    {
        private readonly Dictionary<uint, WiredLinkEndpoint> _endpoints = [];
        public void Join(uint itemId, uint networkId, bool receiveLocal) => _endpoints[itemId] = new(itemId, networkId, receiveLocal);
        public WiredLinkEndpoint? Endpoint(uint itemId) => _endpoints.GetValueOrDefault(itemId);
        public IReadOnlyList<WiredLinkConnection> Members(uint networkId) =>
            [.. _endpoints.Values.Where(endpoint => endpoint.NetworkId == networkId).Select(endpoint => new WiredLinkConnection(0, "", endpoint.ItemId, endpoint.ReceiveLocal))];
        public IReadOnlyList<WiredLinkNetwork> Networks(int ownerId, uint roomId) => throw new NotSupportedException();
        public IReadOnlyList<WiredLinkConnection> Connections(int ownerId, uint networkId) => throw new NotSupportedException();
        public WiredLinkResult Select(int ownerId, uint itemId, uint roomId, uint networkId, bool receiveLocal) => throw new NotSupportedException();
        public WiredLinkResult Create(int ownerId, string name, out uint networkId) => throw new NotSupportedException();
        public WiredLinkResult Rename(int ownerId, uint networkId, string name) => throw new NotSupportedException();
        public WiredLinkResult Delete(int ownerId, uint networkId, out IReadOnlyList<WiredLinkConnection> detached) => throw new NotSupportedException();
    }
}
