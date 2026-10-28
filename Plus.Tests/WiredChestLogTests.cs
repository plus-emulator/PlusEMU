using System.Buffers.Binary;
using System.Text;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.WiredChests;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Chests;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;
using static Plus.Tests.HabbiconTestSupport;

namespace Plus.Tests;

public sealed class WiredChestLogTests
{
    private const long Moment = 1793153202565;

    private static readonly WiredChestLogRow Row = new(7, 42, WiredChestLogType.Wired, 1, "alice", Moment, 2, 3, 4, 5, 6);

    [Theory]
    [InlineData(nameof(ClientPacketHeader.WiredTransactionGetRoomLogsEvent), 3320u)]
    [InlineData(nameof(ClientPacketHeader.WiredTransactionGetChestLogsEvent), 3451u)]
    public void LogRequestsUseTheOfficialIds(string name, uint id) =>
        Assert.Equal(id, (uint)typeof(ClientPacketHeader).GetField(name)!.GetRawConstantValue()!);

    [Fact]
    public void LogListUsesTheOfficialId() =>
        Assert.Equal(3312u, ServerPacketHeader.WiredTransactionLogListComposer);

    [Fact]
    public void ReservedRoomLogIdIsReleasedToTheOfficialRequest() =>
        Assert.DoesNotContain(Plus.Communication.Packets.CustomPacketRanges.ReservedClientPackets, packet => packet.Id == 9328);

    [Fact]
    public void RoomLogListHasTheOfficialShape()
    {
        var packet = new RecordingPacket();
        new WiredTransactionLogListComposer(new(false, 42, 1, 1, 10, [Row])).Compose(packet);

        Assert.Equal(new object[]
        {
            1, 0, 42, 1, 1, 10, 1,
            0, 7, 42, (int)WiredChestLogType.Wired, "", 1, "alice", (int)(Moment >> 32), unchecked((int)Moment), "2026-10-28 02:06:42", 2, 3, 4, 5, 6
        }, packet.Writes);
    }

    [Fact]
    public void ChestLogListNamesTheChestAndAnEmptyListHasNoRows()
    {
        var packet = new RecordingPacket();
        new WiredTransactionLogListComposer(new(true, 100, 0, 1, 25, [])).Compose(packet);

        Assert.Equal(new object[] { 0, 0, 100, 0, 1, 25, 0 }, packet.Writes);
    }

    [Theory]
    [InlineData(10, 1, 10, 1)]
    [InlineData(25, 2, 25, 2)]
    [InlineData(0, 1, 1, 1)]
    [InlineData(-1, 1, 1, 1)]
    [InlineData(1000, 1, 200, 1)]
    [InlineData(100, 0, 100, 1)]
    [InlineData(100, -1, 100, 1)]
    [InlineData(10, 99999, 10, 99999)]
    public void RequestsAreClampedLikeTheOfficialHotel(int amount, int page, int expectedAmount, int expectedPage)
    {
        var query = WiredChestLogQuery.Room(42, amount, page);

        Assert.Equal(new WiredChestLogQuery(42, null, expectedAmount, expectedPage), query);
    }

    [Fact]
    public async Task RoomLogRequestAnswersTheListForAReader()
    {
        var world = World();
        world.Store.Log = new(false, 42, 1, 1, 25, [Row]);

        await Event(world).Parse(world.Room, world.Client, Incoming(25, 1));

        Assert.Equal(WiredChestLogQuery.Room(42, 25, 1), Assert.Single(world.Store.LogQueries));
        var sent = Assert.Single(world.Packets);
        Assert.Equal(ServerPacketHeader.WiredTransactionLogListComposer, sent.Header);
        Assert.Equal(1, ReadInt(sent.Payload, 0));
        Assert.Equal(42, ReadInt(sent.Payload, 8));
    }

    [Fact]
    public async Task ChestLogRequestAnswersTheListOfThatChest()
    {
        var world = World();
        world.Store.Log = new(true, 100, 0, 1, 25, []);

        await ChestEvent(world).Parse(world.Room, world.Client, Incoming(100, 25, 1));

        Assert.Equal(WiredChestLogQuery.Chest(42, 100, 25, 1), Assert.Single(world.Store.LogQueries));
        Assert.Equal(0, ReadInt(Assert.Single(world.Packets).Payload, 0));
    }

    [Fact]
    public async Task ChestLogRequestForAnythingButAChestInTheRoomIsRefused()
    {
        var world = World();

        await ChestEvent(world).Parse(world.Room, world.Client, Incoming(999, 25, 1));

        Assert.Empty(world.Store.LogQueries);
        AssertNoReadRights(Assert.Single(world.Packets));
    }

    [Fact]
    public async Task AUserWithoutReadRightsGetsTheRefusalAndNoQuery()
    {
        var world = World();
        world.Habbo.Username = "visitor";

        await Event(world).Parse(world.Room, world.Client, Incoming(10, 1));

        Assert.Empty(world.Store.LogQueries);
        AssertNoReadRights(Assert.Single(world.Packets));
    }

    [Fact]
    public async Task ARequestOverTheRateLimitIsDropped()
    {
        var world = World();

        await new WiredTransactionGetRoomLogsEvent(new WiredChestLogService(new Gate(false))).Parse(world.Room, world.Client, Incoming(10, 1));

        Assert.Empty(world.Store.LogQueries);
        Assert.Empty(world.Packets);
    }

    [Fact]
    public async Task MalformedTruncatedAndTrailingRequestsNeverQuery()
    {
        var world = World();

        foreach (var values in new object[][] { [], [10], [10, 1, 5] }) {
            await Event(world).Parse(world.Room, world.Client, Incoming(values));
        }

        foreach (var values in new object[][] { [], [100], [100, 25], [100, 25, 1, 5] }) {
            await ChestEvent(world).Parse(world.Room, world.Client, Incoming(values));
        }

        Assert.Empty(world.Store.LogQueries);
        Assert.Empty(world.Packets);
    }

    [Fact]
    public async Task ManualWithdrawalIsLoggedAsManual()
    {
        var world = World();
        world.Store.Kind = WiredChestKind.Coins;

        await new ChestWithdrawCoinsEvent().Parse(world.Room, world.Client, Incoming(100, 5));

        Assert.Equal(WiredChestLogType.Manual, Assert.Single(world.Store.Requests).LogType);
    }

    [Fact]
    public void ADepositWithoutAContractIsLoggedAsManual()
    {
        var world = World();
        Assert.True(world.Module.Start(world.Client, 100, [world.Chest], null));
        world.Module.Offer(world.Client, [201], true);
        world.Module.Confirm(world.Client, false);
        world.Module.Confirm(world.Client, true);

        Assert.Equal(WiredChestLogType.Manual, Assert.Single(world.Store.Requests).LogType);
    }

    [Theory]
    [InlineData(WiredContractKind.Payment, WiredChestLogType.ContractPayment)]
    [InlineData(WiredContractKind.Trade, WiredChestLogType.ContractTrade)]
    public void AConfirmedContractIsLoggedWithItsKind(WiredContractKind kind, WiredChestLogType expected)
    {
        var world = World();
        var contract = world.Contract with { Kind = kind, Reward = kind == WiredContractKind.Trade ? [new(1)] : null };
        Assert.True(world.Module.Start(world.Client, 80, [world.Chest], contract));
        world.Module.Offer(world.Client, [201], true);
        world.Module.Confirm(world.Client, false);
        world.Module.Confirm(world.Client, true);

        Assert.Equal(expected, Assert.Single(world.Store.Requests).LogType);
    }

    [Fact]
    public void AContractRewardIsLoggedAsAReward()
    {
        var world = World();
        var contract = world.Contract with { Kind = WiredContractKind.Reward, Reward = [new(1)] };

        world.Module.Start(world.Client, 80, [world.Chest], contract);

        Assert.Equal(WiredChestLogType.ContractReward, Assert.Single(world.Store.Requests).LogType);
    }

    private static WiredChestProtocolTests.World World()
    {
        var world = new WiredChestProtocolTests.World();
        world.Room.Type = "private";
        world.Room.OwnerName = "owner";
        world.Room.UsersWithRights = [];
        world.Habbo.Username = "owner";
        world.Habbo.Access = EditorTestSupport.Access([]);

        return world;
    }

    private static WiredTransactionGetRoomLogsEvent Event(WiredChestProtocolTests.World world) => new(new WiredChestLogService(new Gate(true)));
    private static WiredTransactionGetChestLogsEvent ChestEvent(WiredChestProtocolTests.World world) => new(new WiredChestLogService(new Gate(true)));

    private static void AssertNoReadRights((uint Header, byte[] Payload) sent)
    {
        Assert.Equal(ServerPacketHeader.WiredValidationErrorComposer, sent.Header);
        var length = BinaryPrimitives.ReadUInt16BigEndian(sent.Payload);
        Assert.Equal("wiredfurni.error.no_read_rights", Encoding.UTF8.GetString(sent.Payload, 2, length));
        Assert.Equal(0, ReadInt(sent.Payload, 2 + length));
    }

    private static int ReadInt(byte[] payload, int offset) => BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(offset));

    private sealed class Gate(bool pass) : IWiredRequestGateService
    {
        public bool TryPass(GameClient session, WiredRequestKind kind) => pass;
    }
}
