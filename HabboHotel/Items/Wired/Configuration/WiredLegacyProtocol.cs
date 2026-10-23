using System.Collections.Immutable;
using System.Text;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>Volt's established three-envelope ABI, shared by configured boxes of all six categories.</summary>
public static class WiredLegacyProtocol
{
    // The caller already read the owning floor item ID. Exactly one canonical body is accepted.
    public static bool TryRead(IIncomingPacket packet, WiredBoxCategory category, out WiredConfiguration configuration)
    {
        var accepted = TryReadNative(packet, category, out var native);
        configuration = accepted ? WireDraft(native) : new();

        return accepted;
    }

    internal static WiredConfiguration WireDraft(WiredNativeEditorConfiguration native) => new()
    {
        IntParams = native.OwnedIntParams,
        Text = native.Text,
        SelectedItems = native.PrimaryItems.Select(item => item.ItemId).ToImmutableArray(),
        SecondarySelectedItems = native.SecondaryItems.Select(item => item.ItemId).ToImmutableArray(),
        VariableIds = native.VariableIds,
        Delay = native.Delay ?? 0
    };

    public static bool TryReadNative(IIncomingPacket packet, WiredBoxCategory category, out WiredNativeEditorConfiguration native) =>
        TryReadNativeUnbounded(packet, category, out native) && WiredNativeEditorProjection.WithinBounds(native);

    /// <summary>Reads a whole save body and leaves the bounds check to the caller, which may know the box and relax a rule for it.</summary>
    public static bool TryReadNativeUnbounded(IIncomingPacket packet, WiredBoxCategory category, out WiredNativeEditorConfiguration native)
    {
        native = new();

        try {
            var owned = ReadInts(packet, WiredConfigurationLimits.IntParams);
            var text = packet.ReadString();
            var primary = ReadItems(packet);
            int? delay = category == WiredBoxCategory.Action ? packet.ReadInt() : null;
            int? quantifier = category == WiredBoxCategory.Condition ? packet.ReadInt() : null;
            bool? filter = category == WiredBoxCategory.Selector ? ReadBoolean(packet) : null;
            bool? inverse = category == WiredBoxCategory.Selector ? ReadBoolean(packet) : null;
            var furni = ReadInts(packet, WiredConfigurationLimits.IntParams);
            var users = ReadInts(packet, WiredConfigurationLimits.IntParams);
            var variableCount = ReadCount(packet, WiredConfigurationLimits.IntParams);
            var variables = ImmutableArray.CreateBuilder<string>(variableCount);

            for (var index = 0; index < variableCount; index++) {
                variables.Add(packet.ReadString());
            }

            var secondary = ReadItems(packet);
            native = new()
            {
                Category = category,
                OwnedIntParams = owned,
                Text = text,
                PrimaryItems = primary,
                SecondaryItems = secondary,
                FurniSourceTypes = furni,
                UserSourceTypes = users,
                VariableIds = variables.MoveToImmutable(),
                Delay = delay,
                Quantifier = quantifier,
                Filter = filter,
                Inverse = inverse
            };

            return !packet.HasDataRemaining();
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or InvalidDataException or OverflowException) {
            return false;
        }
    }

    private static bool ReadBoolean(IIncomingPacket packet)
    {
        if (!packet.HasDataRemaining()) {
            throw new EndOfStreamException("Missing native Wired boolean.");
        }

        return packet.ReadByte() switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidDataException("Invalid native Wired boolean.")
        };
    }

    private static int ReadCount(IIncomingPacket packet, int maximum)
    {
        var count = packet.ReadInt();

        return count is >= 0 && count <= maximum ? count : throw new InvalidDataException("Invalid native Wired count.");
    }

    private static ImmutableArray<int> ReadInts(IIncomingPacket packet, int maximum)
    {
        var count = ReadCount(packet, maximum);
        var values = ImmutableArray.CreateBuilder<int>(count);

        for (var index = 0; index < count; index++) {
            values.Add(packet.ReadInt());
        }

        return values.MoveToImmutable();
    }

    private static ImmutableArray<WiredNativeItemReference> ReadItems(IIncomingPacket packet)
    {
        var count = ReadCount(packet, WiredConfigurationLimits.SelectedItems);
        var items = ImmutableArray.CreateBuilder<WiredNativeItemReference>(count);

        for (var index = 0; index < count; index++) {
            var id = packet.ReadInt();

            if (id is 0 or int.MinValue) {
                throw new InvalidDataException("Invalid native furniture identity.");
            }

            items.Add(new(TemporaryItemIds.FromWire(BuildersClubItemIds.FromWire((uint)Math.Abs(id)), id < 0), id < 0));
        }

        return items.MoveToImmutable();
    }

    public static bool IsWithinLimits(WiredConfiguration configuration) =>
        configuration.Version == WiredConfiguration.CurrentVersion
        && !configuration.IntParams.IsDefault && configuration.IntParams.Length <= WiredConfigurationLimits.IntParams
        && configuration.Text != null && configuration.Text.Length <= WiredConfigurationLimits.TextCharacters
        && Encoding.UTF8.GetByteCount(configuration.Text) <= ushort.MaxValue
        && !configuration.SelectedItems.IsDefault && configuration.SelectedItems.Length <= WiredConfigurationLimits.SelectedItems
        && configuration.SelectedItems.All(id => id > 0)
        && !configuration.SecondarySelectedItems.IsDefault && configuration.SecondarySelectedItems.Length <= WiredConfigurationLimits.SelectedItems
        && configuration.SecondarySelectedItems.All(id => id > 0)
        && WiredConfigurationLimits.IsDelay(configuration.Delay)
        && configuration.SelectionCode is >= 0 and <= WiredConfigurationLimits.SelectionCode
        && (configuration.ScoreQuotaPerGame is null or >= 1 and <= 10)
        && (configuration.TemporaryPlacement?.IsWithinLimits() ?? true)
        && !configuration.VariableIds.IsDefault && configuration.VariableIds.Length <= WiredConfigurationLimits.IntParams
        && configuration.VariableIds.All(id => id != null && id.Length <= 1024)
        && !configuration.Snapshots.IsDefault && configuration.Snapshots.Length <= WiredConfigurationLimits.SelectedItems
        && configuration.Snapshots.All(snapshot => snapshot != null && double.IsFinite(snapshot.Z) && snapshot.State != null
            && (snapshot.Wall?.IsWithinLimits() ?? true))
        && configuration.FurniSources != null && configuration.FurniSources.Count <= WiredConfigurationLimits.IntParams
        && configuration.UserSources != null && configuration.UserSources.Count <= WiredConfigurationLimits.IntParams;

    // A save from before a card gained its reference selector opens with that selector on its default.
    private static IEnumerable<int> Padded(ImmutableArray<int> sources, ImmutableArray<int> defaults) =>
        sources.Length > 0 && sources.Length < defaults.Length ? sources.Concat(defaults.Skip(sources.Length)) : sources;

    public static void Write(IOutgoingPacket packet, uint itemId, int spriteId, WiredBoxDescriptor descriptor,
        WiredConfiguration configuration, int furniLimit, IReadOnlyList<int> blockedItems,
        WiredNativeEditorConfiguration? editor = null, int? catalogHash = null,
        IReadOnlyList<Plus.HabboHotel.Items.Wired.Variables.WiredNativeSharedVariable>? sharedVariables = null,
        Plus.HabboHotel.Items.Wired.Runtime.WiredTextPlaceholders? textPlaceholders = null,
        Plus.HabboHotel.Items.Wired.Leaderboard.WiredLeaderboardContext? leaderboard = null, ImmutableArray<int> contextSources = default,
        Plus.HabboHotel.Items.Wired.Variables.WiredVariableBoxContext? variableBox = null)
    {
        editor ??= configuration.Origin?.Native;

        if (editor == null || !WiredNativeEditorProjection.Supports(descriptor.CanonicalName)
            || editor.NativeCode != WiredNativeEditorProjection.Code(descriptor.CanonicalName)) {
            throw new InvalidDataException("This box has no proven canonical native editor projection.");
        }

        var metadata = WiredNativeEditorProjection.WithTriggerContext(WiredNativeEditorProjection.Metadata(descriptor.CanonicalName), contextSources);
        packet.WriteInteger(furniLimit);
        var picks = editor.PrimaryItems.Select(item => item.WireId);
        WriteInts(packet, metadata.HashSetPickOrder ? JavaHashSetOrder(picks, editor.PrimarySent) : picks);
        var secondary = editor.SecondaryItems.Select(item => item.WireId);
        WriteInts(packet, metadata.HashSetSecondaryPickOrder ? JavaHashSetOrder(secondary, editor.SecondarySent) : secondary);
        packet.WriteInteger(spriteId);
        packet.WriteUInteger(BuildersClubItemIds.ToWire(itemId));
        packet.WriteString(editor.Text);
        WriteInts(packet, editor.OwnedIntParams);
        packet.WriteInteger(editor.VariableIds.Length);

        foreach (var token in editor.VariableIds) {
            packet.WriteString(token);
        }

        WriteInts(packet, Padded(editor.FurniSourceTypes, metadata.FurniDefaults));
        WriteInts(packet, Padded(editor.UserSourceTypes, metadata.UserDefaults));
        packet.WriteInteger(editor.NativeCode);

        if (editor.Category == WiredBoxCategory.Action) {
            packet.WriteInteger(editor.Delay!.Value);
        }

        if (editor.Category == WiredBoxCategory.Condition) {
            packet.WriteInteger(editor.Quantifier!.Value);
        }

        // A selector's two flags follow its code, ahead of the advanced footer.
        if (editor.Category == WiredBoxCategory.Selector) {
            packet.WriteBoolean(editor.Filter!.Value);
            packet.WriteBoolean(editor.Inverse!.Value);
        }

        packet.WriteBoolean(true);
        WriteGroups(packet, metadata.FurniAllowed);
        WriteGroups(packet, metadata.UsersAllowed);
        WriteInts(packet, metadata.FurniDefaults);
        WriteInts(packet, metadata.UserDefaults);
        packet.WriteBoolean(metadata.AllowWall);

        if (editor.Category == WiredBoxCategory.Condition) {
            packet.WriteByte((byte)metadata.QuantifierType);
            packet.WriteBoolean(metadata.Invert);
        }

        // One context entry (type 0, the room's variable catalog) carries its hash; the client synchronizes the catalog by it.
        // Zero is the "unknown, request it" hint and is also what a failed capture sends. Cards without pickers carry no context.
        packet.WriteInteger((catalogHash is null ? 0 : 1) + (sharedVariables is null ? 0 : 1) + (textPlaceholders is null ? 0 : 1)
            + (leaderboard is null ? 0 : 1) + (variableBox is null ? 0 : 1));

        if (catalogHash is { } hash) {
            packet.WriteInteger(0);
            packet.WriteInteger(hash);
        }

        // Contexts 1 to 3 (VariableInfoAndHolders.as, VariableInfoAndValue.as): the box's variable, then its holders or the room variable's value.
        if (variableBox is not null) {
            packet.WriteInteger(variableBox.ContextType);
            variableBox.Variable.Write(packet, false);

            if (variableBox.Value is { } value) {
                packet.WriteInteger(value);
            }
            else {
                packet.WriteInteger(variableBox.Holders.Length);

                foreach (var (objectId, held) in variableBox.Holders) {
                    packet.WriteInteger(objectId);
                    packet.WriteInteger(held);
                }
            }
        }

        // Context 4: the variables other rooms share, each as its room and the standard variable block.
        if (sharedVariables is not null) {
            packet.WriteInteger(4);
            packet.WriteInteger(sharedVariables.Count);

            foreach (var shared in sharedVariables) {
                packet.WriteInteger(checked((int)shared.RoomId));
                packet.WriteString(shared.RoomName);
                shared.Variable.Write(packet, false);
            }
        }

        // Context 7: the message editor's placeholder names, then the stack's capturer names.
        if (textPlaceholders is not null) {
            packet.WriteInteger(Plus.HabboHotel.Items.Wired.Runtime.WiredTextPlaceholders.ContextType);
            packet.WriteInteger(textPlaceholders.PlaceholderNames.Length);

            foreach (var name in textPlaceholders.PlaceholderNames) {
                packet.WriteString(name);
            }

            packet.WriteInteger(textPlaceholders.CapturerNames.Length);

            foreach (var name in textPlaceholders.CapturerNames) {
                packet.WriteString(name);
            }
        }

        // Context 8 (class_4357.VARIABLE_LEADERBOARD; package_127/VariableLeaderboardContext.as:18-36 and VariableLeaderboardChoice.as:18-25):
        // overlay, stack tile, candidate count, then each offered variable's id, technical name, persistent, has value, level-up add-ons.
        if (leaderboard is not null) {
            packet.WriteInteger(Plus.HabboHotel.Items.Wired.Leaderboard.WiredLeaderboardContext.ContextType);
            packet.WriteInteger(leaderboard.OverlayInstanceId);
            packet.WriteInteger(leaderboard.TileX);
            packet.WriteInteger(leaderboard.TileY);
            packet.WriteInteger(leaderboard.TotalCandidateCount);
            packet.WriteInteger(leaderboard.Choices.Count);

            foreach (var choice in leaderboard.Choices) {
                packet.WriteString(choice.VariableId);
                packet.WriteString(choice.Description.Definition.Name);
                packet.WriteBoolean(choice.Persistent);
                packet.WriteBoolean(choice.Description.HasValue);
                packet.WriteInteger(choice.LevelSystem is null ? 0 : 1);
            }
        }

        WriteInts(packet, WiredNativeEditorProjection.FrameDefaults(descriptor.CanonicalName, metadata));
    }

    // The iteration order of the java.util.HashSet<Integer> Habbo holds a card's picks in. An id lands in bucket (h ^ (h >>> 16)) & (buckets - 1)
    // and a table doubles when it holds more than three quarters of its buckets. Ids in one bucket keep their order, and a split keeps it too.
    // Observed on AIR WIN63-202610201032-107964540: the set is built twice. First like the copy constructor of JDK 18 and older, with a
    // table sized from the number of ids sent, repeats counted (seven saves that repeated an id, also after the room unloaded; the sent count
    // is kept in the stored row), then again by adding the first set's ids one by one to a default set of 16 buckets. When no bucket gets a ninth
    // id, below 12 ids and from 13 ids the two steps give the order of one plain add loop; exactly 12 ids give a 16-bucket table filled in the
    // order of a 32-bucket one. When a bucket gets its ninth id, Java doubles a table of under 64 buckets at once (HashMap.treeifyBin).
    // Not modelled: a bucket whose chain gets another id in a table of 64 buckets (the 11th id in a bucket when the first step has 16 buckets,
    // the 10th when it has 32), where Java builds a tree. Habbo reached it with 11 ids and returned the sent order with one id moved first,
    // which was the id the JDK 21 tree puts first in 1 of 6 samples. Habbo was not probed for this tree case with 12 or more ids sent.
    private static IEnumerable<int> JavaHashSetOrder(IEnumerable<int> ids, int sentCount)
    {
        var sent = ids.ToArray();
        var copied = Fill(sent, TableSizeFor(Math.Max((int)(Math.Max(sentCount, sent.Length) / 0.75f) + 1, 16)));

        return Fill(copied, 16);
    }

    private static int TableSizeFor(int capacity)
    {
        var size = 1;

        while (size < capacity) {
            size <<= 1;
        }

        return size;
    }

    private static int[] Fill(IEnumerable<int> ids, int buckets)
    {
        const int treeifyThreshold = 8;
        var table = new List<int>[buckets];
        var size = 0;

        foreach (var id in ids) {
            var bucket = table[Bucket(id, table.Length)] ??= [];

            if (bucket.Contains(id)) {
                continue;
            }

            bucket.Add(id);
            size++;

            if (bucket.Count > treeifyThreshold && table.Length < 64) {
                table = Resize(table);
            }

            if (size > table.Length * 3 / 4) {
                table = Resize(table);
            }
        }

        return [.. table.Where(bucket => bucket != null).SelectMany(bucket => bucket!)];
    }

    private static int Bucket(int id, int buckets) => (int)(((uint)id ^ ((uint)id >> 16)) & (uint)(buckets - 1));

    private static List<int>[] Resize(List<int>[] table)
    {
        var grown = new List<int>[table.Length * 2];

        foreach (var id in table.Where(bucket => bucket != null).SelectMany(bucket => bucket)) {
            (grown[Bucket(id, grown.Length)] ??= []).Add(id);
        }

        return grown;
    }

    private static void WriteGroups(IOutgoingPacket packet, ImmutableArray<ImmutableArray<int>> groups)
    {
        packet.WriteInteger(groups.Length);

        foreach (var group in groups) {
            WriteInts(packet, group);
        }
    }

    private static void WriteInts(IOutgoingPacket packet, IEnumerable<int> values)
    {
        var array = values.ToArray();
        packet.WriteInteger(array.Length);

        foreach (var value in array) {
            packet.WriteInteger(value);
        }
    }
}
