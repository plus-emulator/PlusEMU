using System.Buffers.Binary;
using System.Reflection;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

/// <summary>A Receive Message stack runs in the pass of the Send Message that reached it, and its flashes share the sender's ObjectsDataUpdate.</summary>
public partial class ModernWiredRuntimeTests
{
    private const uint LinkSenderTrigger = 112;
    private const uint LinkSenderLog = 111;
    private const uint LinkReceiverTrigger = 120;
    private const uint LinkReceiverLog = 121;

    private static void LinkBatchLog(TeleportFixture f, uint id, int x, int y, string text)
    {
        var log = Assert.IsType<WiredModernAction>(f.Room.GetWired().CreateConfiguredBox(Placed(f, MakeItem(id, "wf_act_log"), x, y), Descriptor("wf_act_log")));
        Assert.True(WiredNativeEditorProjection.TryCompile(id, Descriptor("wf_act_log"),
            WiredNativeEditorProjection.DefaultNative(Descriptor("wf_act_log")) with { OwnedIntParams = [0], Text = text }, out var logged));
        log.ApplyConfiguration(logged);
        Assert.True(f.Engine.Add(log));
    }

    private static string[] LinkBatchLogs(TeleportFixture f) => [.. f.Room.GetWired().ReadLogs(1, 50).Entries.Select(entry => entry.Reason)];

    private static void LinkBatchSend(TeleportFixture f, string text, uint link) =>
        OctoberBox<WiredModernAction>(f, 110, "wf_act_send_message", 0, 0, native => native with
        {
            OwnedIntParams = [1],
            Text = text,
            PrimaryItems = [new(link, false)],
            FurniSourceTypes = [100]
        });

    private static void LinkBatchReceive(TeleportFixture f, uint id, int x, int y, uint link) =>
        OctoberBox<WiredModernTrigger>(f, id, "wf_trg_receive_message", x, y, native => native with
        {
            PrimaryItems = [new(link, false)],
            FurniSourceTypes = [100]
        });

    private static TeleportFixture LinkBatchRoom(Action<TeleportFixture> senderTrigger, out List<(uint Header, byte[] Body)> sent)
    {
        var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        var store = new MemoryLinkStore();
        f.Room.GetWired().LinkStore = store;
        var sender = LinkFurni(f, 50, 0, 1);
        var receiver = LinkFurni(f, 51, 0, 2);
        store.Join(sender.Id, 7, false);
        store.Join(receiver.Id, 7, true);
        senderTrigger(f);
        LinkBatchSend(f, "now", sender.Id);
        LinkBatchLog(f, LinkSenderLog, 0, 0, "sent");
        LinkBatchReceive(f, LinkReceiverTrigger, 2, 2, receiver.Id);
        LinkBatchLog(f, LinkReceiverLog, 2, 2, "got $(trigger_text)");
        typeof(WiredStackEngine).GetField("_flash", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Engine, (Action<Item>)f.Room.GetWired().OnEvent);
        var frames = new List<(uint Header, byte[] Body)>();
        f.Client.SendCallback = args =>
        {
            frames.Add(((uint)FlashGameClient.DecodeInt16(args.MemoryBuffer.Slice(4, 2)), args.MemoryBuffer[6..].ToArray()));

            return true;
        };
        sent = frames;

        return f;
    }

    private static void LinkBatchClickTrigger(TeleportFixture f) =>
        OctoberBox<WiredModernTrigger>(f, LinkSenderTrigger, "wf_trg_click_furni", 0, 0, native => native with
        {
            PrimaryItems = [new(f.Target.Id, false)],
            FurniSourceTypes = [100]
        });

    private static void LinkBatchSaysTrigger(TeleportFixture f)
    {
        var says = new WiredModernTrigger(f.Room, Placed(f, MakeItem(LinkSenderTrigger, "wf_trg_says_something"), 0, 0), Descriptor("wf_trg_says_something"));
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(says, new() { IntParams = [0, 0, 0], Text = "go1" }, out var config, out var error), error);
        says.ApplyConfiguration(config);
        Assert.True(f.Engine.Add(says));
    }

    private static List<uint[]> LinkBatches(List<(uint Header, byte[] Body)> sent) =>
    [
        .. sent.Where(packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer).Select(packet =>
        {
            var count = BinaryPrimitives.ReadInt32BigEndian(packet.Body);
            var offset = 4;
            var ids = new uint[count];

            for (var i = 0; i < count; i++) {
                ids[i] = BinaryPrimitives.ReadUInt32BigEndian(packet.Body.AsSpan(offset));
                offset += 10 + BinaryPrimitives.ReadUInt16BigEndian(packet.Body.AsSpan(offset + 8));
            }

            return ids;
        })
    ];

    private static void AssertSharedLinkBatch(TeleportFixture f, List<(uint Header, byte[] Body)> sent)
    {
        var batches = LinkBatches(sent);
        var batch = Assert.Single(batches, ids => ids.Contains(LinkSenderTrigger));
        Assert.Contains(LinkReceiverTrigger, batch);
        Assert.Contains(LinkReceiverLog, batch);
        Assert.Equal(["got now", "sent"], LinkBatchLogs(f).Order());
    }

    [Fact]
    public void LinkReceiverFlashesInTheBatchOfAClickSender()
    {
        using var f = LinkBatchRoom(LinkBatchClickTrigger, out var sent);

        using (RoomOwnerScope.Enter(f.Room)) {
            f.Room.GetWired().Dispatch(new WiredRuntimeEvent(WiredEventKind.ClickFurni) { Actor = f.User, EventItem = f.Target });
        }

        f.Advance(500);

        AssertSharedLinkBatch(f, sent);
    }

    [Fact]
    public void LinkReceiverFlashesInTheBatchOfASaysSender()
    {
        using var f = LinkBatchRoom(LinkBatchSaysTrigger, out var sent);

        using (RoomOwnerScope.Enter(f.Room)) {
            f.Room.GetWired().TriggerEvent(WiredBoxType.TriggerUserSays, f.Habbo, "go1", 0, false);
            f.Room.GetWired().FinishSpeech();
        }

        f.Advance(500);

        AssertSharedLinkBatch(f, sent);
    }

    [Fact]
    public void QueuedEventsOtherThanLinksStillRunOnTheNextRoomTick()
    {
        using var f = LinkBatchRoom(LinkBatchClickTrigger, out var sent);
        OctoberBox<WiredModernTrigger>(f, 130, "wf_trg_enter_room", 4, 4, native => native);
        LinkBatchLog(f, 131, 4, 4, "entered");

        using (RoomOwnerScope.Enter(f.Room)) {
            Assert.True(f.Room.GetWired().Dispatch(new WiredRuntimeEvent(WiredEventKind.Enter) { Actor = f.User }));
            f.Engine.OnFastCycle();
        }

        Assert.DoesNotContain("entered", LinkBatchLogs(f));
        f.Advance(500);
        Assert.Contains("entered", LinkBatchLogs(f));
    }
}
