using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Marketplace;
using Xunit;

namespace Plus.Tests;

// October AIR marketplace request IDs are pinned to their direction-qualified composer identities.
public class MarketplacePacketRoutingTests
{
    private static readonly (Type Handler, uint Id)[] Expected =
    [
        (typeof(GetMarketplaceItemStatsEvent), 3359),
        (typeof(MakeOfferEvent), 371),
        (typeof(GetMarketplaceCanMakeOfferEvent), 3972),
        (typeof(GetOffersEvent), 1039),
        (typeof(GetOwnOffersEvent), 1595),
        (typeof(BuyOfferEvent), 2392),
        (typeof(RedeemOfferCreditsEvent), 2483),
        (typeof(CancelOfferEvent), 3285)
    ];

    [Fact]
    public void EveryMarketplaceRequestIsDispatchedToItsOwnHandlerUnderTheAirId()
    {
        var handlers = Expected.Select(entry => (IPacketEvent)RuntimeHelpers.GetUninitializedObject(entry.Handler)).ToArray();
        var manager = new PacketManager(handlers, NullLogger<PacketManager>.Instance);
        var registered = (Dictionary<uint, IPacketEvent>)typeof(PacketManager).GetField("_incomingPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;

        foreach (var (handler, id) in Expected) {
            Assert.True(manager.IsRegistered(id), $"{handler.Name} is not registered under {id}");
            Assert.Equal(handler, registered[id].GetType());
        }

        Assert.Equal(Expected.Length, registered.Count);
    }

    [Fact]
    public void TheDefaultRevisionFileCarriesTheSameMarketplaceIds()
    {
        using var revision = HabbiconPacketTests.CurrentRevisionTable();
        var incoming = revision.RootElement.GetProperty("IncomingHeaders");

        foreach (var (handler, id) in Expected) {
            Assert.Equal(id, incoming.GetProperty(handler.Name).GetUInt32());
        }
    }
}
