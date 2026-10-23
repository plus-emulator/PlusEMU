using Plus.Utilities;
using System.Collections.Immutable;
using System.Globalization;
using Plus.HabboHotel.Items.DataFormat;

namespace Plus.HabboHotel.Items;

public abstract record FurnitureDataSnapshot(FurniDataStructure Structure)
{
    public sealed record Empty() : FurnitureDataSnapshot(FurniDataStructure.Empty);
    public sealed record Legacy(string Value) : FurnitureDataSnapshot(FurniDataStructure.Legacy);
    public sealed record Map(ImmutableArray<KeyValuePair<string, string>> Values) : FurnitureDataSnapshot(FurniDataStructure.Map);
    public sealed record Strings(ImmutableArray<string> Values) : FurnitureDataSnapshot(FurniDataStructure.StringArray);
    public sealed record Vote(string State, int Result) : FurnitureDataSnapshot(FurniDataStructure.VoteResult);
    public sealed record Integers(ImmutableArray<int> Values) : FurnitureDataSnapshot(FurniDataStructure.IntArray);
    public sealed record Highscore(string State, uint ScoreType, uint ClearType) : FurnitureDataSnapshot(FurniDataStructure.HighScore)
    {
        public ImmutableArray<HighscoreEntry> Entries { get; init; } = [];
    }
    public sealed record Crackable(string State, uint Hits, uint Target) : FurnitureDataSnapshot(FurniDataStructure.Crackable);

    public static FurnitureDataSnapshot Capture(IFurniObjectData data) => data switch
    {
        EmptyDataFormat => new Empty(),
        RecolorableDataFormat value => new Map(value.WireMap(value.Data)),
        LegacyDataFormat value => new Legacy(value.Data),
        MapDataFormat value => new Map(value.Data.ToImmutableArray()),
        StringArrayDataFormat value => new Strings(value.Data.ToImmutableArray()),
        VoteResultDataFormat value => new Vote(value.State, value.Result),
        IntArrayDataFormat value => new Integers(value.Data.ToImmutableArray()),
        HighscoreDataFormat value => new Highscore(value.State, value.ScoreType, value.ClearType) { Entries = value.CaptureEntries() },
        CrackableDataFormat value => new Crackable(value.State, value.Hits, value.Target),
        _ => throw new ArgumentOutOfRangeException(nameof(data), data.StructureType, "Unsupported furniture wire data.")
    };
}

public static class FurnitureWireData
{
    /// <summary>
    /// The data an ObjectDataUpdate carries for a state: legacy for ordinary furni, the full map for a recolorable, whose AIR
    /// logic paints legacy data white.
    /// </summary>
    public static FurnitureDataSnapshot ForState(IFurniObjectData data, string state) => data is RecolorableDataFormat recolorable
        ? new FurnitureDataSnapshot.Map(recolorable.WireMap(state))
        : new FurnitureDataSnapshot.Legacy(state);
}

public sealed record GuildFurnitureAppearance(int GroupId, string Badge, string Colour1, string Colour2);

public sealed record FurnitureMetadata(bool Stackable, bool IsSeat, bool IsBed, bool Walkable, int Width, int Length);

public sealed record RoomItemSnapshot(
    uint Id, int SpriteId, int X, int Y, int Rotation, string Z, string Height, int FloorExtra,
    FurnitureDataSnapshot Data, uint UniqueNumber, uint UniqueSeries, string WallCoordinates,
    string WallData, int UseButton, int UserId, string Username, FurnitureMetadata Metadata)
{
    public static RoomItemSnapshot Capture(Item item)
    {
        // Resolved before the lock: a group missing from the cache is loaded from the database.
        var guild = FindGuild(item);

        lock (item.NavSync) {
            return CaptureLocked(item, guild);
        }
    }

    private static GuildFurnitureAppearance? FindGuild(Item item) =>
        item.Definition.InteractionType is InteractionType.GuildItem or InteractionType.GuildGate or InteractionType.GuildForum
            ? item.GetRoom()?.FindGuildAppearance(item.GroupId)
            : null;

    // AIR usage policy: 0 nobody, 1 room controllers, 2 everybody. Wired boxes are always used (their editor opened) by
    // those with rights, whatever states their furni definition declares. Observed on AIR WIN63-202610151745-318700621: a fridge
    // (vending machine) and Floor Switch are usable by anyone, a football score board by nobody, and a game counter and a
    // one-state recolorable block by room controllers.
    private static int UsagePolicy(ItemDefinition definition) => definition.WiredDescriptor != null ? 1 : IsNeverUsed(definition) ? 0 : definition.InteractionType switch
    {
        // The other score board colours and Floor Switch 2 share the observed furni's server behaviour.
        InteractionType.VendingMachine or InteractionType.WfFloorSwitch1 or InteractionType.WfFloorSwitch2 => 2,
        InteractionType.Footballcounterred or InteractionType.Footballcounterblue or InteractionType.Footballcountergreen
            or InteractionType.Footballcounteryellow => 0,
        InteractionType.Counter or InteractionType.Recolorable => 1,
        _ => definition.Modes > 1 ? 1 : 0
    };

    /// <summary>
    /// Observed on AIR WIN63-202610201032-107964540 (usage read from a placed item, and no reaction to 6 double-clicks in each of 3 runs): the
    /// two plain game tiles, the pyramid, the blobs, the box, the buttons and the arrow, pressure and ring plates cannot be used, whatever
    /// number of states they have.
    /// </summary>
    internal static bool IsNeverUsed(ItemDefinition definition) => definition.ItemName is "wf_tile1" or "wf_tile2" or "wf_pyramid" or "wf_box"
        or "wf_arrowplate" or "wf_arrowplate_8dir" or "wf_pressureplate" or "wf_ringplate"
        || definition.ItemName.StartsWith("wf_blob", StringComparison.Ordinal) || definition.ItemName.StartsWith("wf_button", StringComparison.Ordinal);

    private static RoomItemSnapshot CaptureLocked(Item item, GuildFurnitureAppearance? guild)
    {
        var definition = item.Definition;
        var type = definition.InteractionType;
        var z = item.GetZ;
        var legacy = item.LegacyDataString;
        // Observed on AIR WIN63-202610151745-318700621: plain floor items carry extra 0.
        var extra = 0;

        if (type == InteractionType.WalkMagicTile) {
            extra = legacy.Split(';').Skip(1).FirstOrDefault() == "1" ? 1 : 0;
        }
        else if (type == InteractionType.Gift) {
            extra = GiftWrap.Style(legacy);
        }
        else if (type == InteractionType.MusicDisc) {
            var fields = legacy.Split('\n');

            if (fields.Length >= 7 && int.TryParse(fields[6], out var songId)) {
                extra = songId;
            }
        }

        // AIR's FurnitureGuildCustomizedLogic reads StringArray [state, groupId, badge, colour1, colour2]; it draws the badge
        // and colours and links the forum terminal to groupforum/<groupId>. Without its group the stored data stays.
        FurnitureDataSnapshot? guildData = guild == null ? null
            : new FurnitureDataSnapshot.Strings([legacy, guild.GroupId.ToString(CultureInfo.InvariantCulture), guild.Badge, guild.Colour1, guild.Colour2]);
        var data = item.IsWallItem ? new FurnitureDataSnapshot.Empty() : MagicTileHeight.IsMagicTile(type)
            ? new FurnitureDataSnapshot.Legacy(MagicTileHeight.ToWire(z).ToString(CultureInfo.InvariantCulture))
            : guildData ?? FurnitureDataSnapshot.Capture(item.ExtraData);

        return new(item.Id, definition.SpriteId, item.GetX, item.GetY, item.Rotation,
            TextHandling.GetString(z), TextHandling.GetString(item.StateHeight), extra,
            data, item.UniqueNumber, item.UniqueSeries, item.WallCoordinates ?? "",
            type == InteractionType.Postit ? legacy.Split(' ')[0] : legacy,
            UsagePolicy(definition), item.UserId, item.Username ?? "",
            new(definition.Stackable, definition.IsSeat, type == InteractionType.Bed, definition.Walkable, definition.Width, definition.Length));
    }
}

public sealed record FurnitureOwner(int Id, string Name);
public sealed record RoomFurnitureSnapshot(ImmutableArray<RoomItemSnapshot> Items, ImmutableArray<FurnitureOwner> Owners)
{
    public static RoomFurnitureSnapshot Capture(IEnumerable<Item> items, int roomOwnerId, string? roomOwnerName)
    {
        var snapshots = items.Select(RoomItemSnapshot.Capture).ToImmutableArray();
        var names = new Dictionary<int, string>();

        foreach (var item in snapshots) {
            var name = item.UserId == roomOwnerId ? roomOwnerName ?? "" : item.Username;

            if (!names.TryGetValue(item.UserId, out var existing) || (existing.Length == 0 && name.Length > 0)) {
                names[item.UserId] = name;
            }
        }

        var owners = ImmutableArray.CreateBuilder<FurnitureOwner>();

        if (names.Remove(roomOwnerId, out var ownerName)) {
            owners.Add(new(roomOwnerId, ownerName));
        }

        foreach (var id in names.Keys.OrderBy(id => id)) {
            owners.Add(new(id, names[id]));
        }

        return new(snapshots, owners.ToImmutable());
    }
}
