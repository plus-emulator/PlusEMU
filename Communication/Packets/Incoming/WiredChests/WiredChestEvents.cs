using Plus.Communication.Packets.Incoming.Rooms;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Chests;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.WiredChests;

// Parse the complete bounded payload before entering the room engine; rejected packets never write.
// Field orders are the official AIR composers of WIN63-202610131318-860371703 (package_95 / package_186), ids in
// ClientPacketHeader are the same messages on the current build; see reviews/chest-protocol-map.md.
public abstract class WiredChestPacketEvent : RoomPacketEvent
{
    protected abstract Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet);
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        try {
            var action = Read(room, session, packet);

            if (action != null && !packet.HasDataRemaining()) {
                room.GetWired().WithChests(action);
            }
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException or InvalidOperationException or IOException or InvalidDataException or OverflowException) { }

        return Task.CompletedTask;
    }
    protected static Item? Chest(Room room, uint id) => room.GetRoomItemHandler().GetItem(id) is { IsTemporary: false } item
        && WiredChestFurniture.IsChest(item.Definition) ? item : null;
    protected static void Settings(WiredChestRoom module, Room room, GameClient session, Item item, Func<WiredChestSettings, WiredChestSettings> change,
        bool notificationPreferences = false)
    {
        var saved = module.Store.SaveSettings(item, session.GetHabbo().Id, room.GetWired().Settings.CanModify(session), room.OwnerId == session.GetHabbo().Id, change);

        // AIR answers only a saved form (class_2436 closes it); a refused one stays open.
        if (saved) {
            session.Send(new WiredChestSettingsUpdatedComposer(item.Id, notificationPreferences));
        }

        // The stuff data is authoritative state, also on refusal: client options changed optimistically.
        module.Refresh(item);
    }
    protected static void Withdraw(WiredChestRoom module, Room room, GameClient session, Item item, WiredChestItemType? type, int amount, bool allFurni = false)
    {
        var chest = module.Read(item);

        if (chest == null) {
            return;
        }

        // Only the owner withdraws from a plain chest; others need an unlocked Wired chest and modify rights.
        if (!chest.CanWithdraw((uint)session.GetHabbo().Id, room.GetWired().Settings.CanModify(session))) {
            session.Send(new WiredTransactionFailComposer(chest.Settings.WiredEnabled ? WiredChestFailure.NoOrLockedChests : WiredChestFailure.Disabled));

            return;
        }

        var available = type == null && !allFurni ? chest.Coins : chest.Items.Count(furni => allFurni || WiredChestItemType.Of(furni) == type);
        var take = amount < 0 ? available : amount;

        if (take <= 0) {
            return;
        }

        var rewards = allFurni ? chest.Items.GroupBy(WiredChestItemType.Of).Select(group => new WiredChestNode(group.Count(), group.Key)).ToArray()
            : new[] { new WiredChestNode(take, type) };
        var result = module.Move(session, new()
        {
            RoomId = room.Id,
            UserId = session.GetHabbo().Id,
            SourceId = item.Id,
            ChestIds = [item.Id],
            Reward = rewards,
            CanModify = room.GetWired().Settings.CanModify(session)
        });

        session.Send(result.Failure is { } failure ? new WiredTransactionFailComposer(failure)
            : new WiredTransactionSuccessComposer(WiredTransactionSuccess.Withdraw));
    }
}

/// <summary>package_95.class_2544: int chestId.</summary>
public sealed class ChestOpenEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var item = Chest(room, packet.ReadUInt());

        return item == null ? null : module => module.Open(session, item);
    }
}
/// <summary>package_95.class_2979: int chestId.</summary>
public sealed class ChestCloseEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var item = Chest(room, packet.ReadUInt());

        return item == null ? null : module => module.Close(session, item);
    }
}
/// <summary>package_95.class_3668: int chestId; the deposit runs through the wired trade window.</summary>
public sealed class ChestStartDepositEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var item = Chest(room, packet.ReadUInt());

        return item == null ? null : module => module.Start(session, item.Id, [item], null);
    }
}
/// <summary>package_95.class_3881: int chestId. AIR sends it for coin chests too.</summary>
public sealed class ChestWithdrawAllEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var item = Chest(room, packet.ReadUInt());

        return item == null ? null : module => Withdraw(module, room, session, item, null, -1, WiredChestFurniture.Kind(item.Definition) == WiredChestKind.Furni);
    }
}
/// <summary>package_95.class_2388: int chestId, ChestItemType (bool wall, int typeId, string posterId), int amount.</summary>
public sealed class ChestWithdrawFurniEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var item = Chest(room, packet.ReadUInt());
        var type = new WiredChestItemType(packet.ReadBool(), packet.ReadInt(), packet.ReadString());
        var amount = packet.ReadInt();

        return item == null ? null : module => Withdraw(module, room, session, item, type, amount);
    }
}
/// <summary>package_95.class_3305: int chestId, int amount.</summary>
public sealed class ChestWithdrawCoinsEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var item = Chest(room, packet.ReadUInt());
        var amount = packet.ReadInt();

        return item == null || amount <= 0 ? null : module => Withdraw(module, room, session, item, null, amount);
    }
}
/// <summary>package_95.class_3275: int chestId, bool locked, bool autoLock, int capacity.</summary>
public sealed class ChestSaveOptionsEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var item = Chest(room, packet.ReadUInt());
        var locked = packet.ReadBool();
        var autoLock = packet.ReadBool();
        var capacity = packet.ReadInt();

        return item == null ? null : module => Settings(module, room, session, item, old => old with { Locked = locked, AutoLock = autoLock, Capacity = capacity });
    }
}
/// <summary>
/// package_95.class_3094: int chestId, string name, string desc, bool everyoneCanOpen, bool everyoneCanDonate, int stateMode,
/// int previewMode, int previewAmount, bool wiredEnabled. The Wired chest upgrade is this save with wiredEnabled set
/// (AIR ChestSettingsUI.confirmUpgrade); it is one-way, and the store refuses it for starter chests.
/// </summary>
public sealed class ChestSavePreferencesEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var item = Chest(room, packet.ReadUInt());
        var name = packet.ReadString();
        var description = packet.ReadString();
        var open = packet.ReadBool();
        var donate = packet.ReadBool();
        var state = packet.ReadInt();
        var preview = packet.ReadInt();
        var amount = packet.ReadInt();
        var wired = packet.ReadBool();

        return item == null ? null : module => Settings(module, room, session, item, old => old with
        {
            Name = name,
            Description = description,
            EveryoneCanOpen = open,
            EveryoneCanDonate = donate,
            StateMode = state,
            PreviewMode = preview,
            PreviewAmount = amount,
            WiredEnabled = old.WiredEnabled || wired
        });
    }
}
/// <summary>package_95.class_2747: int chestId, int notifyMode, bool chestFull, bool donation, bool someoneWithdraws, bool chestEmpty, bool wiredTransaction.</summary>
public sealed class ChestSaveNotificationsEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var item = Chest(room, packet.ReadUInt());
        var mode = packet.ReadInt();
        var full = packet.ReadBool();
        var donation = packet.ReadBool();
        var withdrawal = packet.ReadBool();
        var empty = packet.ReadBool();
        var wired = packet.ReadBool();

        return item == null ? null : module => Settings(module, room, session, item, old => old with
        { NotifyFull = full, NotifyDonation = donation, NotifyWithdrawal = withdrawal, NotifyEmpty = empty, NotifyTransaction = wired, NotifyMode = mode }, true);
    }
}
/// <summary>package_186.class_2956: bool remove, int count, int[] itemIds.</summary>
public sealed class WiredChestOfferItemsEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var remove = packet.ReadBool();
        var count = packet.ReadInt();

        if (count is < 0 or > 500) {
            return null;
        }

        var ids = new uint[count];

        for (var i = 0; i < count; i++) {
            ids[i] = packet.ReadUInt();
        }

        return module => module.Offer(session, ids, !remove);
    }
}
/// <summary>package_186.class_3394: bool confirm.</summary>
public sealed class WiredChestAcceptEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var final = packet.ReadBool();

        return module => module.Confirm(session, final);
    }
}
/// <summary>package_186.class_3259: no body.</summary>
public sealed class WiredChestCancelEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet) => module => module.Cancel(session);
}

/// <summary>package_95.class_3721: int chestId, int amount.</summary>
public sealed class ChestUpgradeEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var item = Chest(room, packet.ReadUInt());
        var count = packet.ReadInt();

        return item == null ? null : module => module.Upgrade(session, item, count);
    }
}

/// <summary>package_95.class_3508: bool lock, bool all.</summary>
public sealed class WiredChestLockEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var locked = packet.ReadBool();
        var all = packet.ReadBool();

        return module => module.Lock(session, locked, all);
    }
}
