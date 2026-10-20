using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

// Delay, sources and picks of Set Furni Highlight and Set Furni Visibility. Observed on AIR WIN63-202610201032-107964540.
public partial class ModernWiredRuntimeTests
{
    private const string DelayKey = "wiredfurni.error.invalid_delay";

    private static int[] ValidInts(string name) => name == Highlight ? [0, 0, 100, HighlightYellow, 0, 4, 0] : [0, 100, 0, 4, 0];

    [Theory]
    [InlineData(Highlight, 0)]
    [InlineData(Highlight, 5)]
    [InlineData(Highlight, 100)]
    [InlineData(Highlight, 120)]
    [InlineData(Visibility, 101)]
    [InlineData(Visibility, 120)]
    public async Task ADelayOfAtMost120PulsesIsSaved(string name, int delay)
    {
        var f = new MovementOracleSession(name);

        await SaveEffect(f, FurniVisualSave(ValidInts(name), delay: delay));

        AssertSaved(f, true, DelayKey);
    }

    [Theory]
    [InlineData(Highlight, 121)]
    [InlineData(Highlight, 200)]
    [InlineData(Highlight, 3600)]
    [InlineData(Visibility, 121)]
    [InlineData(Visibility, 199)]
    [InlineData(Visibility, 200)]
    public async Task ALongerDelayIsRefusedWithTheDelayKey(string name, int delay)
    {
        var f = new MovementOracleSession(name);

        await SaveEffect(f, FurniVisualSave(ValidInts(name), delay: delay));

        AssertSaved(f, false, DelayKey);
    }

    [Theory]
    [InlineData(Highlight, 0)]
    [InlineData(Highlight, 200)]
    [InlineData(Visibility, 201)]
    [InlineData(Visibility, 0)]
    public async Task EveryFurniSourceOfTheEditorIsSaved(string name, int source)
    {
        var f = new MovementOracleSession(name);

        await SaveEffect(f, FurniVisualSave(ValidInts(name), [], furniSources: [source]));

        AssertSaved(f, true, "");
        Assert.Equal([source], Assert.Single(f.Store.Saves).Origin!.Native.FurniSourceTypes.ToArray());
    }

    [Theory]
    [InlineData(Highlight)]
    [InlineData(Visibility)]
    public async Task TwoFurniSourcesAreRefusedWithTheCountKey(string name)
    {
        var f = new MovementOracleSession(name);

        await SaveEffect(f, FurniVisualSave(ValidInts(name), furniSources: [100, 200]));

        AssertSaved(f, false, "wiredfurni.error.invalid_furni_source_count");
    }

    [Theory]
    [InlineData(Highlight, new[] { 200, 201 }, new[] { 200, 201 })]
    [InlineData(Highlight, new[] { 201, 200 }, new[] { 201, 200 })]
    [InlineData(Visibility, new[] { 0, 200 }, new[] { 0, 200 })]
    // A single user source takes the default for the second slot.
    [InlineData(Highlight, new[] { 200 }, new[] { 200, 0 })]
    [InlineData(Visibility, new[] { 201 }, new[] { 201, 0 })]
    public async Task UserSourcesAreSavedAndTheSecondSlotKeepsItsDefault(string name, int[] saved, int[] stored)
    {
        var f = new MovementOracleSession(name);

        await SaveEffect(f, FurniVisualSave(ValidInts(name), userSources: saved));

        AssertSaved(f, true, "");
        Assert.Equal(stored, Assert.Single(f.Store.Saves).Origin!.Native.UserSourceTypes.ToArray());
    }

    [Theory]
    [InlineData(Highlight, new[] { 100, 0 }, "wiredfurni.error.invalid_user_source")]
    [InlineData(Highlight, new[] { 0, 100 }, "wiredfurni.error.invalid_user_source")]
    [InlineData(Visibility, new[] { 100, 0 }, "wiredfurni.error.invalid_user_source")]
    [InlineData(Highlight, new[] { 200, 201, 0 }, "wiredfurni.error.invalid_user_source_count")]
    [InlineData(Visibility, new[] { 0, 0, 0 }, "wiredfurni.error.invalid_user_source_count")]
    public async Task AUserSourceTheEditorDoesNotOfferIsRefusedWithItsKey(string name, int[] userSources, string key)
    {
        var f = new MovementOracleSession(name);

        await SaveEffect(f, FurniVisualSave(ValidInts(name), userSources: userSources));

        AssertSaved(f, false, key);
    }

    [Theory]
    [InlineData(Highlight)]
    [InlineData(Visibility)]
    public async Task AUnknownUserSourceIsDroppedWithoutAnyReply(string name)
    {
        var f = new MovementOracleSession(name);

        await SaveEffect(f, FurniVisualSave(ValidInts(name), userSources: [0, 77]));

        Assert.Empty(f.Client.Packets);
        Assert.Empty(f.Store.Saves);
    }

    [Theory]
    [InlineData(Highlight, 1)]
    [InlineData(Highlight, 20)]
    [InlineData(Visibility, 20)]
    public async Task UpToTwentyFurniCanBePicked(string name, int count)
    {
        var f = new MovementOracleSession(name, buildersClubPicks: [.. Enumerable.Range(20, 20).Select(id => (uint)id)]);

        await SaveEffect(f, FurniVisualSave(ValidInts(name), [.. Enumerable.Range(20, count)]));

        AssertSaved(f, true, "");
        Assert.Equal(count, Assert.Single(f.Store.Saves).SelectedItems.Length);
    }

    [Theory]
    [InlineData(Highlight)]
    [InlineData(Visibility)]
    public async Task TwentyOnePickedFurniAreDroppedWithoutAnyReply(string name)
    {
        var f = new MovementOracleSession(name, buildersClubPicks: [.. Enumerable.Range(20, 21).Select(id => (uint)id)]);

        await SaveEffect(f, FurniVisualSave(ValidInts(name), [.. Enumerable.Range(20, 21)]));

        Assert.Empty(f.Client.Packets);
        Assert.Empty(f.Store.Saves);
    }
}
