using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>A native editor save run through the same steps the save handler takes, with the frame the hotel reads back.</summary>
internal static class WiredR103EditorSave
{
    internal sealed record Result(bool Accepted, bool Dropped, string? Key, int[] Ints, int[] Furni, int[] Users, string Text, string[] Variables,
        int[] RuntimeInts);

    internal static Result Save(string name, int[] ints, int[]? furni = null, int[]? users = null, string? text = null,
        string[]? variables = null, int? delay = null)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        native = native with
        {
            OwnedIntParams = [.. ints],
            FurniSourceTypes = furni is null ? native.FurniSourceTypes : [.. furni],
            UserSourceTypes = users is null ? native.UserSourceTypes : [.. users],
            Text = text ?? native.Text,
            VariableIds = variables is null ? native.VariableIds : [.. variables],
            Delay = delay ?? native.Delay
        };

        if (!WiredNativeEditorProjection.TryFillSourceSlots(descriptor, native, out native, out var countError)) {
            return new(false, false, countError, [], [], [], "", [], []);
        }

        if (WiredNativeEditorProjection.DroppedWithoutReply(descriptor, native)) {
            return new(false, true, null, [], [], [], "", [], []);
        }

        if (!WiredNativeEditorProjection.TryCompile(1, descriptor, native, out var runtime, WiredNativeEditorProjection.AnyTriggerContext)) {
            return new(false, false, WiredNativeEditorProjection.SourceRejectionKey(descriptor, native), [], [], [], "", [], []);
        }

        var stored = runtime.Origin!.Native;

        return new(true, false, null, [.. stored.OwnedIntParams], [.. stored.FurniSourceTypes], [.. stored.UserSourceTypes], stored.Text,
            [.. stored.VariableIds], [.. runtime.IntParams]);
    }

    /// <summary>A save the hotel accepts; fails the test when the save is refused.</summary>
    internal static Result Saved(string name, int[] ints, int[]? furni = null, int[]? users = null, string? text = null)
    {
        var result = Save(name, ints, furni, users, text);
        Assert.True(result.Accepted, $"{name} [{string.Join(',', ints)}]: {Show(result)}");

        return result;
    }

    internal static string Show(Result r) => r.Accepted
        ? $"ok ints=[{string.Join(',', r.Ints)}] furni=[{string.Join(',', r.Furni)}] users=[{string.Join(',', r.Users)}] text='{r.Text}'"
        : r.Dropped ? "dropped" : $"refused key={r.Key ?? "generic"}";
}
