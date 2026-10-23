using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Variable Value Match and Change Variable Value reopen in the hotel's stored form: seven ints and two variable slots, with an empty
// slot as "0".
public sealed class WiredVariableValueSaveShapeTests
{
    private const string ValueMatch = "wf_cnd_var_val_match";
    private const string ChangeValue = "wf_act_change_var_val";
    private const string UserId = "-100";
    private const string FurniState = "-110";

    [Fact]
    public void AValueMatchSavedWithSixIntsAndNoReferenceReopensWithSevenIntsAndAnEmptySlot()
    {
        using var save = new WiredRawSave(ValueMatch);
        var result = save.Save(ints: [0, 1, 0, 0, 5, 0], variables: [UserId, "n"]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([0, 1, 0, 0, 5, 0, 0], result.Ints);
        Assert.Equal([UserId, "0"], result.Variables);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("")]
    [InlineData(null)]
    public void AValueMatchReferenceSlotLeftEmptyOrOutReopensAsAnEmptySlot(string? reference)
    {
        using var save = new WiredRawSave(ValueMatch);
        var result = save.Save(ints: [0, 1, 0, 0, 5, 0, 0], variables: reference is null ? [UserId] : [UserId, reference]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([0, 1, 0, 0, 5, 0, 0], result.Ints);
        Assert.Equal([UserId, "0"], result.Variables);
    }

    [Fact]
    public void AChangeValueSavedWithSixIntsAndNoReferenceReopensWithSevenIntsAndAnEmptySlot()
    {
        using var save = new WiredRawSave(ChangeValue);
        var result = save.Save(ints: [0, 0, 0, 0, 7, -10], furni: [100, 200], variables: [FurniState, "n"]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([0, 0, 0, 0, 7, -10, 0], result.Ints);
        Assert.Equal([FurniState, "0"], result.Variables);
    }

    [Fact]
    public void AChangeValueSavedWithSevenIntsAndNoReferenceReopensWithAnEmptySlot()
    {
        using var save = new WiredRawSave(ChangeValue);
        var result = save.Save(ints: [0, 0, 0, 0, 1, -10, 0], furni: [100, 200], variables: [FurniState, "n"]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([0, 0, 0, 0, 1, -10, 0], result.Ints);
        Assert.Equal([FurniState, "0"], result.Variables);
    }

    // A row the old code stored keeps loading and reopens in the stored form.
    [Theory]
    [InlineData(ValueMatch, new[] { 0, 1, 0, 0, 5, 0 }, UserId)]
    [InlineData(ChangeValue, new[] { 0, 0, 0, 0, 7, -10 }, FurniState)]
    public void ASixIntRowWithAnEmptyReferenceStoredByTheOldCodeReopensInTheStoredForm(string name, int[] ints, string variable)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var row = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [.. ints],
            FurniSourceTypes = [100, 200],
            UserSourceTypes = [0, 200],
            VariableIds = [variable, "n"]
        };

        var runtime = Assert.IsType<WiredConfiguration>(WiredConfigurationStore.DecodeRow(7, name, JsonSerializer.Serialize(row)));
        var frame = WiredNativeEditorProjection.HotelFrame(descriptor, runtime.Origin!.Native);

        Assert.Equal([.. ints, 0], frame.OwnedIntParams.ToArray());
        Assert.Equal([variable, "0"], frame.VariableIds.ToArray());
    }
}
