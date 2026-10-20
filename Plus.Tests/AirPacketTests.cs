using System.Buffers.Binary;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Catalog;
using Plus.Communication.Packets.Incoming.Handshake;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Marketplace;
using Plus.Communication.Revisions;
using Plus.HabboHotel.GameClients;
using Xunit;

namespace Plus.Tests;

[Collection("Revision cache startup")]
public class AirPacketTests
{
    [Fact]
    public async Task DefaultRegistryUsesAirHeadersWithoutAnAdditionalRevisionFile()
    {
        var directory = Directory.CreateTempSubdirectory("air-packets-").FullName;

        try {
            var cache = CacheAt(directory);
            await cache.Start();
            var revision = cache.InternalRevision;
            Assert.Equal("WIN63-202610201032-107964540", revision.Name);
            Assert.Same(revision, Assert.Single(cache.Revisions).Value);
            Assert.Equal(3209u, ClientPacketHeader.InfoRetrieveEvent);
            Assert.Equal(591u, ClientPacketHeader.SSOTicketEvent);
            Assert.Equal(3401u, ClientPacketHeader.GetCatalogIndexEvent);
            Assert.Equal(3392u, ClientPacketHeader.GetBundleDiscountRulesetEvent);
            Assert.Equal(355u, ClientPacketHeader.GetProductOfferEvent);
            Assert.Equal(819u, ClientPacketHeader.MovePetEvent);
            Assert.Equal(2508u, ClientPacketHeader.MoveEntityInFlatEvent);
            Assert.Equal(3097u, ServerPacketHeader.AuthenticationOkComposer);
            Assert.Equal(199u, ServerPacketHeader.CreditBalanceComposer);
            Assert.Equal(1462u, ServerPacketHeader.CatalogIndexComposer);
            Assert.Equal(2451u, ServerPacketHeader.AvatarEffectAddedComposer);
            Assert.Equal(534u, ServerPacketHeader.CallForHelpPendingCallsComposer);
            Assert.Equal(848u, ServerPacketHeader.MarketplaceCancelOfferResultComposer);
            Assert.Equal(3815u, ServerPacketHeader.MarketplaceItemStatsComposer);
            Assert.Equal(3209u, revision.IncomingIdToInternalIdMapping[3209]);
            Assert.All(revision.IncomingIdToInternalIdMapping, entry => Assert.Equal(entry.Key, entry.Value));
            Assert.All(revision.InternalIdToOutgoingIdMapping, entry => Assert.Equal(entry.Key, entry.Value));

            var server = new BootstrapServer(new ClientHelloEvent(cache, NullLogger<ClientHelloEvent>.Instance));
            var sent = new List<byte[]>();
            var client = new FlashGameClient(server, new FlashPacketFactory(), TestLogging.GameClient)
            {
                Revision = revision,
                SendCallback = args => { sent.Add(args.MemoryBuffer.ToArray()); return true; }
            };
            using var stream = PlusMemoryStream.GetStream();
            var hello = new FlashOutgoingPacket(stream);
            hello.WriteString(revision.Name);
            hello.WriteString("HTML5");
            hello.WriteInt(2);
            hello.WriteInt(1);
            var frame = stream.GetBuffer().AsMemory(0, (int)stream.Length);
            client.CreateHeader(frame, 4000);
            var received = frame.ToArray().Concat(new byte[] { 0, 0, 0, 2, 12, 137 }).ToArray();
            client.OnReceived(received, 0, received.Length);
            Assert.Equal(new uint[] { 4000, 3209 }, server.Headers);
            Assert.Same(revision, client.Revision);

            client.Send(new AuthenticationOkComposer(7));
            client.Send(new CreditBalanceComposer(123));
            Assert.Equal(3097, BinaryPrimitives.ReadUInt16BigEndian(sent[0].AsSpan(4)));
            Assert.Equal(199, BinaryPrimitives.ReadUInt16BigEndian(sent[1].AsSpan(4)));
            var credits = new FlashIncomingPacket { Buffer = sent[1][6..] };
            Assert.Equal("123.0", credits.ReadString());
            Assert.False(credits.HasDataRemaining());

            client.Send(new MarketplaceItemStatsComposer(1, 42, 100, 3));
            Assert.Equal(3815, BinaryPrimitives.ReadUInt16BigEndian(sent[2].AsSpan(4)));
            var stats = new FlashIncomingPacket { Buffer = sent[2][6..] };
            Assert.Equal(new[] { 100, 3, 0, 0, 1, 42, 0, 0 }, Enumerable.Range(0, 8).Select(_ => stats.ReadInt()));
            Assert.False(stats.HasDataRemaining());
        }
        finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void CanonicalCatalogIndexAndBundleHandlersAreIncludedInTheProductionScan()
    {
        var services = new ServiceCollection();
        Program.AddAssignableTo<IPacketEvent>(services, typeof(GetCatalogIndexEvent).Assembly);
        Assert.Contains(services, descriptor => descriptor.ImplementationType == typeof(GetCatalogIndexEvent));
        Assert.Contains(services, descriptor => descriptor.ImplementationType == typeof(GetBundleDiscountRulesetEvent));
    }

    private static RevisionsCache CacheAt(string directory)
    {
        var cache = new RevisionsCache();
        typeof(RevisionsCache).GetField("_directory", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cache, directory);

        return cache;
    }

    private sealed class BootstrapServer(ClientHelloEvent hello) : IGameServer
    {
        public List<uint> Headers { get; } = new();
        public bool Start() => true;
        public bool Stop() => true;
        public bool ModifyOutgoingPacket(GameClient client, IOutgoingPacket packet) => true;
        public bool HasOutgoingPacketInjectors(uint messageId) => false;
        public Task PacketReceived(GameClient client, uint messageId, IIncomingPacket packet)
        {
            Headers.Add(messageId);

            return messageId == ClientPacketHeader.ClientHelloEvent ? hello.Parse(client, packet) : Task.CompletedTask;
        }
    }
}
