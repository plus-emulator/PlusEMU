using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Chests;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// g2: wf_act_give_currency and wf_act_give_furni end to end through the real action, the real chest room and an in-memory store
// that applies a transfer all or nothing (the way the SQL store does in one transaction). The SQL store itself is covered by the
// database facts, which skip without a server.
public sealed class WiredChestGiveG2Tests
{
    private const int FirstChest = 100;

    [Fact]
    public void GiveCurrencyPaysTheAmountFromTheChestIntoTheWalletAndShowsThePopup()
    {
        using var world = new Scene();
        world.Coins(FirstChest, 20);
        var box = world.Box("wf_act_give_currency", Currency(mode: 0, amount: 5, show: 1), [FirstChest], "well done");
        Assert.True(box.Execute(world.Context()));
        Assert.Equal(105, world.Habbo.Credits);
        Assert.Equal(15, world.Store.Chests[FirstChest].Coins);
        var request = Assert.Single(world.Store.Requests);
        Assert.True(request.Wired);
        Assert.Equal(5, Assert.Single(request.Reward).Amount);
        Assert.Null(request.Reward[0].ItemType);
        Assert.Empty(request.PaymentIds);
        var popup = Assert.Single(world.Packets, packet => packet.Header == 9346);
        Assert.Contains("well done", System.Text.Encoding.UTF8.GetString(popup.Payload));
        Assert.Contains(world.Packets, packet => packet.Header == Plus.Communication.Packets.Outgoing.ServerPacketHeader.CreditBalanceComposer);
    }

    [Fact]
    public void GiveCurrencyAskingForMoreThanTheChestHoldsGivesWhatIsThere()
    {
        using var world = new Scene();
        world.Coins(FirstChest, 20);
        var box = world.Box("wf_act_give_currency", Currency(0, 50, 0), [FirstChest]);
        Assert.True(box.Execute(world.Context()));
        Assert.Equal(120, world.Habbo.Credits);
        Assert.Equal(0, world.Store.Chests[FirstChest].Coins);
        Assert.False(box.Execute(world.Context())); // Nothing left: no request, no pop-up.
        Assert.Single(world.Store.Requests);
        Assert.Single(world.Packets, packet => packet.Header == 9346);
        Assert.Equal(120, world.Habbo.Credits);
    }

    [Fact]
    public void GiveCurrencyAllModeEmptiesEveryPickedChestForOneUserOnly()
    {
        using var world = new Scene();
        world.Coins(FirstChest, 20);
        world.Coins(101, 7);
        var box = world.Box("wf_act_give_currency", Currency(mode: 1, amount: 1, show: 0), [FirstChest, 101]);
        Assert.True(box.Execute(world.Context()));
        Assert.Equal(127, world.Habbo.Credits);
        Assert.Equal(0, world.Store.Chests[FirstChest].Coins);
        Assert.Equal(0, world.Store.Chests[101].Coins);
        Assert.Equal([FirstChest, 101u], Assert.Single(world.Store.Requests).ChestIds.Select(id => (uint)id).ToArray());

        // Everything cannot go to each of several users: refused before any request.
        world.Coins(FirstChest, 20);
        var second = world.AddUser(2, 8);
        Assert.False(box.Execute(world.Context(world.Actor, second.Actor)));
        Assert.Single(world.Store.Requests);
        Assert.Equal(20, world.Store.Chests[FirstChest].Coins);
        Assert.Equal(127, world.Habbo.Credits);
        Assert.Equal(100, second.Habbo.Credits);
    }

    [Fact]
    public void GiveCurrencyServesSeveralUsersFromTheChestStockInTurn()
    {
        using var world = new Scene();
        world.Coins(FirstChest, 15);
        var second = world.AddUser(2, 8);
        var box = world.Box("wf_act_give_currency", Currency(0, 10, 0), [FirstChest]);
        Assert.True(box.Execute(world.Context(world.Actor, second.Actor)));
        Assert.Equal(110, world.Habbo.Credits);
        Assert.Equal(105, second.Habbo.Credits); // The stock is read again for each user, so the second gets the rest.
        Assert.Equal(0, world.Store.Chests[FirstChest].Coins);
        Assert.Equal(15, world.Store.Requests.Sum(request => request.Reward.Sum(node => node.Amount)));
    }

    [Fact]
    public void GiveCurrencyIgnoresLockedDisabledAndFurniChestsAndUnplacedOnes()
    {
        using var world = new Scene();
        world.Coins(FirstChest, 20, locked: true);
        world.Coins(101, 20, enabled: false);
        world.Furni(102, 5);
        // hhus refuses the save when any pick is not an upgraded coin chest (require_coin_chests): disabled, furni and unplaced picks.
        foreach (var pick in new uint[] { 101, 102, 999 }) {
            var refused = (WiredChestAction)WiredChestBox.Create(world.Room, new() { Id = 9 }, WiredBoxRegistry.All.Single(entry => entry.CanonicalName == "wf_act_give_currency"), world.Module);
            Assert.True(WiredNativeEditorProjection.TryCompile(9, refused.Descriptor, world.Native("wf_act_give_currency", Currency(0, 5, 0), [FirstChest, pick], ""), out var runtime));
            Assert.False(refused.TryValidateConfiguration(runtime, out _, out var error));
            Assert.Equal("wiredfurni.error.require_coin_chests", error);
        }

        var box = world.Box("wf_act_give_currency", Currency(0, 5, 0), [FirstChest]);
        Assert.False(box.Execute(world.Context()));
        Assert.Empty(world.Store.Requests);
        Assert.Equal(100, world.Habbo.Credits);
        Assert.DoesNotContain(world.Packets, packet => packet.Header == 9346);
        world.Store.Chests[FirstChest].Locked = false;
        Assert.True(box.Execute(world.Context()));
        Assert.Equal(105, world.Habbo.Credits);
        Assert.Equal(20, world.Store.Chests[101].Coins);
    }

    [Fact]
    public void GiveCurrencyNeverGoesBelowOrAboveTheWalletDomain()
    {
        using var world = new Scene();
        world.Coins(FirstChest, 20);
        world.Habbo.Credits = int.MaxValue - 3;
        var box = world.Box("wf_act_give_currency", Currency(0, 5, 0), [FirstChest]);
        Assert.False(box.Execute(world.Context()));
        Assert.Equal(int.MaxValue - 3, world.Habbo.Credits);
        Assert.Equal(20, world.Store.Chests[FirstChest].Coins);
        Assert.DoesNotContain(world.Packets, packet => packet.Header == 9346);
        world.Habbo.Credits = 100;
        Assert.True(box.Execute(world.Context()));
        Assert.Equal(105, world.Habbo.Credits);
        Assert.True(world.Store.Chests.Values.All(chest => chest.Coins >= 0));
    }

    [Theory]
    [InlineData("refused")]
    [InlineData("database")]
    public void GiveCurrencyRollsBackWhenTheTransferFails(string failure)
    {
        using var world = new Scene();
        world.Coins(FirstChest, 20);

        if (failure == "refused") {
            world.Store.Refuse = WiredChestFailure.FundsGone;
        }
        else {
            world.Store.Throw = true;
        }

        var box = world.Box("wf_act_give_currency", Currency(0, 5, 1), [FirstChest]);
        Assert.False(box.Execute(world.Context()));
        Assert.Equal(100, world.Habbo.Credits);
        Assert.Equal(20, world.Store.Chests[FirstChest].Coins);
        Assert.DoesNotContain(world.Packets, packet => packet.Header == 9346);
        world.Store.Refuse = null;
        world.Store.Throw = false;
        Assert.True(box.Execute(world.Context())); // The same box pays normally afterwards: one payment, not two.
        Assert.Equal(105, world.Habbo.Credits);
        Assert.Equal(15, world.Store.Chests[FirstChest].Coins);
    }

    [Fact]
    public void GiveCurrencyGivesNothingWithoutAUserOrWithABotOnly()
    {
        using var world = new Scene();
        world.Coins(FirstChest, 20);
        var box = world.Box("wf_act_give_currency", Currency(0, 5, 0), [FirstChest]);
        Assert.False(box.Execute(world.ContextOf([])));
        Assert.Empty(world.Store.Requests);
        Assert.Equal(20, world.Store.Chests[FirstChest].Coins);
    }

    [Fact]
    public void GiveFurniMovesTheOldestItemsFirstAndTransfersOwnership()
    {
        using var world = new Scene();
        world.Furni(FirstChest, 3, sprites: [10, 10, 20]);
        var box = world.Box("wf_act_give_furni", FurniGive(mode: 0, amount: 2, order: 1), [FirstChest], "enjoy");
        Assert.True(box.Execute(world.Context()));
        Assert.Equal(new uint[] { 1000, 1001 }, world.Store.Received.Select(item => item.Id).ToArray());
        Assert.All(world.Store.Received, item => Assert.Equal(1u, item.OwnerId));
        Assert.Equal(new uint[] { 1002 }, world.Store.Chests[FirstChest].Items.Select(item => item.Id).ToArray());
        Assert.NotNull(world.Habbo.Inventory!.Furniture.GetItem(1000));
        Assert.NotNull(world.Habbo.Inventory.Furniture.GetItem(1001));
        Assert.Null(world.Habbo.Inventory.Furniture.GetItem(1002));
        Assert.Equal(2, world.Packets.Count(packet => packet.Header == Plus.Communication.Packets.Outgoing.ServerPacketHeader.FurniListAddComposer));
        Assert.Single(world.Packets, packet => packet.Header == 9346);
    }

    [Fact]
    public void GiveFurniLastInFirstOutTakesTheNewestItems()
    {
        using var world = new Scene();
        world.Furni(FirstChest, 3, sprites: [10, 10, 10]);
        var box = world.Box("wf_act_give_furni", FurniGive(0, 2, order: 2), [FirstChest]);
        Assert.True(box.Execute(world.Context()));
        Assert.Equal(new uint[] { 1002, 1001 }, world.Store.Received.Select(item => item.Id).OrderByDescending(id => id).ToArray());
        Assert.Equal(new uint[] { 1000 }, world.Store.Chests[FirstChest].Items.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void GiveFurniRandomOrderFollowsTheChestShuffle()
    {
        using var world = new Scene();
        world.Furni(FirstChest, 3, sprites: [10, 10, 10]);
        world.Store.Chests[FirstChest].RandomKeys[1000] = 30;
        world.Store.Chests[FirstChest].RandomKeys[1001] = 10;
        world.Store.Chests[FirstChest].RandomKeys[1002] = 20;
        var box = world.Box("wf_act_give_furni", FurniGive(0, 2, order: 0), [FirstChest]);
        Assert.True(box.Execute(world.Context()));
        Assert.Equal(new uint[] { 1001, 1002 }, world.Store.Received.Select(item => item.Id).OrderBy(id => id).ToArray());
        Assert.Equal(new uint[] { 1000 }, world.Store.Chests[FirstChest].Items.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void GiveFurniAllModeAndShortStockNeverDuplicateOrInventItems()
    {
        using var world = new Scene();
        world.Furni(FirstChest, 2, sprites: [10, 20]);
        var many = world.Box("wf_act_give_furni", FurniGive(0, 9, order: 1), [FirstChest]);
        Assert.True(many.Execute(world.Context()));
        Assert.Equal(2, world.Habbo.Inventory!.Furniture.GetItems.Count(item => item.Id is 1000 or 1001));
        Assert.Empty(world.Store.Chests[FirstChest].Items);
        Assert.False(many.Execute(world.Context()));
        Assert.Equal(2, world.Store.Received.Count);
        Assert.Equal(world.Store.Received.Count, world.Store.Received.Select(item => item.Id).Distinct().Count());

        world.Furni(101, 3, sprites: [10, 10, 10], firstId: 2000);
        var all = world.Box("wf_act_give_furni", FurniGive(1, 1, order: 1), [101]);
        Assert.True(all.Execute(world.Context()));
        Assert.Empty(world.Store.Chests[101].Items);
        Assert.Equal(5, world.Store.Received.Count);
    }

    [Fact]
    public void GiveFurniRollsBackWhenTheTransferFailsAndLeavesTheInventoryAlone()
    {
        using var world = new Scene();
        world.Furni(FirstChest, 2, sprites: [10, 20]);
        var before = world.Habbo.Inventory!.Furniture.GetItems.Select(item => item.Id).OrderBy(id => id).ToArray();
        var box = world.Box("wf_act_give_furni", FurniGive(0, 2, order: 1), [FirstChest]);
        world.Store.Throw = true;
        Assert.False(box.Execute(world.Context()));
        Assert.Equal(before, world.Habbo.Inventory.Furniture.GetItems.Select(item => item.Id).OrderBy(id => id).ToArray());
        Assert.Equal(2, world.Store.Chests[FirstChest].Items.Count);
        Assert.Equal(100, world.Habbo.Credits);
        world.Store.Throw = false;
        world.Store.Refuse = WiredChestFailure.FundsGone;
        Assert.False(box.Execute(world.Context()));
        Assert.Equal(2, world.Store.Chests[FirstChest].Items.Count);
        Assert.Empty(world.Store.Received);
        world.Store.Refuse = null;
        Assert.True(box.Execute(world.Context()));
        Assert.Equal(2, world.Store.Received.Count);
    }

    [Fact]
    public void GiveFurniNeedsAFurniChestAndUnlockedWiredAccess()
    {
        using var world = new Scene();
        world.Coins(FirstChest, 20);
        world.Furni(101, 2, sprites: [10, 10], locked: true);
        // A picked coin chest refuses the save (hhus require_furni_chests); the locked furni chest alone saves but gives nothing.
        var refused = (WiredChestAction)WiredChestBox.Create(world.Room, new() { Id = 9 }, WiredBoxRegistry.All.Single(entry => entry.CanonicalName == "wf_act_give_furni"), world.Module);
        Assert.True(WiredNativeEditorProjection.TryCompile(9, refused.Descriptor, world.Native("wf_act_give_furni", FurniGive(0, 1, order: 1), [FirstChest, 101], ""), out var mixed));
        Assert.False(refused.TryValidateConfiguration(mixed, out _, out var error));
        Assert.Equal("wiredfurni.error.require_furni_chests", error);
        var box = world.Box("wf_act_give_furni", FurniGive(0, 1, order: 1), [101]);
        Assert.False(box.Execute(world.Context()));
        Assert.Empty(world.Store.Requests);
        world.Store.Chests[101].Locked = false;
        Assert.True(box.Execute(world.Context()));
        Assert.Equal(100, world.Habbo.Credits);
        Assert.Equal(20, world.Store.Chests[FirstChest].Coins);
    }

    [Theory]
    [InlineData("wf_act_give_currency", new[] { 0, 1, 0, 1, 1, 11 }, true)]
    [InlineData("wf_act_give_currency", new[] { 0, 1, 0, 1, 1, 13 }, true)]
    [InlineData("wf_act_give_currency", new[] { 0, 1, 0, 1, 1, 12 }, false)]
    [InlineData("wf_act_give_currency", new[] { 0, 1, 0, 1, 1, 0 }, false)]
    [InlineData("wf_act_give_furni", new[] { 0, 1, 0, 1, 1, 0 }, true)]
    [InlineData("wf_act_give_furni", new[] { 1, 1, 0, 1, 0, 2 }, true)]
    [InlineData("wf_act_give_furni", new[] { 0, 1, 0, 1, 1, 3 }, false)]
    [InlineData("wf_act_give_furni", new[] { 0, 1, 0, 1, 1, -1 }, false)]
    [InlineData("wf_act_give_furni", new[] { 2, 1, 0, 1, 1, 1 }, false)]
    [InlineData("wf_act_give_furni", new[] { 0, 0, 0, 1, 1, 1 }, false)]
    [InlineData("wf_act_give_furni", new[] { 0, -5, 0, 1, 1, 1 }, false)]
    [InlineData("wf_act_give_furni", new[] { 0, 1, 2, 1, 1, 1 }, false)]
    [InlineData("wf_act_give_furni", new[] { 0, 1, 0, 1, 2, 1 }, false)]
    public void TheEditorFormsKeepToTheOctoberFieldRanges(string name, int[] owned, bool accepted)
    {
        using var world = new Scene();

        if (name == "wf_act_give_currency") {
            world.Coins(FirstChest, 0);
        }
        else {
            world.Furni(FirstChest, 0);
        }

        var box = (WiredChestAction)WiredChestBox.Create(world.Room, new() { Id = 9 }, WiredBoxRegistry.All.Single(entry => entry.CanonicalName == name), world.Module);
        var native = world.Native(name, owned, [FirstChest], "");
        Assert.Equal(accepted, WiredNativeEditorProjection.TryCompile(box.Item.Id, box.Descriptor, native, out var runtime)
            && box.TryValidateConfiguration(runtime, out _, out _));
    }

    [Fact]
    public void TheRewardPopupTextIsLimitedToTwoHundredCharacters()
    {
        using var world = new Scene();
        world.Coins(FirstChest, 0);
        var box = (WiredChestAction)WiredChestBox.Create(world.Room, new() { Id = 9 }, WiredBoxRegistry.All.Single(entry => entry.CanonicalName == "wf_act_give_currency"), world.Module);
        var owned = Currency(0, 1, 0);
        Assert.True(WiredNativeEditorProjection.TryCompile(9, box.Descriptor, world.Native("wf_act_give_currency", owned, [FirstChest], new('x', 200)), out var fits)
            && box.TryValidateConfiguration(fits, out _, out _));
        Assert.False(WiredNativeEditorProjection.TryCompile(9, box.Descriptor, world.Native("wf_act_give_currency", owned, [FirstChest], new('x', 201)), out var tooLong)
            && box.TryValidateConfiguration(tooLong, out _, out _));
    }

    private static int[] Currency(int mode, int amount, int show) => [mode, amount, 0, 1, show, 11];
    private static int[] FurniGive(int mode, int amount, int order) => [mode, amount, 0, 1, 0, order];

    private sealed class Chest
    {
        public WiredChestKind Kind;
        public int Coins;
        public bool Locked;
        public bool Enabled = true;
        public List<InventoryItem> Items = [];
        public Dictionary<uint, long> RandomKeys = [];
    }

    private sealed class FakeDatabaseFailure : DbException
    {
        public FakeDatabaseFailure() : base("Injected SQL failure") { }
    }

    private sealed class Ledger(Func<uint, uint> owner) : IWiredChestStore
    {
        public readonly Dictionary<uint, Chest> Chests = [];
        public readonly List<WiredChestTransfer> Requests = [];
        public readonly List<InventoryItem> Received = [];
        public WiredChestFailure? Refuse;
        public bool Throw;
        public WiredChestSnapshot? Load(Item item) => Chests.TryGetValue(item.Id, out var chest)
            ? new(item.Id, owner(item.Id), 42, chest.Kind, chest.Coins, 0, new()
            {
                Capacity = 1000,
                Locked = chest.Locked,
                WiredEnabled = chest.Enabled,
                PreviewMode = 5,
                PreviewAmount = 1
            }, [.. chest.Items])
            { RandomKeys = new Dictionary<uint, long>(chest.RandomKeys) }
            : null;
        public bool SaveSettings(Item chest, int userId, bool canModify, bool roomOwner, Func<WiredChestSettings, WiredChestSettings> change) => true;
        public WiredChestContract? LoadContract(Item item) => null;
        public bool SaveContract(Item item, WiredChestContract contract) => true;
        public WiredChestUpgradeResult Upgrade(Item chest, int userId, int count, int? liveCredits = null, int? liveDiamonds = null) => new(4);
        public WiredChestTransferResult Transfer(WiredChestTransfer request)
        {
            Requests.Add(request);

            if (Throw) {
                throw new FakeDatabaseFailure();
            }

            if (Refuse is { } refusal) {
                return WiredChestTransferResult.Refused(refusal);
            }

            // Plan everything against the stock first; nothing is written until every part of the reward is covered.
            var ids = request.ChestIds.Distinct().Order().ToArray();

            if (ids.Any(id => !Chests.TryGetValue(id, out var chest) || chest.Locked || !chest.Enabled)) {
                return WiredChestTransferResult.Refused(WiredChestFailure.NoOrLockedChests);
            }

            var coinTake = ids.ToDictionary(id => id, _ => 0);
            var itemTake = new List<(uint Chest, InventoryItem Item)>();
            var taken = new HashSet<uint>();

            foreach (var node in request.Reward) {
                var remaining = node.Amount;

                if (node.ItemType == null) {
                    foreach (var id in ids.Where(id => Chests[id].Kind == WiredChestKind.Coins)) {
                        var take = Math.Min(remaining, Chests[id].Coins - coinTake[id]);
                        coinTake[id] += take;
                        remaining -= take;
                    }
                }
                else {
                    var candidates = ids.Where(id => Chests[id].Kind == WiredChestKind.Furni)
                        .SelectMany(id => Chests[id].Items.Select(item => (Chest: id, Item: item)))
                        .Where(pair => !taken.Contains(pair.Item.Id) && WiredChestItemType.Of(pair.Item) == node.ItemType).ToArray();

                    if (request.Order == 2) {
                        Array.Reverse(candidates);
                    }
                    else if (request.Order == 0) {
                        candidates = [.. candidates.OrderBy(pair => Chests[pair.Chest].RandomKeys.GetValueOrDefault(pair.Item.Id)).ThenBy(pair => pair.Item.Id)];
                    }

                    foreach (var pair in candidates.Take(remaining)) {
                        itemTake.Add(pair);
                        taken.Add(pair.Item.Id);
                        remaining--;
                    }
                }

                if (remaining != 0) {
                    return WiredChestTransferResult.Refused(WiredChestFailure.FundsGone);
                }
            }

            var credits = (long)(request.LiveCredits ?? 0) + coinTake.Values.Sum(value => (long)value);

            if (credits is < 0 or > int.MaxValue) {
                return WiredChestTransferResult.Refused(WiredChestFailure.ExceedsCapacity);
            }

            foreach (var (id, take) in coinTake) {
                Chests[id].Coins -= take;
            }

            foreach (var (id, item) in itemTake) {
                Chests[id].Items.Remove(item);
                item.OwnerId = (uint)request.UserId;
                Received.Add(item);
            }

            return new(null, (int)credits, itemTake.Select(pair => pair.Item).ToArray(), [],
                new(request.Multiplier, 0, 0, itemTake.Count, coinTake.Values.Sum()));
        }
    }

    private sealed class Scene : IDisposable
    {
        public readonly WiredChestProtocolTests.World Inner = new();
        public readonly Ledger Store;
        public readonly WiredChestRoom Module;
        public Room Room => Inner.Room;
        public Habbo Habbo => Inner.Habbo;
        public RoomUser Actor => Inner.Actor;
        public List<(uint Header, byte[] Payload)> Packets => Inner.Packets;
        public readonly List<(Habbo Habbo, RoomUser Actor)> Others = [];
        private uint _nextItem = 1000;

        public Scene()
        {
            Store = new(_ => 1);
            Module = new(Room, Store, Inner.Clock, Inner.Events.Add);
            typeof(WiredComponent).GetField("_chests", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Room.GetWired(), Module);
            Inner.Habbo.Inventory = new() { Furniture = new([], []) };
        }

        private void Place(uint id)
        {
            var items = (ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling).GetField("_floorItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Room.GetRoomItemHandler())!;
            items.TryAdd(id, id == Inner.Chest.Id ? Inner.Chest : new Item { Id = id, OwnerId = 1, RoomId = 42, Definition = Inner.Chest.Definition });
        }

        public void Coins(uint id, int coins, bool locked = false, bool enabled = true)
        {
            Place(id);
            Store.Chests[id] = new() { Kind = WiredChestKind.Coins, Coins = coins, Locked = locked, Enabled = enabled };
        }

        public void Furni(uint id, int count, int[]? sprites = null, bool locked = false, uint firstId = 0)
        {
            Place(id);
            var first = firstId == 0 ? _nextItem : firstId;
            var chest = new Chest { Kind = WiredChestKind.Furni, Locked = locked };

            for (var index = 0; index < count; index++) {
                chest.Items.Add(new() { Id = first + (uint)index, OwnerId = 0, Definition = new() { Id = 5, SpriteId = sprites?[index] ?? 10, Type = ItemType.Floor, AllowTrade = true } });
            }

            _nextItem = first + (uint)count;
            Store.Chests[id] = chest;
        }

        public (Habbo Habbo, RoomUser Actor) AddUser(int id, int virtualId)
        {
            var habbo = new Habbo { Id = id, Credits = 100, CurrentRoom = Room, Inventory = new() { Furniture = new([], []) } };
            var (client, _) = HabbiconTestSupport.Client(habbo);
            habbo.Client = client;
            var actor = new RoomUser(id, 1, virtualId, Room, client, TestChatEmotions.Unused, TestRewardProgress.Unused);
            ((ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager).GetField("_users", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Room.GetRoomUserManager())!).TryAdd(virtualId, actor);
            Others.Add((habbo, actor));

            return (habbo, actor);
        }

        public WiredNativeEditorConfiguration Native(string name, int[] owned, uint[] chests, string text)
        {
            var descriptor = WiredBoxRegistry.All.Single(entry => entry.CanonicalName == name);

            return WiredNativeEditorProjection.DefaultNative(descriptor) with
            {
                OwnedIntParams = [.. owned.Select((value, index) => index == 3 ? WiredNativeAuxiliaryEditor.AirTarget(value) : value)],
                FurniSourceTypes = [100, 0],
                UserSourceTypes = [0, 0],
                PrimaryItems = [.. chests.Select(id => new WiredNativeItemReference(id, false))],
                Text = text
            };
        }

        public WiredChestAction Box(string name, int[] owned, uint[] chests, string text = "")
        {
            var box = (WiredChestAction)WiredChestBox.Create(Room, new() { Id = 9 }, WiredBoxRegistry.All.Single(entry => entry.CanonicalName == name), Module);
            WiredNativeTestSupport.Install(box, Native(name, owned, chests, text));

            return box;
        }

        public WiredRuntimeContext Context(params RoomUser[] actors) => ContextOf(actors.Length == 0 ? [Actor] : actors);

        public WiredRuntimeContext ContextOf(RoomUser[] actors)
        {
            var items = ((ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling).GetField("_floorItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Room.GetRoomItemHandler())!).Values.ToArray();
            var context = new WiredRuntimeContext(Room, new(WiredEventKind.Use) { Actor = actors.FirstOrDefault() }, new(() => items, () => [.. actors]), new Operations());

            foreach (var actor in actors) {
                context.Triggering.UserIds.Add(actor.VirtualId);
            }

            return context;
        }

        public void Dispose() { }
    }

    private sealed class Operations : IWiredRuntimeOperations
    {
        public bool CallStacks(WiredRuntimeContext context, IEnumerable<Item> targets, bool negative = false) => throw new NotSupportedException();
        public bool SendSignal(WiredRuntimeContext context, IEnumerable<Item> receivers, WiredSelection selection, bool negative = false) => throw new NotSupportedException();
        public void ResetTimers(IEnumerable<Item> targets) => throw new NotSupportedException();
    }
}
