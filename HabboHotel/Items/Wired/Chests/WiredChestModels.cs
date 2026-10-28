using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.HabboHotel.Items.Wired.Chests;

public enum WiredChestKind
{
    Furni, Coins
}
public enum WiredContractKind
{
    Payment = 0, Trade = 1, Reward = 2
}
public enum WiredChestFailure
{
    UserCancelled = 0, Invalid = 1, Timeout = 2, TradeCancelled = 3, AlreadyTrading = 4,
    Misconfig = 5, InsufficientFunds = 6, FundsGone = 7, UserCantTrade = 8, OwnerCantTrade = 9,
    Empty = 10, ChestFull = 11, Disabled = 12, ChestNotInRoom = 13, TooManyChests = 14,
    NoOrLockedChests = 15, CantGiveAllToMultipleUsers = 16, TooManyOffers = 17,
    TooFast = 18, ExceedsCapacity = 19, InternalError = 1000, DatabaseError = 1001
}

public enum WiredChestLogType
{
    Manual = 0, Wired = 1, ContractPayment = 2, ContractReward = 3, ContractTrade = 4, AutoWithdraw = 5
}

public sealed record WiredChestSettings
{
    public bool Locked { get; init; } = true;
    public bool AutoLock { get; init; } = true;
    public bool WiredEnabled { get; init; }
    public bool EveryoneCanOpen { get; init; }
    public bool EveryoneCanDonate { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int Capacity { get; init; }
    public int StateMode { get; init; }
    public int PreviewMode { get; init; }
    public int PreviewAmount { get; init; } = 1;
    public int NotifyMode { get; init; }
    public bool NotifyFull { get; init; }
    public bool NotifyDonation { get; init; }
    public bool NotifyWithdrawal { get; init; }
    public bool NotifyEmpty { get; init; }
    public bool NotifyTransaction { get; init; }
}

public readonly record struct WiredChestItemType(bool Wall, int SpriteId, string PosterId = "")
{
    public static WiredChestItemType Of(InventoryItem item) => new(item.IsWallItem, item.Definition.SpriteId,
        item.IsWallItem && item.Definition.SpriteId == 1 ? item.ExtraData.Serialize() : "");
    public static WiredChestItemType Of(Item item) => new(item.IsWallItem, item.Definition.SpriteId,
        item.IsWallItem && item.Definition.SpriteId == 1 ? item.ExtraData.Serialize() : "");
}

public sealed record WiredChestNode(int Amount, WiredChestItemType? ItemType = null);
public sealed record WiredChestContract
{
    public uint Id { get; init; }
    public WiredContractKind Kind { get; init; }
    public WiredChestNode[][]? Payment { get; init; }
    public WiredChestNode[]? Reward { get; init; }
    public int PaymentMode { get; init; }
    public string ReceiveText { get; init; } = "";
    public string Layout { get; init; } = "generic";
    public int RewardCategory { get; init; } = 11;
    public bool ShowDialog { get; init; }
    public string RewardText { get; init; } = "";
}

public sealed record WiredChestSnapshot(uint Id, uint OwnerId, uint RoomId, WiredChestKind Kind,
    int Coins, int Level, WiredChestSettings Settings, InventoryItem[] Items)
{
    public IReadOnlyDictionary<uint, long> RandomKeys { get; init; } = new Dictionary<uint, long>();
    public IReadOnlyDictionary<uint, long> DepositTransactions { get; init; } = new Dictionary<uint, long>();
    public int Amount => Kind == WiredChestKind.Coins ? Coins : Items.Length;
    public int MaximumCapacity => Kind == WiredChestKind.Coins ? 5000 * (Level + 1) : 1000 * (Level + 1);
    public bool Usable => Settings.WiredEnabled && !Settings.Locked;
    public bool CanWithdraw(uint userId, bool canModify) => userId == OwnerId || Usable && canModify;
    public bool CanDeposit(uint userId, bool canModify) => Settings.EveryoneCanDonate || CanWithdraw(userId, canModify);
    public bool CanOpen(uint userId, bool canRead) => userId == OwnerId || Settings.EveryoneCanOpen || canRead;
}

// One request moves all payment and reward together. The operation id survives retries.
public sealed record WiredChestTransfer
{
    public Guid OperationId { get; init; } = Guid.NewGuid();
    public uint RoomId { get; init; }
    public int UserId { get; init; }
    public uint SourceId { get; init; }
    public uint[] ChestIds { get; init; } = [];
    public int WalletDepositCredits { get; init; }
    public uint[] PaymentIds { get; init; } = [];
    public WiredChestNode[] Reward { get; init; } = [];
    public bool Wired { get; init; }
    public bool CanModify { get; init; }
    public int Multiplier { get; init; } = 1;
    public int Order { get; init; } = 1;
    public int? LiveCredits { get; init; }
    public WiredChestLogType LogType { get; init; }
}

/// <summary>A log page request. The amount is kept in 1..200 and the page is 1-based, as the hotel answers them.</summary>
public sealed record WiredChestLogQuery(uint RoomId, uint? ChestId, int Amount, int Page)
{
    public const int MaxAmount = 200;
    public static WiredChestLogQuery Room(uint roomId, int amount, int page) => new(roomId, null, Clamp(amount), Math.Max(1, page));
    public static WiredChestLogQuery Chest(uint roomId, uint chestId, int amount, int page) => new(roomId, chestId, Clamp(amount), Math.Max(1, page));
    private static int Clamp(int amount) => Math.Clamp(amount, 1, MaxAmount);
}

public sealed record WiredChestLogRow(long Id, uint RoomId, WiredChestLogType Type, int UserId, string UserName, long Timestamp,
    int ChestCount, int WithdrawFurni, int DepositFurni, int WithdrawCoins, int DepositCoins);

public sealed record WiredChestLogPage(bool ForChest, long ListId, int Total, int Page, int Amount, WiredChestLogRow[] Rows);

public sealed record WiredChestFigures(int Multiplier, int DepositFurni, int DepositCoins, int WithdrawalFurni, int WithdrawalCoins);
public sealed record WiredChestTransferResult(WiredChestFailure? Failure, int Credits, InventoryItem[] Received,
    uint[] Removed, WiredChestFigures Figures, bool Replayed = false)
{
    public bool Succeeded => Failure == null;
    public static WiredChestTransferResult Refused(WiredChestFailure reason) => new(reason, 0, [], [], new(1, 0, 0, 0, 0));
}

public interface IWiredChestStore
{
    WiredChestSnapshot? Load(Item chest);
    bool SaveSettings(Item chest, int userId, bool canModify, bool roomOwner, Func<WiredChestSettings, WiredChestSettings> change);
    WiredChestContract? LoadContract(Item item);
    bool SaveContract(Item item, WiredChestContract contract);
    WiredChestTransferResult Transfer(WiredChestTransfer request);
    WiredChestLogPage ReadLog(WiredChestLogQuery query);
    WiredChestUpgradeResult Upgrade(Item chest, int userId, int count, int? liveCredits = null, int? liveDiamonds = null);
}

public sealed record WiredChestUpgradeResult(int Code, int Credits = 0, int Diamonds = 0);
