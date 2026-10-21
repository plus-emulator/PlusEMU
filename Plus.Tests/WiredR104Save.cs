using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A native editor save run through the steps of the save handler: fill the source slots, compile, then let the box validate and store it.
/// Returns what the box reads back, the frame the editor shows when it is opened again.
/// </summary>
internal static class WiredR104Save
{
    internal sealed record Result(bool Accepted, string? Key, int[] Ints, int[] Furni, int[] Users, string Text, int? Quantifier)
    {
        public override string ToString() => Accepted
            ? $"ok ints=[{string.Join(',', Ints)}] furni=[{string.Join(',', Furni)}] users=[{string.Join(',', Users)}] text='{Text}' q={Quantifier}"
            : $"refused key={Key ?? "generic"}";
    }

    internal static Result Save(G1World world, string name, int[]? ints = null, int[]? furni = null, int[]? users = null, string? text = null,
        int? quantifier = null, uint[]? picks = null, uint[]? secondary = null, string[]? variables = null)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var item = world.AddItem(name, name.StartsWith("wf_trg", StringComparison.Ordinal) ? InteractionType.WiredTrigger
            : name.StartsWith("wf_cnd", StringComparison.Ordinal) ? InteractionType.WiredCondition : InteractionType.WiredEffect);
        var box = world.Wired.CreateConfiguredBox(item) ?? throw new InvalidOperationException(name);
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        native = native with
        {
            OwnedIntParams = ints is null ? native.OwnedIntParams : [.. ints],
            FurniSourceTypes = furni is null ? native.FurniSourceTypes : [.. furni],
            UserSourceTypes = users is null ? native.UserSourceTypes : [.. users],
            VariableIds = variables is null ? native.VariableIds : [.. variables],
            Text = text ?? native.Text,
            Quantifier = quantifier ?? native.Quantifier,
            PrimaryItems = [.. (picks ?? []).Select(id => new WiredNativeItemReference(id, false))],
            SecondaryItems = [.. (secondary ?? []).Select(id => new WiredNativeItemReference(id, false))]
        };

        if (WiredSourceRules.Check(descriptor, native, WiredNativeEditorProjection.Metadata(name)).Key is { } sourceKey) {
            return Refused(sourceKey);
        }

        if (!WiredNativeEditorProjection.TryFillSourceSlots(descriptor, native, out native, out var countError)) {
            return Refused(countError);
        }

        if (!WiredNativeEditorProjection.TryCompile(item.Id, descriptor, native, out var runtime, WiredNativeEditorProjection.AnyTriggerContext)) {
            return Refused(WiredNativeEditorProjection.SourceRejectionKey(descriptor, native));
        }

        if (!WiredConfigurationSave.TrySave(box, runtime, TestWiredConfigurationStore.Instance, out var error, id => world.Floor.ContainsKey(id))) {
            return Refused(error);
        }

        var stored = WiredNativeEditorProjection.HotelFrame(descriptor, box.Configuration.Origin!.Native);

        return new(true, null, [.. stored.OwnedIntParams], [.. stored.FurniSourceTypes], [.. stored.UserSourceTypes], stored.Text, stored.Quantifier);
    }

    private static Result Refused(string? key) => new(false, key, [], [], [], "", null);
}
