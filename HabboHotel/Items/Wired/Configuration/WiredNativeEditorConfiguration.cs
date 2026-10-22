using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>The single stored authority for a canonical native editor save.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WiredNativeEditorConfiguration
{
    public int Version { get; init; } = 2;
    public WiredBoxCategory Category { get; init; }
    public int NativeCode { get; init; }
    public ImmutableArray<int> OwnedIntParams { get; init; } = [];
    public string Text { get; init; } = "";
    public ImmutableArray<WiredNativeItemReference> PrimaryItems { get; init; } = [];
    public ImmutableArray<WiredNativeItemReference> SecondaryItems { get; init; } = [];
    public ImmutableArray<int> FurniSourceTypes { get; init; } = [];
    public ImmutableArray<int> UserSourceTypes { get; init; } = [];
    public ImmutableArray<string> VariableIds { get; init; } = [];
    public int? Delay { get; init; }
    public int? Quantifier { get; init; }
    public bool? Filter { get; init; }
    public bool? Inverse { get; init; }
    public WiredNativeSavedState SavedState { get; init; } = new();

    /// <summary>
    /// The default record of a box that was never saved. It is never stored, so every stored row reads as saved. Observed on AIR
    /// WIN63-202610201032-107964540: after a room reload a never-saved box still reads 1 and a saved box 0. The flag after a pickup and
    /// placing again is not checked on Habbo.
    /// </summary>
    [JsonIgnore]
    public bool NeverSaved { get; init; }
}

public sealed record WiredNativeItemReference(uint ItemId, bool Wall)
{
    [JsonIgnore]
    public int WireId => Wall ? checked(-(int)BuildersClubItemIds.ToWire(ItemId)) : checked((int)BuildersClubItemIds.ToWire(ItemId));
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WiredNativeSavedState
{
    public ImmutableArray<WiredFurniSnapshot> Snapshots { get; init; } = [];

    /// <summary>The save named no ints, so its snapshot keeps no state although the stored int is the frame default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool StateDropped { get; init; }
}

/// <summary>The single authority behind an executable runtime: the native editor record it was compiled from.</summary>
internal sealed record WiredConfigurationOrigin(uint ItemId, string Name,
    WiredConfiguration Derived, WiredNativeEditorConfiguration Native);

public sealed record WiredNativeEditorMetadata(ImmutableArray<ImmutableArray<int>> FurniAllowed,
    ImmutableArray<ImmutableArray<int>> UsersAllowed, ImmutableArray<int> FurniDefaults,
    ImmutableArray<int> UserDefaults, ImmutableArray<int> OwnedDefaults, bool AllowWall)
{
    /// <summary>One empty slot per variable picker; an unchosen variable is the safe inactive default.</summary>
    public ImmutableArray<string> VariableDefaults { get; init; } = [];

    /// <summary>Condition footer: 0 none, 1 furni, 2 users, 3 variables (the AIR quantifier wording), and the invert flag.</summary>
    public int QuantifierType { get; init; }
    public bool Invert { get; init; }

    /// <summary>
    /// Source selectors per kind that earlier saves of the card carry, when the editor has since gained a second one (a reference
    /// selector). Such a save still compiles; the editor then opens it padded with the new selector's defaults.
    /// </summary>
    public int LegacySlots { get; init; }

    /// <summary>Reopen sends the primary picks in the iteration order of a Java HashSet of their ids, not in the saved order.</summary>
    public bool HashSetPickOrder { get; init; }
}
