using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The Share and Import add-on editors. Observed on AIR WIN63-202610201032-107964540 (class_3963.as, VariableAddonShareAddon.as,
/// VariableAddonImportAddon.as): both open on [text connectors, subvariable creators, variable FX] all on and save the same way.
/// </summary>
public sealed class WiredShareImportEditorTests
{
    private static (bool Saved, WiredConfiguration Runtime, string Key) Save(string name, Func<WiredNativeEditorConfiguration, WiredNativeEditorConfiguration> change)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = change(WiredNativeEditorProjection.DefaultNative(descriptor));
        var key = WiredNativeEditorProjection.TryFillSourceSlots(descriptor, native, out native, out var countError) ? "" : countError ?? "";
        var runtime = new WiredConfiguration();
        var saved = key.Length == 0 && WiredNativeEditorProjection.TryCompile(1, descriptor, native, out runtime);

        if (!saved && key.Length == 0) {
            key = WiredNativeEditorProjection.SourceRejectionKey(descriptor, native) ?? "";
        }

        return (saved, runtime, key);
    }

    [Theory]
    [InlineData("wf_xtra_var_share")]
    [InlineData("wf_xtra_var_import")]
    public void AFreshAddonOpensWithEveryCategoryOn(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        Assert.Equal([1, 1, 1], WiredNativeEditorProjection.DefaultNative(descriptor).OwnedIntParams.ToArray());
    }

    [Theory]
    [InlineData("wf_xtra_var_share")]
    [InlineData("wf_xtra_var_import")]
    public void EveryOtherFlagValueIsStoredAsOn(string name)
    {
        Assert.Equal([1, 0, 0], Save(name, n => n with { OwnedIntParams = [2, 0, 0] }).Runtime.IntParams.ToArray());
        Assert.Equal([0, 1, 0], Save(name, n => n with { OwnedIntParams = [0, -1, 0] }).Runtime.IntParams.ToArray());
        Assert.Equal([0, 0, 1], Save(name, n => n with { OwnedIntParams = [0, 0, 99] }).Runtime.IntParams.ToArray());
    }

    [Theory]
    [InlineData("wf_xtra_var_share")]
    [InlineData("wf_xtra_var_import")]
    public void MissingFlagsStayOnAndExtraFlagsAreDropped(string name)
    {
        Assert.Equal([1, 1, 1], Save(name, n => n with { OwnedIntParams = [] }).Runtime.IntParams.ToArray());
        Assert.Equal([1, 1, 1], Save(name, n => n with { OwnedIntParams = [1] }).Runtime.IntParams.ToArray());
        Assert.Equal([0, 1, 1], Save(name, n => n with { OwnedIntParams = [0] }).Runtime.IntParams.ToArray());
        Assert.Equal([1, 0, 1], Save(name, n => n with { OwnedIntParams = [1, 0, 1, 1] }).Runtime.IntParams.ToArray());
    }

    [Theory]
    [InlineData("wf_xtra_var_share")]
    [InlineData("wf_xtra_var_import")]
    public void AStringIsAcceptedAndNotKept(string name)
    {
        var (saved, runtime, _) = Save(name, n => n with { Text = "hello" });

        Assert.True(saved);
        Assert.Equal("", runtime.Text);
    }

    [Theory]
    [InlineData("wf_xtra_var_share")]
    [InlineData("wf_xtra_var_import")]
    public void AFurniOrUserSourceIsRefusedWithItsCountKey(string name)
    {
        Assert.Equal((false, "wiredfurni.error.invalid_furni_source_count"), Save(name, n => n with { FurniSourceTypes = [100] }) is var furni ? (furni.Saved, furni.Key) : default);
        Assert.Equal((false, "wiredfurni.error.invalid_user_source_count"), Save(name, n => n with { UserSourceTypes = [0] }) is var user ? (user.Saved, user.Key) : default);
    }
}
