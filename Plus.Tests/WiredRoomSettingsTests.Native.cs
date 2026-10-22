using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.Communication.Revisions;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Settings;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610151745-318700621: WiredGetRoomSettings (3215, no fields) and WiredSetRoomSettings
// (2035: int modify mask, int read mask, string timezone) are both answered with WiredRoomSettings
// (3319: int modify mask, int read mask, string timezone; an empty timezone means the room has no own timezone).
// Volt keeps its own save (65465, same fields) and its own settings frame (5102).
public partial class WiredRoomSettingsTests
{
    private const uint NativeGetId = 3320;
    private const uint NativeSetId = 2172;
    private const uint NativeSettingsId = 2114;
    private const uint VoltSaveId = 65465;

    [Fact]
    public void NativeSettingsFrameCarriesModifyMaskReadMaskAndTimezone()
    {
        var room = Room();
        var owner = Client(room, 1);
        var configured = new WiredRoomSettings(room, new MemoryStore { Saved = new(15, 14, "Europe/Berlin") });
        var defaults = new WiredRoomSettings(room, new MemoryStore());

        var frame = new WiredRoomSettingsComposer(configured.View(owner));
        Assert.Equal(NativeSettingsId, frame.MessageId);
        Assert.Equal((14, 15, "Europe/Berlin"), NativeFields(frame));

        // A room without a saved row keeps the Plus defaults and has no own timezone.
        Assert.Equal((2, 2, ""), NativeFields(new WiredRoomSettingsComposer(defaults.View(owner))));
    }

    [Fact]
    public async Task NativeGetIsAnsweredWithTheNativeSettingsFrameForEveryoneTheCustomRequestAnswers()
    {
        var room = Room();
        var store = new MemoryStore { Saved = new(15, 14, "") };
        Register(room, store);
        var (owner, ownerPackets) = NativeClient(room, 1);
        var (guest, guestPackets) = NativeClient(room, 4);

        await new WiredGetRoomSettingsEvent(Service()).Parse(room, owner, HabbiconTestSupport.Incoming());
        await new WiredGetRoomSettingsEvent(Service()).Parse(room, guest, HabbiconTestSupport.Incoming());

        Assert.Equal((14, 15, ""), NativeFrame(Assert.Single(ownerPackets)));
        Assert.Equal((14, 15, ""), NativeFrame(Assert.Single(guestPackets)));

        ownerPackets.Clear();
        await new WiredGetRoomSettingsEvent(Service()).Parse(room, owner, HabbiconTestSupport.Incoming(1));
        Assert.Empty(ownerPackets);
    }

    [Fact]
    public async Task NativeSetSavesThroughTheSettingsRulesAndAnswersWithTheSavedNativeFrame()
    {
        var room = Room();
        var store = new MemoryStore { Saved = new(2, 2, "") };
        var settings = Register(room, store);
        var users = Users(room);
        var (owner, ownerPackets) = NativeClient(room, 1, users, 1);

        await new WiredSetRoomSettingsEvent(Service()).Parse(room, owner, HabbiconTestSupport.Incoming(4, 1, "UTC"));

        // Modify bit 2 forces bit 3, and every modifier may read.
        Assert.Equal(new(13, 12, "UTC"), store.Saved);
        Assert.Equal(store.Saved, settings.Snapshot);
        Assert.Equal((12, 13, "UTC"), NativeFrame(ownerPackets[0]));
        Assert.DoesNotContain(ownerPackets, packet => packet.Id == ServerPacketHeader.WiredRoomSettingsDataComposer);
    }

    [Fact]
    public async Task RejectedNativeSetAnswersWithTheUnchangedNativeFrame()
    {
        var room = Room();
        var store = new MemoryStore { Saved = new(15, 14, "Europe/Berlin") };
        var settings = Register(room, store);
        var users = Users(room);
        var (owner, ownerPackets) = NativeClient(room, 1, users, 1);
        var (guest, guestPackets) = NativeClient(room, 4, users, 2);
        var original = settings.Snapshot;

        await new WiredSetRoomSettingsEvent(Service()).Parse(room, guest, HabbiconTestSupport.Incoming(8, 8, ""));
        Assert.Same(original, settings.Snapshot);
        Assert.Equal(original, store.Saved);
        Assert.Equal((14, 15, "Europe/Berlin"), NativeFrame(Assert.Single(guestPackets, packet => packet.Id == NativeSettingsId)));
        Assert.DoesNotContain(guestPackets, packet => packet.Id == ServerPacketHeader.WiredRoomSettingsDataComposer);
        Assert.Empty(ownerPackets);

        // Modify bit 0 (everyone) is not a modify option.
        await new WiredSetRoomSettingsEvent(Service()).Parse(room, owner, HabbiconTestSupport.Incoming(1, 1, ""));
        Assert.Same(original, settings.Snapshot);
        Assert.Equal((14, 15, "Europe/Berlin"), NativeFrame(Assert.Single(ownerPackets, packet => packet.Id == NativeSettingsId)));
    }

    [Fact]
    public async Task NativeHandlersAskForTheNativeReplyAndDropMalformedFrames()
    {
        var room = Room();
        var session = Client(room, 1);
        var service = new RecordingService();

        await new WiredGetRoomSettingsEvent(service).Parse(room, session, HabbiconTestSupport.Incoming(7));
        Assert.Null(service.Reloaded);

        await new WiredGetRoomSettingsEvent(service).Parse(room, session, HabbiconTestSupport.Incoming());
        await new WiredSetRoomSettingsEvent(service).Parse(room, session, HabbiconTestSupport.Incoming(12, 13, "UTC"));
        await new WiredSetRoomSettingsEvent(service).Parse(room, session, HabbiconTestSupport.Incoming(12, 13));

        Assert.Equal((room, session), service.Reloaded);
        Assert.Equal((room, session, 13, 12, "UTC"), Assert.Single(service.Saves));
        Assert.Equal([WiredRoomSettingsReply.Native, WiredRoomSettingsReply.Native], service.Replies);
    }

    [Fact]
    public async Task CustomSettingsRoutesStillAnswerWithTheCustomFrame()
    {
        var room = Room();
        var service = new RecordingService();
        var session = Client(room, 1);

        await new WiredRoomSettingsRequestEvent(service).Parse(room, session, HabbiconTestSupport.Incoming());
        await new WiredRoomSettingsSaveEvent(service).Parse(room, session, HabbiconTestSupport.Incoming(3, 4));
        await new WiredMenuPermissionsSaveEvent(service).Parse(room, session, HabbiconTestSupport.Incoming(5, 6, "Europe/Berlin"));
        await new WiredMenuPermissionsSaveEvent(service).Parse(room, session, HabbiconTestSupport.Incoming(5, 6));

        Assert.Equal([(room, session, 3, 4, null), (room, session, 6, 5, "Europe/Berlin")], service.Saves);
        Assert.Equal([WiredRoomSettingsReply.Custom, WiredRoomSettingsReply.Custom, WiredRoomSettingsReply.Custom], service.Replies);
    }

    [Fact]
    public async Task VoltMenuSaveSharesTheSettingsRulesAndAnswersWithTheCustomFrame()
    {
        var room = Room();
        var store = new MemoryStore { Saved = new(2, 2, "") };
        var settings = Register(room, store);
        var users = Users(room);
        var (owner, ownerPackets) = NativeClient(room, 1, users, 1);
        var (guest, guestPackets) = NativeClient(room, 4, users, 2);

        await new WiredMenuPermissionsSaveEvent(Service()).Parse(room, owner, HabbiconTestSupport.Incoming(4, 1, "UTC"));
        Assert.Equal(new(13, 12, "UTC"), store.Saved);
        Assert.Equal((13, 12, "UTC"), CustomFrame(ownerPackets[0]));
        Assert.DoesNotContain(ownerPackets, packet => packet.Id == NativeSettingsId);

        var saved = settings.Snapshot;
        guestPackets.Clear();
        await new WiredMenuPermissionsSaveEvent(Service()).Parse(room, guest, HabbiconTestSupport.Incoming(8, 8, ""));
        Assert.Same(saved, settings.Snapshot);
        Assert.Equal((13, 12, "UTC"), CustomFrame(Assert.Single(guestPackets, packet => packet.Id == ServerPacketHeader.WiredRoomSettingsDataComposer)));
        Assert.DoesNotContain(guestPackets, packet => packet.Id == NativeSettingsId);
    }

    [Fact]
    public void NativeAndVoltSettingsHeadersAreEachRegisteredOnce()
    {
        IPacketEvent[] handlers =
        [
            new WiredGetRoomSettingsEvent(Service()), new WiredSetRoomSettingsEvent(Service()),
            new WiredRoomSettingsRequestEvent(Service()), new WiredRoomSettingsSaveEvent(Service()), new WiredMenuPermissionsSaveEvent(Service())
        ];
        using var manager = new PacketManager(handlers, NullLogger<PacketManager>.Instance);
        var registered = (Dictionary<uint, IPacketEvent>)typeof(PacketManager).GetField("_incomingPackets", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager)!;
        var revision = new RevisionsCache().InternalRevision;
        using var table = HabbiconPacketTests.CurrentRevisionTable();
        var incoming = table.RootElement.GetProperty("IncomingHeaders");
        var outgoing = table.RootElement.GetProperty("OutgoingHeaders");

        foreach (var (handler, wire) in new[] { (handlers[0], NativeGetId), (handlers[1], NativeSetId), (handlers[2], 10022u), (handlers[3], 10023u), (handlers[4], VoltSaveId) }) {
            var name = handler.GetType().Name;
            Assert.True(revision.IncomingIdToInternalIdMapping.TryGetValue(wire, out var internalId), $"no incoming header {wire} for {name}");
            Assert.Same(handler, registered[internalId]);
            Assert.Equal(wire, incoming.GetProperty(name).GetUInt32());
            Assert.Single(revision.IncomingHeaders, entry => entry.Value == wire);
            Assert.Single(incoming.EnumerateObject(), property => property.Value.GetUInt32() == wire);
        }

        foreach (var (name, wire) in new[] { (nameof(WiredRoomSettingsComposer), NativeSettingsId), (nameof(WiredRoomSettingsDataComposer), 5102u) }) {
            Assert.Equal(wire, revision.OutgoingHeaders[name]);
            Assert.Equal(wire, outgoing.GetProperty(name).GetUInt32());
            Assert.Single(revision.OutgoingHeaders, entry => entry.Value == wire);
            Assert.Single(outgoing.EnumerateObject(), property => property.Value.GetUInt32() == wire);
        }

        // Volt's save is not in the client build, so it stays a placeholder id instead of a custom range.
        Assert.True(VoltSaveId >= CustomPacketRanges.PlaceholderStart);
    }

    private static (int Modify, int Read, string Timezone) NativeFields(IServerPacket frame)
    {
        using var stream = PlusMemoryStream.GetStream();
        frame.Compose(new FlashOutgoingPacket(stream));

        return Read(new FlashIncomingPacket { Buffer = stream.ToArray().AsMemory(6) });
    }

    private static (int Modify, int Read, string Timezone) NativeFrame((uint Id, FlashIncomingPacket Payload) packet)
    {
        Assert.Equal(NativeSettingsId, packet.Id);

        return Read(packet.Payload);
    }

    // Volt's frame: room id, inspect mask, modify mask, three permission flags, timezone.
    private static (int Inspect, int Modify, string Timezone) CustomFrame((uint Id, FlashIncomingPacket Payload) packet)
    {
        Assert.Equal(ServerPacketHeader.WiredRoomSettingsDataComposer, packet.Id);
        var payload = packet.Payload;
        Assert.Equal(77, payload.ReadInt());
        var masks = (payload.ReadInt(), payload.ReadInt());
        payload.ReadBool();
        payload.ReadBool();
        payload.ReadBool();
        var timezone = payload.ReadString();
        Assert.False(payload.HasDataRemaining());

        return (masks.Item1, masks.Item2, timezone);
    }

    private static (int Modify, int Read, string Timezone) Read(FlashIncomingPacket payload)
    {
        var fields = (payload.ReadInt(), payload.ReadInt(), payload.ReadString());
        Assert.False(payload.HasDataRemaining());

        return fields;
    }

    private static RoomUserManager Users(Room room)
    {
        var manager = new RoomUserManager(room, TestRoomUserStore.Instance, TimeProvider.System, new TestRewardProgress(), TestChatEmotions.Unused,
            TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel, TestItemRuntime.Settings);
        Set(room, "_roomUserManager", manager);

        return manager;
    }

    // The client reads frames through the supported revision, so the wire ids come from the real header tables.
    private static (FlashGameClient Client, List<(uint Id, FlashIncomingPacket Payload)> Packets) NativeClient(Room room, int id,
        RoomUserManager? users = null, int virtualId = 0)
    {
        var client = Client(room, id);
        var packets = new List<(uint, FlashIncomingPacket)>();
        client.GetHabbo().Client = client;
        client.Revision = new RevisionsCache().InternalRevision;
        client.SendCallback = args =>
        {
            packets.Add(((uint)FlashGameClient.DecodeInt16(args.MemoryBuffer.Slice(4, 2)), new() { Buffer = args.MemoryBuffer[6..].ToArray() }));

            return true;
        };

        if (users != null) {
            var user = new RoomUser(id, roomId: room.Id, virtualId: virtualId, room: room, client: client, TestChatEmotions.Unused, TestRewardProgress.Unused);
            ((ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager).GetField("_users", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(users)!).TryAdd(virtualId, user);
        }

        return (client, packets);
    }
}
