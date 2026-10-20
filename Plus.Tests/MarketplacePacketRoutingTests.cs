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
        (typeof(GetMarketplaceItemStatsEvent), 3428),
        (typeof(MakeOfferEvent), 221),
        (typeof(GetMarketplaceCanMakeOfferEvent), 3684),
        (typeof(GetOffersEvent), 2858),
        (typeof(GetOwnOffersEvent), 3444),
        (typeof(BuyOfferEvent), 3262),
        (typeof(RedeemOfferCreditsEvent), 2939),
        (typeof(CancelOfferEvent), 872)
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
        using var revision = JsonDocument.Parse(File.ReadAllText(HabbiconPacketTests.Repo("Resources/Revisions/example.json")));
        var incoming = revision.RootElement.GetProperty("IncomingHeaders");

        foreach (var (handler, id) in Expected) {
            Assert.Equal(id, incoming.GetProperty(handler.Name).GetUInt32());
        }
    }
}
