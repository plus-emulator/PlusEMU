using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Saves of Set Furni Highlight, Set Furni Visibility and Room Initialized, answered the way Habbo answers them.
// Observed on AIR WIN63-202610201032-107964540.
public partial class ModernWiredRuntimeTests
{
    private const string RoomInitialized = "wf_trg_room_initializes";

    // The save body of an action: item, ints, text, picks, delay, furni sources, user sources, variables, second picks.
    private static object[] ActionSave(int[] ints, string text = "", int[]? picks = null, int delay = 0, int[]? furniSources = null,
        int[]? userSources = null)
    {
        picks ??= [];
        furniSources ??= [100];
        userSources ??= [0, 0];

        return [100, ints.Length, .. ints.Cast<object>(), text, picks.Length, .. picks.Cast<object>(), delay, furniSources.Length,
            .. furniSources.Cast<object>(), userSources.Length, .. userSources.Cast<object>(), 0, 0];
    }

    private static Task SaveTrigger(MovementOracleSession f, int[] ints, string text)
    {
        f.Client.Packets.Clear();
        object[] body = [100, ints.Length, .. ints.Cast<object>(), text, 0, 0, 0, 0, 0];

        return new SaveWiredTriggerConfigEvent(new WiredConfigurationService(f.Store, null!, new ReplacingFilter("bobba", "bobba"),
            TestLogging.For<WiredConfigurationService>())).Parse(f.Client, Request(body));
    }

    [Theory]
    [InlineData(Highlight)]
    [InlineData(Visibility)]
    public void TheEditorOffersWallFurni(string name) => Assert.True(WiredNativeEditorProjection.Metadata(name).AllowWall);

    [Theory]
    [InlineData(Highlight)]
    [InlineData(Visibility)]
    public async Task AStringParameterIsAcceptedAndIgnored(string name)
    {
        var f = new MovementOracleSession(name);

        await SaveEffect(f, ActionSave(ValidInts(name), "hello", [8]));

        AssertSaved(f, true, "");
        var saved = Assert.Single(f.Store.Saves).Origin!.Native;
        Assert.Equal("", saved.Text);
        Assert.Equal(ValidInts(name), saved.OwnedIntParams.ToArray());
    }

    [Theory]
    [InlineData(new int[0], "")]
    [InlineData(new[] { 1, 2, 3 }, "")]
    [InlineData(new int[0], "hello")]
    public async Task RoomInitializedAcceptsAndIgnoresIntsAndAString(int[] ints, string text)
    {
        var f = new MovementOracleSession(RoomInitialized);

        await SaveTrigger(f, ints, text);

        AssertSaved(f, true, "");
        var saved = Assert.Single(f.Store.Saves).Origin!.Native;
        Assert.Empty(saved.OwnedIntParams);
        Assert.Equal("", saved.Text);
    }

    [Theory]
    [InlineData(Highlight, -1)]
    [InlineData(Visibility, -1)]
    [InlineData(Highlight, -49)]
    public async Task ANegativeDelayIsRefusedWithTheDelayKey(string name, int delay)
    {
        var f = new MovementOracleSession(name);

        await SaveEffect(f, ActionSave(ValidInts(name), delay: delay));

        AssertSaved(f, false, DelayKey);
    }

    // Only the two boxes of the evidence change: another box still drops a negative delay without a reply.
    [Fact]
    public async Task ANegativeDelayOnAnotherBoxIsStillDroppedWithoutAReply()
    {
        var f = new MovementOracleSession("wf_act_toggle_state");

        await SaveEffect(f, ActionSave([0], delay: -1, userSources: []));

        Assert.Empty(f.Client.Packets);
        Assert.Empty(f.Store.Saves);
    }

    [Fact]
    public async Task ADelayAbove3600IsStillDroppedWithoutAReply()
    {
        var f = new MovementOracleSession(Highlight);

        await SaveEffect(f, ActionSave(ValidInts(Highlight), delay: 3601));

        Assert.Empty(f.Client.Packets);
        Assert.Empty(f.Store.Saves);
    }

    [Fact]
    public async Task ThePreciseDelayOfOtherBoxesIsStillSaved()
    {
        var f = new MovementOracleSession("wf_act_toggle_state");

        await SaveEffect(f, ActionSave([0], delay: -500, userSources: []));

        AssertSaved(f, true, "");
    }

    [Fact]
    public async Task TheSameFurniPickedTwiceInHighlightIsSavedOnce()
    {
        var f = new MovementOracleSession(Highlight);

        await SaveEffect(f, ActionSave(ValidInts(Highlight), picks: [8, 8]));

        AssertSaved(f, true, "");
        Assert.Equal([8u], Assert.Single(f.Store.Saves).SelectedItems.ToArray());
    }

    // Observed on AIR WIN63-202610201032-107964540: Set Furni Visibility saves a furni picked twice once, as it does on 48 cards in all.
    [Fact]
    public async Task TheSameFurniPickedTwiceInVisibilityIsSavedOnce()
    {
        var f = new MovementOracleSession(Visibility);

        await SaveEffect(f, ActionSave(ValidInts(Visibility), picks: [8, 8]));

        AssertSaved(f, true, "");
        Assert.Equal([8u], Assert.Single(f.Store.Saves).SelectedItems.ToArray());
    }
}
