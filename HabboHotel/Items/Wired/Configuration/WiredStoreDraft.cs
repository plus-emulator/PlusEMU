using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// A save while its store rule edits it: the ints already cut and padded to the frame, and the text and quantifier as sent.
/// </summary>
internal sealed class WiredStoreDraft(WiredNativeEditorConfiguration native, ImmutableArray<int> defaults)
{
    public ImmutableArray<int>.Builder Ints { get; } = Fit(native.OwnedIntParams, defaults).ToBuilder();

    public string Text { get; set; } = native.Text;

    public int? Quantifier { get; set; } = native.Quantifier;

    // Missing ints take the frame default, extra ints are dropped.
    public static ImmutableArray<int> Fit(ImmutableArray<int> ints, ImmutableArray<int> defaults) =>
        [.. Enumerable.Range(0, defaults.Length).Select(index => index < ints.Length ? ints[index] : defaults[index])];

    public WiredNativeEditorConfiguration ToNative() =>
        native with { OwnedIntParams = Ints.ToImmutable(), Text = Text, Quantifier = Quantifier };
}
