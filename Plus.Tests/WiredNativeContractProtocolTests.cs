using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.WiredChests;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Interactor;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Plus.HabboHotel.Items.Wired.Chests;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed class WiredNativeContractProtocolTests
{
    // Independently pinned AIR fixture shared with renderer WiredContractProtocol.test.ts.
    private const string PaymentHex = "0000002a00000100000001000000020000000007010000000201000000010001390000010003506179000767656e65726963";
    private static WiredChestContract Payment => new()
    {
        Id = 42,
        Kind = WiredContractKind.Payment,
        PaymentMode = 1,
        ReceiveText = "Pay",
        Payment = [[new(7), new(2, new(true, 1, "9"))]]
    };
    private static byte[] Encode(WiredChestContract contract)
    {
        using var stream = PlusMemoryStream.GetStream();
        WiredNativeContractWire.Write(new FlashOutgoingPacket(stream), contract);

        return stream.ToArray()[6..];
    }
    private static FlashIncomingPacket Packet(byte[] bytes) => new() { Buffer = bytes };
    private static G1World World()
    {
        var world = new G1World();
        world.Room.Type = "private";
        world.Room.OwnerName = "user1";
        world.Room.UsersWithRights = [];

        return world;
    }
    private static WiredChestContract Decode(byte[] bytes)
    {
        var packet = Packet(bytes);

        try {
            var value = WiredNativeContractWire.Read(packet);
            Assert.False(packet.HasDataRemaining());

            return value;
        }
        finally {
            packet.Stream.Dispose();
        }
    }
    private sealed class Definitions : IItemDataManager
    {
        public void Init() { }
        public ItemDefinition? GetItemByName(string name) => null;
        public Dictionary<int, uint> Gifts { get; } = [];
        public Dictionary<uint, ItemDefinition> Items { get; } = new() { [1] = new() { Id = 1, SpriteId = 1, Type = ItemType.Wall, AllowTrade = true } };
    }
    private static async Task Update(G1World world, G1Player player, byte[] bytes)
    {
        var packet = Packet(bytes);

        try {
            await new UpdateWiredContractEvent(new Definitions()).Parse(world.Room, player.Client, packet);
        }
        finally {
            packet.Stream.Dispose();
        }
    }
    private static async Task Fetch(G1World world, G1Player player, uint id)
    {
        var packet = HabbiconTestSupport.Incoming((int)id);

        try {
            await new GetWiredContractContentsEvent().Parse(world.Room, player.Client, packet);
        }
        finally {
            packet.Stream.Dispose();
        }
    }

    [Fact]
    public void ExactAirBytesAndCurrentHeaderRegistrations()
    {
        Assert.Equal(PaymentHex, Convert.ToHexString(Encode(Payment)).ToLowerInvariant());
        var parsed = Decode(Convert.FromHexString(PaymentHex));
        Assert.Equal(42u, parsed.Id);
        Assert.Equal(WiredContractKind.Payment, parsed.Kind);
        Assert.Equal(Payment.Payment![0], parsed.Payment![0]);
        Assert.Null(parsed.Reward);
        Assert.Equal(1, parsed.PaymentMode);
        Assert.Equal("Pay", parsed.ReceiveText);
        Assert.Equal("generic", parsed.Layout);
        var manager = new PacketManager([new GetWiredContractContentsEvent(), new UpdateWiredContractEvent(new Definitions())], NullLogger<PacketManager>.Instance);
        Assert.True(manager.IsRegistered(1764));
        Assert.True(manager.IsRegistered(1005));
        Assert.Equal(3218u, ServerPacketHeader.WiredContractOpenComposer);
        Assert.Equal(2309u, ServerPacketHeader.WiredContractContentsComposer);
        Assert.Equal(1085u, ServerPacketHeader.WiredContractUpdateResultComposer);
    }

    [Theory]
    [InlineData(WiredContractKind.Payment)]
    [InlineData(WiredContractKind.Trade)]
    [InlineData(WiredContractKind.Reward)]
    public async Task OwnerCanOpenFetchSaveAndRefetchEveryNativeContractKind(WiredContractKind kind)
    {
        using var world = World();
        var owner = world.AddPlayer();
        var original = Payment with
        {
            Kind = kind,
            Payment = kind == WiredContractKind.Reward ? null : Payment.Payment,
            Reward = kind == WiredContractKind.Payment ? null : [new(11)],
            RewardCategory = 13,
            ShowDialog = true,
            RewardText = "Thanks ♥"
        };
        var item = world.AddContract(kind, original);
        new InteractorWiredContract().OnTrigger(owner.Client, item, 0, true);
        var open = Assert.Single(owner.Packets);
        Assert.Equal(3218u, open.Header);
        var openPacket = Packet(open.Payload);
        Assert.Equal(item.Id, openPacket.ReadUInt());
        openPacket.Stream.Dispose();
        owner.Packets.Clear();
        await Fetch(world, owner, item.Id);
        var contents = Assert.Single(owner.Packets);
        Assert.Equal(2309u, contents.Header);
        var loaded = Decode(contents.Payload);
        Assert.Equal(item.Id, loaded.Id);
        Assert.Equal(kind, loaded.Kind);
        var changed = loaded with
        {
            Payment = kind == WiredContractKind.Reward ? null : [[new(23)]],
            Reward = kind == WiredContractKind.Payment ? null : [new(31)],
            ReceiveText = kind == WiredContractKind.Payment ? "Updated" : loaded.ReceiveText,
            RewardText = kind == WiredContractKind.Reward ? "Updated reward" : loaded.RewardText
        };
        owner.Packets.Clear();
        await Update(world, owner, Encode(changed));
        var response = Assert.Single(owner.Packets);
        Assert.Equal(1085u, response.Header);
        var result = Packet(response.Payload);
        Assert.Equal(item.Id, result.ReadUInt());
        Assert.True(result.ReadBool());
        Assert.Equal("", result.ReadString());
        result.Stream.Dispose();
        Assert.Equal(Encode(changed), Encode(world.Store.Contracts[item.Id]));
        owner.Packets.Clear();
        await Fetch(world, owner, item.Id);
        Assert.Equal(Encode(changed), Assert.Single(owner.Packets).Payload);
        Assert.Empty(world.Store.Requests); // Editing never transfers inventory or currency.
    }

    [Fact]
    public async Task UnauthorizedVisitorCannotOpenReadOrWriteContract()
    {
        using var world = World();
        var visitor = world.AddPlayer(2);
        var item = world.AddContract(WiredContractKind.Payment, Payment);
        new InteractorWiredContract().OnTrigger(visitor.Client, item, 0, false);
        await Fetch(world, visitor, item.Id);
        await Update(world, visitor, Encode(Payment with { Id = item.Id, ReceiveText = "Tampered" }));
        Assert.Empty(visitor.Packets);
        Assert.Equal("Pay", world.Store.Contracts[item.Id].ReceiveText);
    }

    [Fact]
    public async Task InvalidTypeRulesAmountsAndMetadataDoNotPersist()
    {
        using var world = World();
        var owner = world.AddPlayer();
        var item = world.AddContract(WiredContractKind.Payment, Payment);
        var valid = Payment with { Id = item.Id };
        var invalid = new[] {
            valid with { Kind = WiredContractKind.Reward, Payment = null, Reward = [new(5)] },
            valid with { Payment = [] }, valid with { Payment = [[new(0)]] }, valid with { Payment = [[new(100001)]] },
            valid with { Reward = [new(1)] }, valid with { PaymentMode = 2 }, valid with { Layout = "unknown" },
            valid with { ReceiveText = new string('x', 61) }, valid with { Payment = [[new(1, new(false, 999))]] }
        };

        foreach (var contract in invalid) {
            owner.Packets.Clear();
            await Update(world, owner, Encode(contract));
            var response = Packet(Assert.Single(owner.Packets).Payload);
            Assert.Equal(item.Id, response.ReadUInt());
            Assert.False(response.ReadBool());
            Assert.Equal("invalid_rules", response.ReadString());
            response.Stream.Dispose();
            Assert.Equal("Pay", world.Store.Contracts[item.Id].ReceiveText);
        }
    }

    [Fact]
    public async Task TruncatedTrailingAndUnboundedPacketsCannotReachPersistence()
    {
        using var world = World();
        var owner = world.AddPlayer();
        var item = world.AddContract(WiredContractKind.Payment, Payment);
        var bytes = Encode(Payment with { Id = item.Id, ReceiveText = "Tampered" });
        List<byte[]> malformed = [[.. bytes, 0], Convert.FromHexString("0000002a0000017fffffff"), Convert.FromHexString("0000002a0000010000000100000006")];
        malformed.AddRange(Enumerable.Range(0, bytes.Length).Select(length => bytes[..length]));

        foreach (var input in malformed) {
            await Update(world, owner, input);
        }

        Assert.Empty(owner.Packets);
        Assert.Equal("Pay", world.Store.Contracts[item.Id].ReceiveText);
    }

    [Fact]
    public async Task RemovedTemporaryItemAndDepartedOwnerCannotSaveOrFetch()
    {
        using var world = World();
        var owner = world.AddPlayer();
        var item = world.AddContract(WiredContractKind.Payment, Payment);
        var bytes = Encode(Payment with { Id = item.Id, ReceiveText = "Tampered" });
        owner.Habbo.CurrentRoom = null;
        await Fetch(world, owner, item.Id);
        await Update(world, owner, bytes);
        owner.Habbo.CurrentRoom = world.Room;
        world.Floor.TryRemove(item.Id, out _);
        await Fetch(world, owner, item.Id);
        await Update(world, owner, bytes);
        world.Floor[item.Id] = item;
        typeof(Item).GetProperty(nameof(Item.IsTemporary))!.SetValue(item, true);
        new InteractorWiredContract().OnTrigger(owner.Client, item, 0, true);
        await Fetch(world, owner, item.Id);
        await Update(world, owner, bytes);
        Assert.Empty(owner.Packets);
        Assert.Equal("Pay", world.Store.Contracts[item.Id].ReceiveText);
    }

    [Fact]
    public void ExistingCompatibilityEditorAndSaveRemainAvailable()
    {
        using var world = World();
        var owner = world.AddPlayer();
        var item = world.AddContract(WiredContractKind.Payment, Payment);
        WiredChestContractEditor.Open(world.Module, owner.Client, item);
        Assert.Equal(ServerPacketHeader.WiredContractOpenComposer, Assert.Single(owner.Packets).Header);
        var saved = WiredChestContractEditor.TrySave(world.Module, item, new WiredConfiguration
        {
            IntParams = ImmutableArray.Create(-1, 1, 1, 0, -1, 0, 0, 9, 0),
            Text = ""
        });
        Assert.True(saved);
        Assert.Equal(9, world.Store.Contracts[item.Id].Payment![0][0].Amount);
    }
}
