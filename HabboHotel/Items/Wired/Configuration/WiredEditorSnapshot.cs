using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Modern.Actions;

namespace Plus.HabboHotel.Items.Wired.Configuration;

public sealed record WiredEditorSnapshot(
    uint ItemId, int SpriteId, WiredBoxDescriptor Descriptor, WiredConfiguration Configuration,
    int FurniLimit, ImmutableArray<int> BlockedItems)
{
    public WiredNativeEditorConfiguration? Native { get; init; }

    /// <summary>The room variable catalog hash for forms with variable pickers; null for forms without any.</summary>
    public int? CatalogHash { get; init; }

    /// <summary>The shared variables a reference box may point at; null for every other form.</summary>
    public ImmutableArray<Plus.HabboHotel.Items.Wired.Variables.WiredNativeSharedVariable>? SharedVariables { get; init; }

    /// <summary>Context section 7 for message editors (Show Message, bot talk); null for every other form.</summary>
    public Plus.HabboHotel.Items.Wired.Runtime.WiredTextPlaceholders? TextPlaceholders { get; init; }

    /// <summary>Context section 8 for the Variable Leaderboard add-on; null for every other form.</summary>
    public Plus.HabboHotel.Items.Wired.Leaderboard.WiredLeaderboardContext? Leaderboard { get; init; }

    /// <summary>The user sources the stack's trigger adds to every user slot (10 under Bot Reaches User, 11 under Click User).</summary>
    public ImmutableArray<int> TriggerContextSources { get; init; } = [];

    /// <param name="sharedVariablesVisible">False for an editor who may not set a reference up: the list of the owner's other rooms stays empty.</param>
    public static WiredEditorSnapshot Capture(IWiredConfiguredItem box, bool sharedVariablesVisible = true)
    {
        var configuration = box.Configuration;

        if (!WiredNativeEditorProjection.TryProject(box.Item, box.Descriptor, configuration, out var native)) {
            throw new InvalidDataException("The saved settings have no supported native editor projection.");
        }

        // Pickers need the catalog hash; a failed capture sends the zero request hint, never a successful empty catalog.
        int? hash = WiredNativeEditorProjection.Metadata(box.Descriptor.CanonicalName).VariableDefaults.Length == 0 ? null
            : box.Instance.GetWired().Variables.TryCaptureNativeCatalog(out var catalog) && catalog is not null ? catalog.Hash : 0;

        return new(box.Item.Id, box.Item.Definition.SpriteId, box.Descriptor, configuration,
            WiredConfigurationLimits.EditorFurniLimit, [])
        {
            Native = WiredNativeEditorProjection.HotelFrame(box.Descriptor, native),
            CatalogHash = hash,
            SharedVariables = box.Descriptor.CanonicalName == "wf_var_reference"
                ? sharedVariablesVisible
                    ? [.. box.Instance.GetWired().Variables.Module.ListShared().Select(Plus.HabboHotel.Items.Wired.Variables.WiredNativeSharedVariable.Project)]
                    : []
                : null,
            TextPlaceholders = Plus.HabboHotel.Items.Wired.Runtime.WiredTextPlaceholders.Applies(box.Descriptor.CanonicalName)
                ? Plus.HabboHotel.Items.Wired.Runtime.WiredTextPlaceholders.Capture(box) : null,
            Leaderboard = box.Descriptor.CanonicalName == "wf_xtra_var_leaderboard" ? box.Instance.GetWired().LeaderboardContext(box) : null,
            TriggerContextSources = TriggerContext(box)
        };
    }

    public static ImmutableArray<int> TriggerContext(IWiredConfiguredItem box) => WiredNativeEditorProjection.TriggerContextSources(StackBoxNames(box));

    private static IEnumerable<string> StackBoxNames(IWiredConfiguredItem box)
    {
        var room = box.Instance;
        var wired = room.GetWired();

        return room.GetGameMap()?.GetCoordinatedItems(new(box.Item.GetX, box.Item.GetY))
            .Select(item => wired.TryGet(item.Id, out var found) ? found : null).OfType<IWiredConfiguredItem>()
            .Select(found => found.Descriptor.CanonicalName) ?? [];
    }
}
