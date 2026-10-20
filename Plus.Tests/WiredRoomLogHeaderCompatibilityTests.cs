using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.Communication.Revisions;
using Xunit;

namespace Plus.Tests;

// The supported client build (WIN63-202610201032-107964540, communication/class_2061.as) sends the room log page request under
// _composers[2730] = class_3819 and registers the room log page frame as name_1[976] = class_3652.
public class WiredRoomLogHeaderCompatibilityTests
{
    private const uint ClientRequestId = 2730;
    private const uint ClientPageId = 976;

    [Fact]
    public void TheClientsLogPageRequestIdResolvesToTheRoomLogPageHandler()
    {
        var handler = (IPacketEvent)RuntimeHelpers.GetUninitializedObject(typeof(WiredRoomLogsPageEvent));
        using var manager = new PacketManager([handler], NullLogger<PacketManager>.Instance);
        var registered = (Dictionary<uint, IPacketEvent>)typeof(PacketManager).GetField("_incomingPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        var revision = new RevisionsCache().InternalRevision;

        Assert.True(revision.IncomingIdToInternalIdMapping.TryGetValue(ClientRequestId, out var internalId),
            $"the supported revision maps no incoming wire id {ClientRequestId}");
        Assert.True(manager.IsRegistered(internalId), $"the packet manager registers nothing under {internalId}");
        Assert.Same(handler, registered[internalId]);
    }

    [Fact]
    public void TheRoomLogPageFrameIsSentUnderTheClientsRegisteredId()
    {
        var sent = new List<byte[]>();
        var client = new FlashGameClient(TestGameServer.Instance, new FlashPacketFactory(), TestLogging.GameClient)
        {
            Revision = new RevisionsCache().InternalRevision,
            SendCallback = args =>
            {
                sent.Add(args.MemoryBuffer.ToArray());

                return true;
            }
        };

        client.Send(new WiredRoomLogPageComposer(new(0, 0, 50, []), -1, -1, ""));

        var frame = Assert.Single(sent);
        Assert.Equal(ClientPageId, BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4, 2)));
    }

    [Fact]
    public void TheRevisionExampleTableAndHeaderConstantsAgreeOnTheClientIdsAndTheyAreUnique()
    {
        using var table = HabbiconPacketTests.CurrentRevisionTable();
        var incoming = table.RootElement.GetProperty("IncomingHeaders");
        var outgoing = table.RootElement.GetProperty("OutgoingHeaders");

        Assert.Equal(ClientRequestId, incoming.GetProperty(nameof(ClientPacketHeader.WiredRoomLogsPageEvent)).GetUInt32());
        Assert.Equal(ClientPageId, outgoing.GetProperty(nameof(ServerPacketHeader.WiredRoomLogPageComposer)).GetUInt32());
        Assert.Equal(ClientRequestId, ClientPacketHeader.WiredRoomLogsPageEvent);
        Assert.Equal(ClientPageId, ServerPacketHeader.WiredRoomLogPageComposer);
        Assert.Single(incoming.EnumerateObject(), property => property.Value.GetUInt32() == ClientRequestId);
        Assert.Single(outgoing.EnumerateObject(), property => property.Value.GetUInt32() == ClientPageId);
    }

    // Control: whatever ids the room log uses, the two header tables stay free of duplicate wire ids.
    [Fact]
    public void TheSupportedRevisionKeepsEveryWireIdUnique()
    {
        var revision = new RevisionsCache().InternalRevision;

        Assert.Equal(revision.IncomingHeaders.Count, revision.IncomingHeaders.Values.Distinct().Count());
        Assert.Equal(revision.OutgoingHeaders.Values.Count(id => id > 0), revision.OutgoingHeaders.Values.Where(id => id > 0).Distinct().Count());
    }
}
