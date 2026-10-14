using System.Collections.Concurrent;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>Passive speech-capture configuration, consumed before the speech trigger's literal predicate.</summary>
public sealed class WiredVariableTextInputBox(Room room, Item item, WiredBoxDescriptor descriptor) : IWiredConfiguredItem
{
    public Room Instance { get; set; } = room;
    public Item Item { get; set; } = item;
    public WiredBoxDescriptor Descriptor { get; } = descriptor with { Support = WiredBoxSupport.Implemented };
    public WiredConfiguration Configuration { get; private set; } = WiredNativeEditorProjection.DefaultRuntime(item.Id, descriptor) ?? new();
    public WiredBoxType Type => WiredBoxType.None;
    public ConcurrentDictionary<uint, Item> SetItems { get; set; } = new();
    public string StringData { get; set; } = "";
    public string ItemsData { get; set; } = "";
    public bool BoolData { get; set; }
    public bool TryValidateConfiguration(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
    {
        validated = proposed;
        error = "Choose a context variable and capturer name.";

        if (!WiredNativeEditorProjection.IsBound(Item.Id, Descriptor, proposed)
            || !WiredNativeEditorProjection.TryValidateRuntime(Item, Descriptor, proposed, out _, out error)) {
            return false;
        }

        // Observed on hhus (WIN63-202610131318): an empty placeholder name is refused with this key and no parameters, before the variable is checked.
        if (!NameLengthIsValid(proposed.Text)) {
            error = NameLengthKey;

            return false;
        }

        // The editor only lets letters, digits and "_" through (PlaceholderNameSection, "a-zA-Z_0-9 "), and the hotel has a key for a name
        // that is not made of them. Where it applies to the capturer is inferred; only the length key was seen.
        if (!NameSyntaxIsValid(proposed.Text)) {
            error = NameSyntaxKey;

            return false;
        }

        if (HasStackTwin(proposed.Text)) {
            error = StackTwinKey;

            return false;
        }

        error = "Choose a context variable and capturer name.";

        return proposed.Version == 1 && proposed.IntParams.Length == 1 && proposed.IntParams[0] is 1 or 2
            && proposed.VariableIds.Length == 1 && !WiredVariableAbsent.Is(proposed.VariableIds[0])
            && WiredVariableDescription.TryParseCatalogId(proposed.VariableIds[0], out var target, out _) && target == WiredVariableTarget.Context
            && NormalizeName(proposed.Text) == proposed.Text;
    }

    /// <summary>The capturer name as the player types it: optional surrounding "#" and padding are not part of the name.</summary>
    internal static string? NormalizeName(string text)
    {
        var name = text.Trim();

        if (name.Length >= 2 && name.StartsWith('#') && name.EndsWith('#')) {
            name = name[1..^1].Trim();
        }

        return name.Length is < 1 or > MaxNameLength || name.IndexOfAny(['\r', '\n', '#']) >= 0 ? null : name;
    }

    internal const string NameLengthKey = "wiredfurni.error.placeholder.name_length";
    internal const string NameSyntaxKey = "wiredfurni.error.placeholder.name_syntax";
    internal const string StackTwinKey = "wiredfurni.error.link.destination_conflict";

    private static string Stripped(string text)
    {
        var name = text.Trim();

        return name.Length >= 2 && name.StartsWith('#') && name.EndsWith('#') ? name[1..^1].Trim() : name;
    }

    /// <summary>Letters, digits and "_" only, once padding and surrounding "#" are dropped.</summary>
    internal static bool NameSyntaxIsValid(string text) => Stripped(text).All(c => c is '_' or >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z');

    /// <summary>Another capturer on this tile already has the name: each capturer must be unique in its stack.</summary>
    private bool HasStackTwin(string text)
    {
        var name = Stripped(text);

        if (name.Length == 0 || Instance?.GetGameMap() is not { } map || Instance.GetWired() is not { } wired) {
            return false;
        }

        return map.GetCoordinatedItems(new(Item.GetX, Item.GetY)).Where(other => other.Id != Item.Id)
            .Select(other => wired.TryGet(other.Id, out var found) ? found : null).OfType<IWiredConfiguredItem>()
            .Any(other => other.Descriptor.CanonicalName == Descriptor.CanonicalName
                && string.Equals(Stripped(other.Configuration.Text), name, StringComparison.OrdinalIgnoreCase));
    }
    // The hotel text reads "Placeholder length must be between 1 and 40 characters."
    internal const int MaxNameLength = 40;

    /// <summary>Whether the name, as the player types it, is 1 to 40 characters once padding and surrounding "#" are dropped.</summary>
    internal static bool NameLengthIsValid(string text)
    {
        var name = text.Trim();

        if (name.Length >= 2 && name.StartsWith('#') && name.EndsWith('#')) {
            name = name[1..^1].Trim();
        }

        return name.Length is >= 1 and <= MaxNameLength;
    }

    /// <summary>The captured context variable's current local definition id and the capture name, or none when it no longer resolves.</summary>
    internal bool TryCapture(WiredConfiguration config, WiredVariableModule authority, out uint definitionId)
    {
        definitionId = 0;

        return config.VariableIds.Length == 1 && authority.TryResolveCatalogId(config.VariableIds[0], WiredVariableTarget.Context, out var reference)
            && WiredVariableModule.TryDefinitionId(reference.Token, out definitionId);
    }
    public void ApplyConfiguration(WiredConfiguration validated)
    {
        if (!WiredNativeEditorProjection.IsBound(Item.Id, Descriptor, validated)) {
            throw new InvalidDataException("Unbound or altered native Wired runtime projection.");
        }

        Configuration = validated;
        StringData = validated.Text;
    }
    public void HandleSave(IIncomingPacket packet) => throw new InvalidOperationException("Capture boxes require validated persistence.");
    public bool Execute(params object[] arguments) => false;
}
