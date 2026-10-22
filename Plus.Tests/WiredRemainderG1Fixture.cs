using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Runtime.CompilerServices;
using Plus.Communication.Flash;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Chests;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// An in-memory chest store that keeps the database store's promises: one operation plans everything first and then applies it in one step, so a
/// refusal or a failed commit leaves chests, inventory and wallet untouched, and a retried operation id replays its result.
/// </summary>
internal sealed class G1ChestStore : IWiredChestStore
{
    internal sealed class ChestState
    {
        public uint Id;
        public WiredChestKind Kind;
        public int Coins;
        public int Level;
        public uint OwnerId = 1;
        public WiredChestSettings Settings = new() { Capacity = 1000, Locked = false, WiredEnabled = true, PreviewMode = 5, PreviewAmount = 1 };
        public List<InventoryItem> Items = [];
    }

    public readonly Dictionary<uint, ChestState> Chests = [];
    public readonly Dictionary<uint, WiredChestContract> Contracts = [];
    public readonly List<WiredChestTransfer> Requests = [];
    private readonly Dictionary<Guid, WiredChestTransferResult> _done = [];
    public Func<int, uint, InventoryItem?> Inventory = (_, _) => null;
    /// <summary>Raised once, after planning and before applying: the database failing at commit.</summary>
    public bool FailNextCommit;
    public int Commits;

    public ChestState AddChest(uint id, WiredChestKind kind, int coins = 0, params InventoryItem[] items)
    {
        var state = new ChestState { Id = id, Kind = kind, Coins = coins, Items = [.. items] };

        if (kind == WiredChestKind.Coins) {
            state.Settings = state.Settings with { Capacity = 5000 };
        }

        Chests[id] = state;

        return state;
    }

    public WiredChestSnapshot? Load(Item chest)
    {
        if (!WiredChestFurniture.IsChest(chest.Definition) || !Chests.TryGetValue(chest.Id, out var state)) {
            return null;
        }

        return Snapshot(state, chest.RoomId);
    }

    private static WiredChestSnapshot Snapshot(ChestState state, uint roomId) =>
        new(state.Id, state.OwnerId, roomId, state.Kind, state.Coins, state.Level, state.Settings, [.. state.Items])
        {
            RandomKeys = state.Items.ToDictionary(item => item.Id, item => (long)item.Id)
        };

    public bool SaveSettings(Item chest, int userId, bool canModify, bool roomOwner, Func<WiredChestSettings, WiredChestSettings> change)
    {
        if (!Chests.TryGetValue(chest.Id, out var state)) {
            return false;
        }

        state.Settings = change(state.Settings);

        return true;
    }

    public WiredChestContract? LoadContract(Item item) => WiredChestFurniture.IsContract(item.Definition) && Contracts.TryGetValue(item.Id, out var contract)
        ? contract with { Id = item.Id, Kind = WiredChestFurniture.ContractKind(item.Definition) } : null;

    public bool SaveContract(Item item, WiredChestContract contract)
    {
        Contracts[item.Id] = contract;

        return true;
    }

    public WiredChestUpgradeResult Upgrade(Item chest, int userId, int count, int? liveCredits = null, int? liveDiamonds = null) => new(4);

    public WiredChestTransferResult Transfer(WiredChestTransfer request)
    {
        Requests.Add(request);

        if (request.WalletDepositCredits < 0 || request.ChestIds.Length is 0 or > 100 || request.PaymentIds.Length > 500
            || request.PaymentIds.Distinct().Count() != request.PaymentIds.Length || request.Multiplier is < 1 or > 500
            || request.Reward.Any(node => node.Amount < 1) || request.Order is < 0 or > 2) {
            return WiredChestTransferResult.Refused(WiredChestFailure.Invalid);
        }

        if (_done.TryGetValue(request.OperationId, out var replay)) {
            return replay with { Replayed = true, Received = [], Removed = [] };
        }

        var states = request.ChestIds.Distinct().Order().Select(id => Chests.GetValueOrDefault(id)).ToArray();

        if (states.Any(state => state == null)) {
            return WiredChestTransferResult.Refused(WiredChestFailure.ChestNotInRoom);
        }

        var chests = states.Select(state => Snapshot(state!, (uint)request.RoomId)).ToArray();

        if (chests.Any(chest => request.Wired ? !chest.Usable
            : (request.PaymentIds.Length != 0 || request.WalletDepositCredits > 0) && !chest.CanDeposit((uint)request.UserId, request.CanModify)
                || request.Reward.Length > 0 && !chest.CanWithdraw((uint)request.UserId, request.CanModify))) {
            return WiredChestTransferResult.Refused(WiredChestFailure.NoOrLockedChests);
        }

        var payment = request.PaymentIds.Select(id => Inventory(request.UserId, id)).ToArray();

        if (payment.Any(item => item == null || item.OwnerId != request.UserId || !item.Definition.AllowTrade
            || WiredChestFurniture.IsChest(item.Definition) || WiredChestFurniture.IsContract(item.Definition))) {
            return WiredChestTransferResult.Refused(WiredChestFailure.Invalid);
        }

        var offered = payment.Select(item => item!).ToArray();
        var vouchers = offered.Where(item => item.Definition.InteractionType == InteractionType.Exchange).ToArray();
        var wallet = request.LiveCredits ?? 0;

        if (request.WalletDepositCredits > wallet) {
            return WiredChestTransferResult.Refused(WiredChestFailure.InsufficientFunds);
        }

        var depositedCoins = vouchers.Sum(item => (long)item.Definition.BehaviourData) + request.WalletDepositCredits;

        if (depositedCoins > int.MaxValue || vouchers.Any(item => item.Definition.BehaviourData <= 0)) {
            return WiredChestTransferResult.Refused(WiredChestFailure.Invalid);
        }

        var deposits = new Dictionary<uint, ChestState>();
        var coinDeltas = states.ToDictionary(state => state!.Id, _ => 0L);
        var occupancy = chests.ToDictionary(chest => chest.Id, chest => (long)chest.Amount);

        foreach (var item in offered.Except(vouchers)) {
            var chest = chests.FirstOrDefault(chest => chest.Kind == WiredChestKind.Furni && occupancy[chest.Id] < chest.Settings.Capacity);

            if (chest == null) {
                return WiredChestTransferResult.Refused(WiredChestFailure.ChestFull);
            }

            deposits[item.Id] = Chests[chest.Id];
            occupancy[chest.Id]++;
        }

        var remainingCoins = depositedCoins;

        foreach (var chest in chests.Where(chest => chest.Kind == WiredChestKind.Coins)) {
            var put = Math.Min(remainingCoins, Math.Max(0, chest.Settings.Capacity - occupancy[chest.Id]));
            coinDeltas[chest.Id] += put;
            remainingCoins -= put;
        }

        if (remainingCoins != 0) {
            return WiredChestTransferResult.Refused(WiredChestFailure.ChestFull);
        }

        var availableCoins = chests.ToDictionary(chest => chest.Id, chest => chest.Coins);
        var availableItems = chests.Where(chest => chest.Kind == WiredChestKind.Furni).SelectMany(chest => chest.Items.Select(item => (Chest: chest.Id, Item: item))).ToList();
        var received = new List<(uint Chest, InventoryItem Item)>();
        long withdrawnCoins = 0;

        foreach (var node in request.Reward) {
            var remaining = node.Amount;

            if (node.ItemType == null) {
                foreach (var chest in chests.Where(chest => chest.Kind == WiredChestKind.Coins)) {
                    var take = Math.Min(remaining, availableCoins[chest.Id]);
                    availableCoins[chest.Id] -= take;
                    coinDeltas[chest.Id] -= take;
                    withdrawnCoins += take;
                    remaining -= take;
                }
            }
            else {
                var candidates = availableItems.Where(entry => WiredChestItemType.Of(entry.Item) == node.ItemType).ToList();

                if (request.Order == 2) {
                    candidates.Reverse();
                }

                foreach (var entry in candidates.Take(remaining)) {
                    received.Add(entry);
                    availableItems.Remove(entry);
                    remaining--;
                }
            }

            if (remaining != 0) {
                return WiredChestTransferResult.Refused(WiredChestFailure.FundsGone);
            }
        }

        var credits = (long)wallet - request.WalletDepositCredits + withdrawnCoins;

        if (credits is < 0 or > int.MaxValue || coinDeltas.Any(pair => Chests[pair.Key].Coins + pair.Value is < 0 or > int.MaxValue)) {
            return WiredChestTransferResult.Refused(WiredChestFailure.ExceedsCapacity);
        }

        if (FailNextCommit) {
            FailNextCommit = false;

            throw new DBConcurrencyException("The commit failed.");
        }

        foreach (var (itemId, chest) in deposits) {
            var item = offered.Single(entry => entry.Id == itemId);
            item.OwnerId = 0;
            chest.Items.Add(item);
        }

        foreach (var (chestId, item) in received) {
            Chests[chestId].Items.Remove(item);
            item.OwnerId = (uint)request.UserId;
        }

        foreach (var (chestId, delta) in coinDeltas) {
            Chests[chestId].Coins += (int)delta;
        }

        var result = new WiredChestTransferResult(null, (int)credits, received.Select(entry => entry.Item).ToArray(), request.PaymentIds,
            new(request.Multiplier, deposits.Count, (int)depositedCoins, received.Count, (int)withdrawnCoins));
        _done[request.OperationId] = result;
        Commits++;

        return result;
    }
}

internal sealed class G1Clock : TimeProvider
{
    public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    public long Milliseconds => (Now - DateTimeOffset.UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan span) => Now += span;
}

/// <summary>One logged-in user of the fixture room.</summary>
internal sealed class G1Player
{
    public required Habbo Habbo { get; init; }
    public required RoomUser User { get; init; }
    public required FlashGameClient Client { get; init; }
    public required List<(uint Header, byte[] Payload)> Packets { get; init; }
    public int Credits => Habbo.Credits;
    public bool Owns(uint id) => Habbo.Inventory!.Furniture.GetItem(id) != null;
    public int Sent(uint header) => Packets.Count(packet => packet.Header == header);
    public InventoryItem Give(InventoryItem item)
    {
        item.OwnerId = (uint)Habbo.Id;
        Habbo.Inventory!.Furniture.AddItem(item);

        return item;
    }
}

/// <summary>A witness effect: records every run and which user the stack ran for.</summary>
internal sealed class G1Witness(Room room, Item item) : WiredModernBox(room, item,
    WiredBoxRegistry.All.Single(entry => entry.CanonicalName == "wf_act_toggle_state")), IWiredContextualAction
{
    public List<WiredRuntimeEvent> Runs { get; } = [];
    /// <summary>Looks at the run while it is still going: variables captured earlier in it are only visible there.</summary>
    public Action<WiredRuntimeContext>? Probe { get; set; }
    public bool IsNegative => false;
    public override bool TryValidateConfiguration(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
    {
        validated = proposed;
        error = "";

        return true;
    }
    public override bool Execute(WiredRuntimeContext context)
    {
        Runs.Add(context.Event);
        Probe?.Invoke(context);

        return true;
    }
}

/// <summary>
/// A room with the real wired engine, the real chest module and boxes, and an in-memory chest store. Stacks are built from boxes on one tile and
/// fired through the engine, so a trigger runs its conditions, add-ons and effects the way the hotel runs them.
/// </summary>
internal sealed class G1World : IDisposable
{
    public const uint RoomId = 42;
    public readonly Room Room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
    public readonly WiredComponent Wired;
    public readonly G1ChestStore Store = new();
    public readonly G1Clock Clock = new();
    public readonly WiredChestRoom Module;
    public readonly List<WiredRuntimeEvent> Published = [];
    public readonly ConcurrentDictionary<uint, Item> Floor;
    private readonly ConcurrentDictionary<int, RoomUser> _users;
    private readonly Dictionary<int, Habbo> _habbos = [];
    private uint _next = 500;
    private int _virtual = 7;

    public G1World()
    {
        Room.Id = RoomId;
        Room.OwnerId = 1;
        Set(Room, "_interactionClock", TimeProvider.System);
        var items = new RoomItemHandling(Room, TestRoomItemStore.Instance, TestRoomItemMetadataStore.Instance, TestGameClientManager.Empty, TestLanguageManager.RoomItems, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);
        Set(Room, "_roomItemHandling", items);
        var users = new RoomUserManager(Room, TestRoomUserStore.Instance, Clock, new TestRewardProgress(), TestChatEmotions.Unused, TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel, TestItemRuntime.Settings);
        Set(Room, "_roomUserManager", users);
        var map = new Gamemap(Room, new RoomModel("g1", 0, 0, 0, 0, "000\r000\r000", 0, 0, true), TestLogging.Navigation, TestRoomSettings.Empty, TestGroupManager.Empty, TestNavigationDatabase.Instance, TestNavigationRewards.Instance);
        Set(Room, "_gamemap", map);
        typeof(Gamemap).GetProperty("GameMap")!.SetValue(map, new byte[3, 3]);
        typeof(Gamemap).GetProperty("EffectMap")!.SetValue(map, new byte[3, 3]);
        Wired = new WiredComponent(Room, TestLogging.Logger, Clock, TestRoomSettings.Empty, TestWiredRoomSettingsFactory.Instance, TestWiredConfigurationStore.Instance, TestWiredDatabase.Instance, TestWiredRewardService.Instance, TestBotManagementStore.Instance, TestWiredClients.Empty, TestGroupManager.Empty, TestWiredDefinitions.Unused, TestWiredCommands.Unused, TestWiredAccess.Unused, TestItemRuntime.Travel);
        Set(Room, "_wiredComponent", Wired);
        Set(Get(Wired, "_engine"), "_now", (Func<long>)(() => Clock.Milliseconds));
        Floor = (ConcurrentDictionary<uint, Item>)Get(items, "_floorItems");
        _users = (ConcurrentDictionary<int, RoomUser>)Get(users, "_users");
        Module = new(Room, Store, Clock, evt =>
        {
            Published.Add(evt);
            Wired.Dispatch(evt);
        });
        Set(Wired, "_chests", Module);
        Store.Inventory = (userId, id) => _habbos[userId].Inventory?.Furniture.GetItem(id);
    }

    /// <summary>The room's variable authority over these definitions, kept in memory: what a variable box would have loaded from the database.</summary>
    public WiredVariableModule UseVariables(params WiredVariableDefinition[] definitions)
    {
        var module = new WiredVariableModule(RoomId, new VariableDirectory(definitions), new MemoryWiredVariableStore(), Clock);
        var variables = (WiredRoomVariables)RuntimeHelpers.GetUninitializedObject(typeof(WiredRoomVariables));
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(variables, module);
        Set(Wired, "_variables", new Lazy<WiredRoomVariables>(() => variables));

        return module;
    }

    private sealed class VariableDirectory(WiredVariableDefinition[] definitions) : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => roomId == RoomId ? 1u : null;
        public WiredVariableDefinition? Find(uint itemId) => definitions.FirstOrDefault(definition => definition.ItemId == itemId);
    }

    public G1Player AddPlayer(int habboId = 1, int credits = 100)
    {
        var habbo = new Habbo { Id = habboId, Username = "user" + habboId, Credits = credits, CurrentRoom = Room, Inventory = new() { Furniture = new([], []) } };
        var (client, packets) = HabbiconTestSupport.Client(habbo);
        habbo.Client = client;
        var user = new RoomUser(habboId, (int)RoomId, _virtual, Room, client, TestChatEmotions.Unused, TestRewardProgress.Unused);
        _users.TryAdd(_virtual++, user);
        _habbos[habboId] = habbo;

        return new() { Habbo = habbo, User = user, Client = client, Packets = packets };
    }

    public Item AddItem(string interaction, InteractionType type, int x = 0, int y = 0, int spriteId = 0, bool wall = false, double z = 0)
    {
        var item = new Item
        {
            Id = _next++,
            OwnerId = 1,
            RoomId = RoomId,
            GetX = x,
            GetY = y,
            GetZ = z,
            ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" },
            Definition = new()
            {
                Id = _next,
                ItemName = interaction,
                InteractionName = interaction,
                InteractionType = type,
                SpriteId = spriteId,
                Type = wall ? ItemType.Wall : ItemType.Floor,
                Width = 1,
                Length = 1,
                Modes = 2,
                AdjustableHeights = [],
                VendingIds = [],
                PublicName = interaction
            }
        };
        Floor[item.Id] = item;
        typeof(Item).GetField("_room", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(item, Room);

        return item;
    }

    public Item AddChest(WiredChestKind kind, int coins = 0, int x = 2, int y = 2, params InventoryItem[] stock)
    {
        var item = AddItem(kind == WiredChestKind.Coins ? "wf_storage_coins1" : "wf_storage_furni1",
            kind == WiredChestKind.Coins ? InteractionType.WiredChestCoins : InteractionType.WiredChestFurni, x, y);
        Store.AddChest(item.Id, kind, coins, stock);

        return item;
    }

    public Item AddContract(WiredContractKind kind, WiredChestContract contract, int x = 2, int y = 1)
    {
        var item = AddItem("wf_contract_" + kind.ToString().ToLowerInvariant(), kind switch
        {
            WiredContractKind.Payment => InteractionType.WiredContractPayment,
            WiredContractKind.Reward => InteractionType.WiredContractReward,
            _ => InteractionType.WiredContractTrade
        }, x, y);
        Store.Contracts[item.Id] = contract with { Kind = kind };

        return item;
    }

    /// <summary>A catalogue furni of one type: its definition is shared so that equal sprites mean equal chest item types.</summary>
    public static InventoryItem Furni(uint id, int sprite = 44, uint owner = 0, bool tradeable = true) => new()
    {
        Id = id,
        OwnerId = owner,
        Definition = new() { Id = (uint)sprite, SpriteId = sprite, Type = ItemType.Floor, AllowTrade = tradeable }
    };

    public static InventoryItem Voucher(uint id, int credits) => new()
    {
        Id = id,
        Definition = new() { Id = 9000, SpriteId = 9000, Type = ItemType.Floor, AllowTrade = true, InteractionType = InteractionType.Exchange, BehaviourData = credits }
    };

    /// <summary>A box of the given card, configured from a native editor record, on the tile; it joins the engine like a loaded one.</summary>
    public IWiredConfiguredItem AddBox(string name, Func<WiredNativeEditorConfiguration, WiredNativeEditorConfiguration>? edit = null, int x = 0, int y = 0, double z = 0)
    {
        var item = AddItem(name, name.StartsWith("wf_trg", StringComparison.Ordinal) ? InteractionType.WiredTrigger
            : name.StartsWith("wf_cnd", StringComparison.Ordinal) ? InteractionType.WiredCondition
            : name.StartsWith("wf_xtra", StringComparison.Ordinal) ? InteractionType.WiredAddon : InteractionType.WiredEffect, x, y, 0, false, z);
        var box = Wired.CreateConfiguredBox(item) ?? throw new InvalidOperationException(name);

        if (edit != null) {
            Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
            WiredNativeTestSupport.Install(box, edit(WiredNativeEditorProjection.DefaultNative(descriptor)));
        }

        Assert.True(Wired.AddBox(box));

        return box;
    }

    /// <summary>The native save path of a fresh box: compile the edited record, then let the box validate it.</summary>
    public bool TrySave(string name, Func<WiredNativeEditorConfiguration, WiredNativeEditorConfiguration> edit) => TrySave(name, edit, out _);

    public bool TrySave(string name, Func<WiredNativeEditorConfiguration, WiredNativeEditorConfiguration> edit, out WiredConfiguration stored)
    {
        var item = AddItem(name, name.StartsWith("wf_trg", StringComparison.Ordinal) ? InteractionType.WiredTrigger
            : name.StartsWith("wf_cnd", StringComparison.Ordinal) ? InteractionType.WiredCondition
            : name.StartsWith("wf_xtra", StringComparison.Ordinal) ? InteractionType.WiredAddon : InteractionType.WiredEffect);
        var box = Wired.CreateConfiguredBox(item) ?? throw new InvalidOperationException(name);
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        stored = new();

        return WiredNativeEditorProjection.TryCompile(item.Id, descriptor, edit(WiredNativeEditorProjection.DefaultNative(descriptor)), out var runtime)
            && box.TryValidateConfiguration(runtime, out stored, out _);
    }

    public G1Witness AddWitness(int x, int y)
    {
        var item = AddItem("wf_act_toggle_state", InteractionType.WiredEffect, x, y);
        var witness = new G1Witness(Room, item);
        Assert.True(Wired.AddBox(witness));

        return witness;
    }

    /// <summary>Queues the event and lets the engine run until it settles.</summary>
    public void Fire(WiredRuntimeEvent @event)
    {
        Wired.Dispatch(@event);
        Flush();
    }

    public void Flush()
    {
        for (var i = 0; i < 4; i++) {
            Wired.OnFastCycle();
        }
    }

    public void Advance(TimeSpan span)
    {
        Clock.Advance(span);
        Flush();
    }

    public static WiredNativeEditorConfiguration Chest(string name, int[] ints, int[] targetSlots, int[] furniSources, int[] userSources, string[] variables,
        uint[] primary, uint[] secondary) => WiredNativeTestSupport.Chest(name, ints, targetSlots, furniSources, userSources, variables, primary, secondary);

    public void Dispose() => Wired.Cleanup();

    private static object Get(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
    private static void Set(object value, string name, object data) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(value, data);
}
