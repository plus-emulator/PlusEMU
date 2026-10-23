using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Selector editor saves through the packet handler, the frame the editor reopens on, and the stored row read back.
public partial class ModernWiredRuntimeTests
{
    private const string FurniSourceKey = "wiredfurni.error.invalid_furni_source";
    private const string FurniSourceCountKey = "wiredfurni.error.invalid_furni_source_count";
    private const string UserSourceRefusedKey = "wiredfurni.error.invalid_user_source";
    private const string UserSourceCountKey = "wiredfurni.error.invalid_user_source_count";
    private static readonly int[] FreshNeighborhood = [1, 0, 0, 33554431, .. Enumerable.Repeat(0, 14)];

    private sealed record SelectorFrame(int[] Ints, string Text, int[] Furni, int[] Users, bool Filter, bool Invert);

    private static async Task<SelectorFrame> SelectorSave(MovementOracleSession f, int[] ints, string text = "", bool filter = false,
        bool invert = false, int[]? picks = null, int[]? furni = null, int[]? users = null)
    {
        f.Client.Packets.Clear();
        picks ??= [];
        furni ??= [];
        users ??= [];
        object[] body = [100, ints.Length, .. ints.Cast<object>(), text, picks.Length, .. picks.Cast<object>(), filter, invert,
            furni.Length, .. furni.Cast<object>(), users.Length, .. users.Cast<object>(), 0, 0];
        await new SaveWiredSelectorConfigEvent(new WiredConfigurationService(f.Store, null!, null!, TestLogging.For<WiredConfigurationService>()))
            .Parse(f.Client, WiredCanonicalProtocolTests.Packet(body));

        return Reopen(f);
    }

    private static SelectorFrame Reopen(MovementOracleSession f)
    {
        var frame = Assert.IsType<WiredNativeEditorConfiguration>(WiredEditorSnapshot.Capture(Assert.IsAssignableFrom<IWiredConfiguredItem>(f.Box)).Native);

        return new([.. frame.OwnedIntParams], frame.Text, [.. frame.FurniSourceTypes], [.. frame.UserSourceTypes], frame.Filter == true,
            frame.Inverse == true);
    }

    /// <summary>A save the hotel accepts, the frame it reopens on, and a check that its stored row loads back to that frame.</summary>
    private static async Task<SelectorFrame> SelectorSaved(string name, int[] ints, string text = "", bool filter = false, bool invert = false,
        int[]? picks = null, int[]? furni = null, int[]? users = null)
    {
        var f = new MovementOracleSession(name);
        var frame = await SelectorSave(f, ints, text, filter, invert, picks, furni ?? FurniDefaults(name), users ?? UserDefaults(name));
        AssertSaved(f, true, "");
        var reloaded = f.Store.Load(100, Assert.IsAssignableFrom<IWiredConfiguredItem>(f.Box).Descriptor)
            ?? throw new InvalidOperationException("The stored row did not load.");
        Assert.Equal(frame.Ints, reloaded.Origin!.Native.OwnedIntParams.ToArray());
        Assert.Equal(frame.Text, reloaded.Origin.Native.Text);

        return frame;
    }

    private static async Task SelectorRefused(string name, int[] ints, string key, int[]? furni = null, int[]? users = null)
    {
        var f = new MovementOracleSession(name);
        await SelectorSave(f, ints, furni: furni ?? FurniDefaults(name), users: users ?? UserDefaults(name));
        AssertSaved(f, false, key);
    }

    // A raw save keeps the sources the box shows when no other ones are sent.
    private static int[] FurniDefaults(string name) => [.. WiredNativeEditorProjection.Metadata(name).FurniDefaults];

    private static int[] UserDefaults(string name) => [.. WiredNativeEditorProjection.Metadata(name).UserDefaults];

    private static async Task<int[]> SavedInts(string name, params int[] ints) => (await SelectorSaved(name, ints)).Ints;

    private static void Same(int[] expected, int[] actual) => Assert.Equal(expected, actual);

    [Theory]
    [InlineData("wf_slc_furni_area", new[] { 1, 2, 3, 4 })]
    [InlineData("wf_slc_furni_bytype", new[] { 1 })]
    [InlineData("wf_slc_furni_signal", new int[0])]
    [InlineData("wf_slc_remote", new[] { 1, 2 })]
    [InlineData("wf_slc_users_onfurni", new int[0])]
    [InlineData("wf_slc_furni_altitude", new[] { 100, 1 })]
    [InlineData("wf_slc_users_bytype", new[] { 2 })]
    [InlineData("wf_slc_users_handitem", new[] { 5 })]
    public async Task ASelectorIgnoresATextItDoesNotRead(string name, int[] ints)
    {
        var frame = await SelectorSaved(name, ints, "abc");

        Assert.Equal(ints, frame.Ints);
        Assert.Equal("", frame.Text);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task FilterAndInvertAreStoredInEveryCombination(bool filter, bool invert)
    {
        var frame = await SelectorSaved("wf_slc_furni_area", [1, 2, 3, 4], filter: filter, invert: invert);

        Assert.Equal((filter, invert), (frame.Filter, frame.Invert));
    }

    [Theory]
    [InlineData("wf_slc_furni_area")]
    [InlineData("wf_slc_furni_signal")]
    [InlineData("wf_slc_furni_altitude")]
    [InlineData("wf_slc_users_bytype")]
    [InlineData("wf_slc_users_group")]
    [InlineData("wf_slc_users_handitem")]
    [InlineData("wf_slc_users_byaction")]
    public async Task ASourceOnABoxWithoutThatSlotIsRefusedWithTheCountKey(string name)
    {
        var ints = WiredNativeEditorProjection.Metadata(name).OwnedDefaults.ToArray();

        await SelectorRefused(name, ints, FurniSourceCountKey, furni: [100]);
        await SelectorRefused(name, ints, UserSourceCountKey, users: [0]);
    }

    [Fact]
    public async Task AreaPadsAShortListAndDropsAFifthInt()
    {
        Same([5, 5, 3, 0], await SavedInts("wf_slc_furni_area", 5, 5, 3));
        Same([0, 0, 0, 0], await SavedInts("wf_slc_furni_area"));
        Same([1, 2, 3, 4], await SavedInts("wf_slc_furni_area", 1, 2, 3, 4, 5));
        Same([64, 0, 0, 64], await SavedInts("wf_slc_furni_area", int.MaxValue, -1, int.MinValue, 65));
    }

    [Fact]
    public async Task FurniByTypeMatchesStateOnlyForTheExactValueOne()
    {
        Same([1], await SavedInts("wf_slc_furni_bytype", 1));

        foreach (var value in new[] { 0, 2, -1, 100, int.MaxValue }) {
            Same([0], await SavedInts("wf_slc_furni_bytype", value));
        }

        Same([0], await SavedInts("wf_slc_furni_bytype"));
        Same([1], await SavedInts("wf_slc_furni_bytype", 1, 1));
    }

    [Theory]
    [InlineData("wf_slc_furni_bytype")]
    [InlineData("wf_slc_remote")]
    [InlineData("wf_slc_users_onfurni")]
    public async Task FurniSourcesZeroPickedAndSignalAreTheOnlyOnesOffered(string name)
    {
        var ints = WiredNativeEditorProjection.Metadata(name).OwnedDefaults.ToArray();

        foreach (var source in new[] { 0, 100, 201 }) {
            Same([source], (await SelectorSaved(name, ints, furni: [source])).Furni);
        }

        foreach (var source in new[] { 101, 200, 901 }) {
            await SelectorRefused(name, ints, FurniSourceKey, furni: [source]);
        }

        await SelectorRefused(name, ints, FurniSourceCountKey, furni: [100, 100]);
        await SelectorRefused(name, ints, UserSourceCountKey, users: [0]);
    }

    [Fact]
    public async Task NeighborhoodStoresEighteenIntsAndFillsAShortListFromTheFreshFrame()
    {
        const string name = "wf_slc_furni_neighborhood";

        Assert.Equal(FreshNeighborhood, await SavedInts(name));
        Assert.Equal(FreshNeighborhood, await SavedInts(name, 1));
        Same([1, 2, 3, 33554431, .. Enumerable.Repeat(0, 14)], await SavedInts(name, 1, 2, 3));
        Same([1, 2, 3, 5, 5, 5, 5, 5, 5, 5, .. Enumerable.Repeat(0, 8)], await SavedInts(name, [1, 2, 3, .. Enumerable.Repeat(5, 7)]));
        Assert.Equal(FreshNeighborhood, await SavedInts(name, FreshNeighborhood[..17]));
    }

    [Fact]
    public async Task NeighborhoodClampsTheReferenceRootAndLastMaskWords()
    {
        const string name = "wf_slc_furni_neighborhood";
        int[] Mask(int reference, int x, int y) => [reference, x, y, 33554431, .. Enumerable.Repeat(0, 14)];

        Assert.Equal(Mask(0, 0, 0), await SavedInts(name, Mask(2, 0, 0)));
        Assert.Equal(Mask(0, 0, 0), await SavedInts(name, Mask(-1, 0, 0)));
        Assert.Equal(Mask(0, 64, -64), await SavedInts(name, Mask(0, 65, -65)));
        Assert.Equal(Mask(0, 64, -64), await SavedInts(name, Mask(0, int.MaxValue, int.MinValue)));
        Assert.Equal(Mask(0, -1, 1), await SavedInts(name, Mask(0, -1, 1)));
        Same([0, 0, 0, .. Enumerable.Repeat(-1, 13), 33554431, 0], await SavedInts(name, [0, 0, 0, .. Enumerable.Repeat(-1, 15)]));
        Same([1, 2, 3, .. Enumerable.Repeat(5, 14), 0], await SavedInts(name, [1, 2, 3, .. Enumerable.Repeat(5, 15)]));
        Same([0, 0, 0, int.MinValue, .. Enumerable.Repeat(0, 12), 256, 0],
            await SavedInts(name, [0, 0, 0, int.MinValue, .. Enumerable.Repeat(0, 12), 256]));
    }

    [Fact]
    public async Task NeighborhoodRefusesTheUnofferedUserSourcesWithTheirOwnKey()
    {
        const string name = "wf_slc_furni_neighborhood";

        Same([201], (await SelectorSaved(name, FreshNeighborhood, users: [201])).Users);
        Same([201], (await SelectorSaved(name, FreshNeighborhood, furni: [201])).Furni);
        await SelectorRefused(name, FreshNeighborhood, UserSourceRefusedKey, users: [100]);
        await SelectorRefused(name, FreshNeighborhood, UserSourceRefusedKey, users: [200]);
        await SelectorRefused(name, FreshNeighborhood, FurniSourceKey, furni: [101]);
    }

    [Fact]
    public async Task SignalAndUsersOnFurniDropEveryInt()
    {
        Assert.Empty(await SavedInts("wf_slc_furni_signal", 1, 2));
        Assert.Empty(await SavedInts("wf_slc_users_onfurni", 1, 2));
    }

    [Fact]
    public async Task RemoteStoresAnyPositiveTypeAsIntersectionAndNoNegativeCount()
    {
        const string name = "wf_slc_remote";

        Same([1, 0], await SavedInts(name, 2, 0));
        Same([1, 0], await SavedInts(name, 100, 0));
        Same([0, 0], await SavedInts(name, -1, 0));
        Same([0, 0], await SavedInts(name, 0, -1));
        Same([0, 0], await SavedInts(name, 0, int.MinValue));
        Same([0, int.MaxValue], await SavedInts(name, 0, int.MaxValue));
        Same([0, 0], await SavedInts(name));
        Same([1, 0], await SavedInts(name, 1));
        Same([1, 2], await SavedInts(name, 1, 2, 3));
    }

    [Fact]
    public async Task AltitudeClampsTheHeightAndKeepsComparisonsUpToFive()
    {
        const string name = "wf_slc_furni_altitude";

        Same([8000, 0], await SavedInts(name, 8001, 0));
        Same([8000, 0], await SavedInts(name, int.MaxValue, 0));
        Same([0, 0], await SavedInts(name, -1, 0));
        Same([0, 0], await SavedInts(name, int.MinValue, 0));

        foreach (var comparison in new[] { 3, 4, 5 }) {
            Same([100, comparison], await SavedInts(name, 100, comparison));
        }

        foreach (var comparison in new[] { 6, 100, -1, int.MaxValue }) {
            Same([100, 0], await SavedInts(name, 100, comparison));
        }

        Same([0, 0], await SavedInts(name));
        Same([100, 0], await SavedInts(name, 100));
        Same([100, 1], await SavedInts(name, 100, 1, 9));
    }

    [Fact]
    public async Task UsersByActionKeepsTheOfferedActionsNineAnd67()
    {
        const string name = "wf_slc_users_byaction";

        foreach (var action in new[] { 0, 1, 5, 9, 10, 11, 67 }) {
            Same([action], await SavedInts(name, action));
        }

        foreach (var action in new[] { 12, 13, 20, 66, 68, 100, 1000, -1, int.MaxValue }) {
            Same([0], await SavedInts(name, action));
        }

        Same([0], await SavedInts(name));
        Same([0], await SavedInts(name, 0, 1));
    }

    [Theory]
    [InlineData(10, "abc")]
    [InlineData(10, "18")]
    [InlineData(10, "-1")]
    [InlineData(10, " 5")]
    [InlineData(11, "dance 5")]
    [InlineData(11, "Dance 1")]
    [InlineData(11, "1")]
    [InlineData(0, "dance 1")]
    [InlineData(67, "abc")]
    public async Task UsersByActionStoresTheFilterTextAsSent(int action, string text)
    {
        var frame = await SelectorSaved("wf_slc_users_byaction", [action], text);

        Same([action], frame.Ints);
        Assert.Equal(text, frame.Text);
    }

    [Fact]
    public async Task UsersByActionStoresTheFirst200CharactersOfALongFilterText()
    {
        var text = new string('1', 1000);

        Assert.Equal(text[..200], (await SelectorSaved("wf_slc_users_byaction", [10], text)).Text);
    }

    [Fact]
    public async Task UsersByTypeClampsTheTypeFromOneToFour()
    {
        const string name = "wf_slc_users_bytype";

        foreach (var type in new[] { 1, 2, 3, 4 }) {
            Same([type], await SavedInts(name, type));
        }

        Same([1], await SavedInts(name, 0));
        Same([1], await SavedInts(name, -1));

        foreach (var type in new[] { 5, 6, 7, 8, 16, 100, int.MaxValue }) {
            Same([4], await SavedInts(name, type));
        }

        Same([1], await SavedInts(name));
        Same([1], await SavedInts(name, 1, 4));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("0", "0")]
    [InlineData("1", "1")]
    [InlineData("-1", "-1")]
    [InlineData("123456789", "123456789")]
    [InlineData("99999999999999", "99999999999999")]
    [InlineData("2147483647", "2147483647")]
    [InlineData("2147483648", "2147483648")]
    [InlineData("+5", "5")]
    [InlineData("007", "7")]
    [InlineData("abc", "")]
    [InlineData(" 5 ", "")]
    [InlineData("1,2", "")]
    [InlineData("1.5", "")]
    [InlineData("0x10", "")]
    public async Task UsersGroupStoresTheIdAsADecimalNumberOrClearsIt(string sent, string stored)
    {
        Assert.Equal(stored, (await SelectorSaved("wf_slc_users_group", [], sent)).Text);
    }

    [Fact]
    public async Task UsersGroupClearsAnIdTooLongForANumberAndDropsInts()
    {
        Assert.Equal("", (await SelectorSaved("wf_slc_users_group", [], new string('9', 100))).Text);
        Assert.Empty(await SavedInts("wf_slc_users_group", 1, 2));
    }

    [Fact]
    public async Task UsersHandItemStoresEveryValueAsSent()
    {
        const string name = "wf_slc_users_handitem";

        foreach (var item in new[] { int.MinValue, -100, -1, 0, 28, 100000, int.MaxValue }) {
            Same([item], await SavedInts(name, item));
        }

        Same([2], await SavedInts(name));
        Same([2], await SavedInts(name, 2, 3));
    }

    // Rows saved before these rules keep loading with the same runtime fields.
    [Theory]
    [InlineData("wf_slc_furni_area", "[1,2,3,4]", "", "[]", "[]", new[] { 1, 2, 3, 4, 0, 0 })]
    [InlineData("wf_slc_furni_bytype", "[1]", "", "[201]", "[]", new[] { 0, 1, 0, 0 })]
    [InlineData("wf_slc_remote", "[1,3]", "", "[100]", "[]", new[] { 0, 0, 1, 3, 100 })]
    [InlineData("wf_slc_furni_altitude", "[150,2]", "", "[]", "[]", new[] { 2, 0, 0 })]
    [InlineData("wf_slc_users_bytype", "[4]", "", "[]", "[]", new[] { 4, 0, 0 })]
    [InlineData("wf_slc_users_byaction", "[11]", "dance 2", "[]", "[]", new[] { 10, 0, 0, 1, 2, 0, 0 })]
    [InlineData("wf_slc_users_byaction", "[10]", "", "[]", "[]", new[] { 9, 0, 0, 0, 1, 0, 0 })]
    [InlineData("wf_slc_users_group", "[]", "12", "[]", "[]", new[] { 1, 12, 0, 0 })]
    [InlineData("wf_slc_users_handitem", "[-5]", "", "[]", "[]", new[] { -5, 0, 0 })]
    [InlineData("wf_slc_users_onfurni", "[]", "", "[0]", "[]", new[] { 0, 0, 0 })]
    public void AnOldStoredSelectorRowStillLoads(string name, string ints, string text, string furni, string users, int[] runtime)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var loaded = WiredConfigurationStore.DecodeRow(5, name, Row(descriptor.EditorCode, ints, false, false, furni, users, text: text));

        Assert.NotNull(loaded);
        Assert.Equal(runtime, loaded.IntParams.ToArray());
    }

    [Fact]
    public void AnOldSeventeenIntNeighborhoodRowLoadsAndReopensAsStored()
    {
        const string name = "wf_slc_furni_neighborhood";
        int[] old = [0, 2, -3, 511, .. Enumerable.Repeat(0, 13)];
        var json = Row(6, JsonSerializer.Serialize(old), false, true, "[100]", "[0]");
        var loaded = WiredConfigurationStore.DecodeRow(5, name, json);

        Assert.NotNull(loaded);
        Assert.Equal(old, loaded.IntParams.ToArray());
        Same(old, Reopen(new MovementOracleSession(name, row: new(100, name, 2, json))).Ints);
    }
}
