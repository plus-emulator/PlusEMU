using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

/// <summary>Runtime contracts of the October boxes that do not need a live room.</summary>
public sealed class WiredOctoberRuntimeTests
{
    [Fact]
    public void TransparencyPacketsFollowTheClientParsers()
    {
        // [n, ids], transparency, click through, easing.
        var furni = new HabbiconTestSupport.RecordingPacket();
        new WiredFurniTransparencyComposer([4, 9], 100, true, 3).Compose(furni);
        Assert.Equal(new object[] { 2, 4, 9, 100, true, 3 }, furni.Writes);

        var users = new HabbiconTestSupport.RecordingPacket();
        new WiredUserTransparencyComposer([12], 0, false, 0).Compose(users);
        Assert.Equal(new object[] { 1, 12, 0, false, 0 }, users.Writes);

        Assert.Equal(3737u, new WiredFurniTransparencyComposer([], 0, false, 0).MessageId);
        Assert.Equal(3073u, new WiredUserTransparencyComposer([], 0, false, 0).MessageId);
    }

    [Fact]
    public void HighlightPacketFollowsTheClientParser()
    {
        // [n, ids], type, strength, colour, animation, easing.
        var packet = new HabbiconTestSupport.RecordingPacket();
        new WiredFurniHighlightComposer([7], 3, 75, 0xFF00FF, 1, 0).Compose(packet);
        Assert.Equal(new object[] { 1, 7, 3, 75, 0xFF00FF, 1, 0 }, packet.Writes);
        Assert.Equal(ServerPacketHeader.WiredFurniHighlightComposer, new WiredFurniHighlightComposer([], 0, 0, 0, 0, 0).MessageId);
    }

    [Fact]
    public void UserHighlightAndEntryStatePacketsFollowTheClientParsers()
    {
        // [n, room indices], type, strength, colour, animation, easing.
        var user = new HabbiconTestSupport.RecordingPacket();
        new WiredUserHighlightComposer([3, 4], 1, 100, 0x00FF00, 0, 4).Compose(user);
        Assert.Equal(new object[] { 2, 3, 4, 1, 100, 0x00FF00, 0, 4 }, user.Writes);

        // Transparencies then highlights.
        var state = new HabbiconTestSupport.RecordingPacket();
        new WiredVisualizationStateComposer(false, [new(9, 100, false, 1)], [new(9, 2, 30, 0, 1, 0)]).Compose(state);
        Assert.Equal(new object[] { 1, 9, 100, false, 1, 1, 9, 2, 30, 0, 1, 0 }, state.Writes);

        Assert.Equal((1899u, 1477u, 3360u), (new WiredUserHighlightComposer([], 0, 0, 0, 0, 0).MessageId,
            new WiredVisualizationStateComposer(false, [], []).MessageId, new WiredVisualizationStateComposer(true, [], []).MessageId));
    }

    [Fact]
    public void EnvironmentPacketListsTheEnabledAchievements()
    {
        // [click user wired, n, achievement x n], nothing after.
        var packet = new HabbiconTestSupport.RecordingPacket();
        new WiredEnvironmentComposer(true, ["ACH_RoomEntry", "ACH_Login"]).Compose(packet);
        Assert.Equal(new object[] { true, 2, "ACH_RoomEntry", "ACH_Login" }, packet.Writes);
    }

    [Theory]
    [InlineData("wf_trg_room_initializes", WiredEventKind.RoomInitialized)]
    [InlineData("wf_trg_receive_message", WiredEventKind.LinkMessage)]
    [InlineData("wf_trg_receive_dataframe", WiredEventKind.LinkDataframe)]
    public void OctoberTriggersListenToTheirOwnEvents(string name, WiredEventKind kind)
    {
        Assert.Equal(kind, WiredTriggerConfiguration.Events[name]);
        Assert.Empty(WiredTriggerConfiguration.Defaults(name).IntParams);
    }

    [Fact]
    public void UserLevelConditionHasALegacyDefault()
    {
        var defaults = WiredConditionConfiguration.Defaults("wf_cnd_user_level");

        Assert.Equal(new[] { 1, 1, 0 }, defaults.IntParams);
        Assert.False(WiredConditionConfiguration.TryValidate("wf_cnd_user_level", new() { IntParams = [31, 1, 0] }, out _, out _));
    }
}
