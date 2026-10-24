using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

/// <summary>A link that receives from another room toggles its state before the speaker bubble and the receiver's flashes, and the delivery waits for a busy pass of that room.</summary>
public partial class ModernWiredRuntimeTests
{
    private static ConcurrentQueue<(uint Header, byte[] Body)> CaptureThreadSafe(TeleportFixture f)
    {
        var sent = new ConcurrentQueue<(uint Header, byte[] Body)>();
        f.Client.SendCallback = args =>
        {
            sent.Enqueue(((uint)FlashGameClient.DecodeInt16(args.MemoryBuffer.Slice(4, 2)), args.MemoryBuffer[6..].ToArray()));

            return true;
        };

        return sent;
    }

    private static string WireId(byte[] body) =>
        System.Text.Encoding.ASCII.GetString(body, 2, BinaryPrimitives.ReadUInt16BigEndian(body));

    private static string[] LinkPackets(ConcurrentQueue<(uint Header, byte[] Body)> sent, Item link) =>
    [
        .. sent.Where(packet => packet.Header is ServerPacketHeader.ObjectDataUpdateComposer or ServerPacketHeader.ChatComposer
                or ServerPacketHeader.ObjectsDataUpdateComposer)
            .Select(packet => packet.Header switch
            {
                ServerPacketHeader.ChatComposer => "bubble",
                ServerPacketHeader.ObjectDataUpdateComposer when WireId(packet.Body) == link.Id.ToString() => "link",
                _ => "flashes"
            })
    ];

    private static bool PassEnded(TeleportFixture f) => WaitFor(() => Volatile.Read(ref f.Room.CycleSlot) == 0);

    private static (TeleportFixture F, Item Receiver, ConcurrentQueue<(uint Header, byte[] Body)> Sent) ReceivingRoom(TeleportFixture a, CrossRoomLinkStore store)
    {
        var b = new TeleportFixture();
        b.Room.Id = 2;
        b.Room.OwnerId = 5;
        Assert.True(b.Engine.Remove(100));
        b.Room.GetWired().LinkStore = store;
        var receiver = LinkFurni(b, 70, 0, 2);
        store.Join(2, receiver.Id, 7);
        LinkTextReceiveMessage(b, receiver);
        LinkTextLog(b, "got [$(trigger_text)]");
        SpeakerAddon(b, 130, 1, 2, 1, 1, receiver);
        typeof(WiredStackEngine).GetField("_flash", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(b.Engine, (Action<Item>)b.Room.GetWired().OnEvent);
        LoadRooms(a.Room, b.Room);

        return (b, receiver, CaptureThreadSafe(b));
    }

    [Fact]
    public void AReceivingLinkInAnotherRoomTogglesBeforeTheBubbleAndTheFlashes()
    {
        var store = new CrossRoomLinkStore();
        var (a, _, _) = CrossRoom(1, 50, store, "hello");
        using var _a = a;
        var (b, receiver, sent) = ReceivingRoom(a, store);
        using var _b = b;

        a.Fire(land: false);

        Assert.True(WaitFor(() => LinkTextLogs(b).Length > 0));
        Assert.True(PassEnded(b));
        Assert.Equal("1", receiver.LegacyDataString);
        Assert.Equal(["link", "bubble", "flashes"], LinkPackets(sent, receiver));
    }

    [Fact]
    public void AReceivingLinkTogglesOncePerDelivery()
    {
        var store = new CrossRoomLinkStore();
        var (a, _, _) = CrossRoom(1, 50, store, "hello");
        using var _a = a;
        var (b, receiver, sent) = ReceivingRoom(a, store);
        using var _b = b;

        a.Fire(land: false);
        Assert.True(WaitFor(() => LinkTextLogs(b).Length == 1));
        Assert.True(PassEnded(b));
        a.Fire(land: false);
        Assert.True(WaitFor(() => LinkTextLogs(b).Length == 2));
        Assert.True(PassEnded(b));

        Assert.Equal("0", receiver.LegacyDataString);
        Assert.Equal(2, LinkPackets(sent, receiver).Count(kind => kind == "link"));
        Assert.Equal(2, LinkPackets(sent, receiver).Count(kind => kind == "bubble"));
    }

    [Fact]
    public void ALinkThatReceivesItsOwnMessageKeepsItsState()
    {
        var (f, link, store) = LinkRoom();
        using var _ = f;
        store.Join(link.Id, 7, true);
        LinkSend(f, "hello", link);
        LinkReceive(f, 120, 2, 2, link);
        LinkLog(f, 121, 2, 2, "got $(trigger_text)");
        var sent = CaptureThreadSafe(f);

        f.Fire(land: false);
        f.Advance(50);

        Assert.Equal(["got hello"], LinkLogs(f));
        Assert.Equal("0", link.LegacyDataString);
        Assert.DoesNotContain("link", LinkPackets(sent, link));
    }

    [Fact]
    public void ALinkMessageSentWhileTheTargetRoomPassIsRunningStillRunsAfterIt()
    {
        var store = new CrossRoomLinkStore();
        var (a, _, _) = CrossRoom(1, 50, store, "hello");
        using var _a = a;
        var (b, _, _) = CrossRoom(2, 60, store, "unused");
        using var _b = b;
        LoadRooms(a.Room, b.Room);
        Interlocked.Exchange(ref b.Room.CycleSlot, 1);

        a.Fire(land: false);
        Thread.Sleep(3);
        Interlocked.Exchange(ref b.Room.CycleSlot, 0);

        Assert.True(WaitFor(() => LinkTextLogs(b).Length > 0), "The event did not run once the pass ended.");
    }

    [Fact]
    public void ALinkMessageStillRunsAfterALongPassOfTheTargetRoom()
    {
        var store = new CrossRoomLinkStore();
        var (a, _, _) = CrossRoom(1, 50, store, "hello");
        using var _a = a;
        var (b, _, _) = CrossRoom(2, 60, store, "unused");
        using var _b = b;
        LoadRooms(a.Room, b.Room);
        Interlocked.Exchange(ref b.Room.CycleSlot, 1);

        a.Fire(land: false);
        Thread.Sleep(400);
        Assert.Empty(LinkTextLogs(b));
        Interlocked.Exchange(ref b.Room.CycleSlot, 0);

        Assert.True(WaitFor(() => LinkTextLogs(b).Length > 0), "The event did not run once the pass ended.");
    }

    [Fact]
    public void AFullTickOfTheReceivingRoomShowsTheLinkAndTheBubbleBeforeTheFlashes()
    {
        var store = new CrossRoomLinkStore();
        var (a, _, _) = CrossRoom(1, 50, store, "hello");
        using var _a = a;
        var (b, receiver, sent) = ReceivingRoom(a, store);
        using var _b = b;
        Interlocked.Exchange(ref b.Room.CycleSlot, 2);

        a.Fire(land: false);
        Thread.Sleep(100);

        using (RoomOwnerScope.Enter(b.Room)) {
            b.Room.GetWired().OnCycle();
        }

        Interlocked.Exchange(ref b.Room.CycleSlot, 0);
        Assert.True(WaitFor(() => LinkTextLogs(b).Length > 0));
        Assert.True(PassEnded(b));
        Assert.Equal("1", receiver.LegacyDataString);
        Assert.Equal(["link", "bubble", "flashes"], LinkPackets(sent, receiver));
    }

    [Fact]
    public void ManyLinkMessagesToABusyRoomStartOnePassAfterItEnds()
    {
        var store = new CrossRoomLinkStore();
        var (a, _, _) = CrossRoom(1, 50, store, "hello");
        using var _a = a;
        var (b, receiver, sent) = ReceivingRoom(a, store);
        using var _b = b;
        Interlocked.Exchange(ref b.Room.CycleSlot, 1);

        for (var i = 0; i < 20; i++) {
            a.Fire(land: false);
        }

        Thread.Sleep(100);
        var passes = new HashSet<Task>();
        var before = b.Room.ProcessTask;
        Interlocked.Exchange(ref b.Room.CycleSlot, 0);
        var clock = Stopwatch.StartNew();

        while (clock.ElapsedMilliseconds < 300) {
            if (b.Room.ProcessTask is { } pass && !ReferenceEquals(pass, before)) {
                passes.Add(pass);
            }

            Thread.SpinWait(20);
        }

        Assert.Equal(20, LinkTextLogs(b).Length);
        Assert.Equal(20, LinkPackets(sent, receiver).Count(kind => kind == "link"));
        Assert.Single(passes);
    }

    [Fact]
    public void ALinkMessageDoesNotRunInARoomThatUnloadsWhileItsPassIsRunning()
    {
        var store = new CrossRoomLinkStore();
        var (a, _, _) = CrossRoom(1, 50, store, "hello");
        using var _a = a;
        var (b, _, _) = CrossRoom(2, 60, store, "unused");
        using var _b = b;
        LoadRooms(a.Room, b.Room);
        Interlocked.Exchange(ref b.Room.CycleSlot, 1);

        a.Fire(land: false);
        b.Room.MDisposed = true;
        Thread.Sleep(3);
        Interlocked.Exchange(ref b.Room.CycleSlot, 0);

        Assert.False(WaitFor(() => LinkTextLogs(b).Length > 0, 300));
    }
}
