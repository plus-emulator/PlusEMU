using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    private static string[] LinkLogs(TeleportFixture f) => [.. f.Room.GetWired().ReadLogs(1, 50).Entries.Select(entry => entry.Reason)];

    private static void LinkLog(TeleportFixture f, uint id, int x, int y, string text)
    {
        var log = Assert.IsType<WiredModernAction>(f.Room.GetWired().CreateConfiguredBox(Placed(f, MakeItem(id, "wf_act_log"), x, y), Descriptor("wf_act_log")));
        Assert.True(WiredNativeEditorProjection.TryCompile(id, Descriptor("wf_act_log"),
            WiredNativeEditorProjection.DefaultNative(Descriptor("wf_act_log")) with { OwnedIntParams = [0], Text = text }, out var logged));
        log.ApplyConfiguration(logged);
        Assert.True(f.Engine.Add(log));
    }

    private static void LinkSend(TeleportFixture f, string text, Item link) =>
        OctoberBox<WiredModernAction>(f, 110, "wf_act_send_message", 0, 0, native => native with
        {
            OwnedIntParams = [1],
            Text = text,
            PrimaryItems = [new(link.Id, false)],
            FurniSourceTypes = [100]
        });

    private static void LinkReceive(TeleportFixture f, uint id, int x, int y, Item link) =>
        OctoberBox<WiredModernTrigger>(f, id, "wf_trg_receive_message", x, y, native => native with
        {
            PrimaryItems = [new(link.Id, false)],
            FurniSourceTypes = [100]
        });

    private static (TeleportFixture F, Item Link, MemoryLinkStore Store) LinkRoom()
    {
        var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        var store = new MemoryLinkStore();
        f.Room.GetWired().LinkStore = store;

        return (f, LinkFurni(f, 50, 0, 1), store);
    }

    [Fact]
    public void SelfReceivingLinkHearsItsOwnMessageWithANetwork()
    {
        var (f, link, store) = LinkRoom();
        using var _ = f;
        store.Join(link.Id, 7, true);
        LinkSend(f, "hello", link);
        LinkReceive(f, 120, 2, 2, link);
        LinkLog(f, 121, 2, 2, "got $(trigger_text)");

        f.Fire(land: false);
        f.Advance(50);

        Assert.Equal(["got hello"], LinkLogs(f));
    }

    [Fact]
    public void SelfReceivingLinkHearsItsOwnMessageWithoutANetwork()
    {
        var (f, link, store) = LinkRoom();
        using var _ = f;
        store.Join(link.Id, 0, true);
        LinkSend(f, "hello", link);
        LinkReceive(f, 120, 2, 2, link);
        LinkLog(f, 121, 2, 2, "got $(trigger_text)");

        f.Fire(land: false);
        f.Advance(50);

        Assert.Equal(["got hello"], LinkLogs(f));
    }

    [Fact]
    public void LinkWithoutSelfReceivingHearsNothingWithOrWithoutANetwork()
    {
        var (f, link, store) = LinkRoom();
        using var _ = f;
        LinkSend(f, "hello", link);
        LinkReceive(f, 120, 2, 2, link);
        LinkLog(f, 121, 2, 2, "got $(trigger_text)");

        foreach (var network in new uint[] { 7, 0 }) {
            store.Join(link.Id, network, false);
            f.Fire(land: false);
            f.Advance(50);
        }

        Assert.Empty(LinkLogs(f));
    }

    [Fact]
    public void ReceiveAndResendLoopRunsTenTimesInTotal()
    {
        var (f, link, store) = LinkRoom();
        using var _ = f;
        store.Join(link.Id, 0, true);
        LinkSend(f, "ping", link);
        LinkReceive(f, 120, 2, 2, link);
        OctoberBox<WiredModernAction>(f, 122, "wf_act_send_message", 2, 2, native => native with
        {
            OwnedIntParams = [1],
            Text = "pong",
            PrimaryItems = [new(link.Id, false)],
            FurniSourceTypes = [100]
        });
        LinkLog(f, 121, 2, 2, "run");

        f.Fire(land: false);

        for (var i = 0; i < 15; i++) {
            f.Advance(500);
        }

        Assert.Equal(10, LinkLogs(f).Length);
    }
}
