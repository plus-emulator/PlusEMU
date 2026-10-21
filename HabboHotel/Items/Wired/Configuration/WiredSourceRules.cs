using System.Collections.Frozen;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// The answer to the source lists of a save, the same for every box with a native editor.
/// Observed on AIR WIN63-202610201032-107964540: a value Habbo does not know anywhere in either list is dropped without any reply.
/// Otherwise the furni list is checked first, then the user list. In each list a value its slot does not offer gets the source key, and
/// only then a list longer than the box has slots gets the count key. A shorter list is not refused: the box fills it from its defaults.
/// A box above the triggers keeps the trigger context sources 10 and 11 in any user slot.
/// </summary>
internal static class WiredSourceRules
{
    public const string FurniSourceKey = "wiredfurni.error.invalid_furni_source";
    public const string FurniSourceCountKey = "wiredfurni.error.invalid_furni_source_count";
    public const string UserSourceKey = "wiredfurni.error.invalid_user_source";
    public const string UserSourceCountKey = "wiredfurni.error.invalid_user_source_count";

    /// <summary>The user source values Habbo knows.</summary>
    public static readonly FrozenSet<int> KnownUser = new[]
    {
        WiredSources.Trigger, WiredSources.ReachedUser, WiredSources.ClickedUser, WiredSources.Selected, WiredSources.Snapshot,
        WiredSources.Selector, WiredSources.Signal, WiredSources.AllRoom
    }.ToFrozenSet();

    // The furni source values Habbo knows: the user values without the two trigger users, 110, the snapshot Place Furni opens on, and 901,
    // the furni a stack moved.
    private const int PlaceSnapshot = 110;

    private static readonly FrozenSet<int> KnownFurni = KnownUser.Where(source => source is not (WiredSources.ReachedUser or WiredSources.ClickedUser))
        .Append(PlaceSnapshot).Append(Modern.Addons.WiredProjectilePolicy.StackMovedSource).ToFrozenSet();

    /// <summary>What Habbo answers: nothing (<see cref="Dropped"/>), a key, or null when the sources are fine.</summary>
    public readonly record struct Verdict(bool Dropped, string? Key);

    public static Verdict Check(WiredBoxDescriptor descriptor, WiredNativeEditorConfiguration native, WiredNativeEditorMetadata metadata)
    {
        var furni = native.FurniSourceTypes.IsDefault ? [] : native.FurniSourceTypes;
        var users = native.UserSourceTypes.IsDefault ? [] : native.UserSourceTypes;

        if (furni.Any(source => !KnownFurni.Contains(source)) || users.Any(source => !KnownUser.Contains(source))) {
            return new(true, null);
        }

        var hasContext = descriptor.Category != WiredBoxCategory.Trigger;

        if (furni.Where((source, index) => index < metadata.FurniAllowed.Length && !metadata.FurniAllowed[index].Contains(source)).Any()) {
            return new(false, FurniSourceKey);
        }

        if (furni.Length > metadata.FurniAllowed.Length) {
            return new(false, FurniSourceCountKey);
        }

        if (users.Where((source, index) => index < metadata.UsersAllowed.Length && !metadata.UsersAllowed[index].Contains(source)
            && !(hasContext && source is WiredSources.ReachedUser or WiredSources.ClickedUser)).Any()) {
            return new(false, UserSourceKey);
        }

        return new(false, users.Length > metadata.UsersAllowed.Length ? UserSourceCountKey : null);
    }
}
