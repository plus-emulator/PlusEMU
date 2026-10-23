using System.Reflection;
using System.Text.Json;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// A says-something trigger, a Variable Value Match and a Toggle State on a colour tile. A Value Match saved with no variable, or
// with option 1 and no reference variable, is accepted, reopens with an empty slot as "0" and blocks when fired. A Toggle State
// saved with no ints reopens with ints [0] and toggles its pick.
public sealed partial class WiredGlideConveyorTests
{
    private const uint MatchTarget = 71, MatchState = 72, MatchSay = 73, MatchCondition = 74, MatchToggle = 75;
    private const int FurniState = -110;

    private static (Fixture Fixture, RoomUser Owner, List<(uint Header, byte[] Payload)> Frames) MatchFixture(bool condition = true,
        string variableBox = "wf_cnd_var_val_match", Func<uint, IWiredVariableDirectory>? variables = null)
    {
        var f = new Fixture(
        [
            (MatchTarget, 5, 12, 0.0, 0, "wf_colortile", null),
            (MatchState, 6, 12, 0.0, 0, "wf_colortile", null),
            (MatchSay, 4, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "tg" }))
        ], live: true);
        f.Room.OwnerId = 1;
        f.Room.OwnerName = "user1";
        f.Room.Type = "private";
        // The room's real built-in variables, so @state reads the tile.
        var module = new WiredVariableModule(f.Room.Id, variables?.Invoke(f.Room.Id) ?? new NoUserVariables(), new MemoryWiredVariableStore(), TimeProvider.System,
            new RoomWiredBuiltinVariables(f.Room));
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(f.Room.GetWired().Variables, module);

        if (condition) {
            f.PlaceWired(MatchCondition, 4, 9, 0.65, variableBox);
        }

        f.PlaceWired(MatchToggle, 4, 9, 1.3, "wf_act_toggle_state");

        // The open handler reaches the editor through the box's interactor.
        foreach (var box in f.Items.Values.Where(item => item.Id is MatchCondition or MatchToggle)) {
            box.Attach(f.Room, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);
        }

        f.Items[MatchTarget].ExtraData = new LegacyDataFormat { Data = "0" };
        f.Items[MatchState].ExtraData = new LegacyDataFormat { Data = "0" };
        var owner = f.Walker(1, 6, 10, 6, 11);

        return (f, owner, CaptureFrames(owner));
    }

    private static void Send(Fixture f, RoomUser owner, List<(uint Header, byte[] Payload)> frames, IPacketEvent save, object[] body)
    {
        frames.Clear();
        f.Owned(() => save.Parse(owner.GetClient()!, EditorTestSupport.Incoming(body)).GetAwaiter().GetResult());
    }

    private static WiredConfigurationService MatchService() =>
        new(new SavedConfigurations([]), null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());

    // The client sends a condition save as: item, ints, text, picks, quantifier, furni sources, user sources, variables, secondary picks.
    private static void SaveMatch(Fixture f, RoomUser owner, List<(uint Header, byte[] Payload)> frames, int[] ints, string[] variables)
    {
        object[] body = [(int)MatchCondition, ints.Length, .. ints.Cast<object>(), "", 1, (int)MatchState, 0, 2, 100, 200, 2, 0, 200,
            variables.Length, .. variables.Cast<object>(), 0];
        Send(f, owner, frames, new SaveWiredConditionConfigEvent(MatchService()), body);
    }

    private static void SaveToggle(Fixture f, RoomUser owner, List<(uint Header, byte[] Payload)> frames, int[] ints)
    {
        object[] body = [(int)MatchToggle, ints.Length, .. ints.Cast<object>(), "", 1, (int)MatchTarget, 0, 1, 100, 0, 0, 0];
        Send(f, owner, frames, new SaveWiredEffectConfigEvent(MatchService()), body);
    }

    private static string SaveAnswer(List<(uint Header, byte[] Payload)> frames) =>
        SaveRefusal(frames) is { } key ? $"refused: {key}"
        : frames.Any(frame => frame.Header == ServerPacketHeader.HideWiredConfigComposer) ? "ok" : "no answer";

    // The raw open packet, then the start of the editor frame: limit, picks, secondary picks, sprite, id, text, ints, variable ids.
    private static (int[] Ints, string[] Variables) Reopen(Fixture f, RoomUser owner, List<(uint Header, byte[] Payload)> frames, uint box, uint header)
    {
        frames.Clear();
        f.Owned(() => new OpenWiredEvent().Parse(f.Room, owner.GetClient()!, EditorTestSupport.Incoming((int)box)).GetAwaiter().GetResult());
        var frame = new FlashIncomingPacket { Buffer = Assert.Single(frames, frame => frame.Header == header).Payload };
        frame.ReadInt();

        for (var list = 0; list < 2; list++) {
            for (var count = frame.ReadInt(); count > 0; count--) {
                frame.ReadInt();
            }
        }

        frame.ReadInt();
        Assert.Equal(box, frame.ReadUInt());
        frame.ReadString();
        var ints = new int[frame.ReadInt()];

        for (var index = 0; index < ints.Length; index++) {
            ints[index] = frame.ReadInt();
        }

        var variables = new string[frame.ReadInt()];

        for (var index = 0; index < variables.Length; index++) {
            variables[index] = frame.ReadString();
        }

        return (ints, variables);
    }

    private static string Fire(Fixture f, RoomUser owner)
    {
        f.Owned(() => owner.OnChat(0, "tg", false));
        f.Advance(1000);

        return f.Items[MatchTarget].LegacyDataString;
    }

    private static (Fixture Fixture, RoomUser Owner, List<(uint Header, byte[] Payload)> Frames) MatchStackWithToggle()
    {
        var (f, owner, frames) = MatchFixture();
        SaveToggle(f, owner, frames, [0]);
        Assert.Equal("ok", SaveAnswer(frames));

        return (f, owner, frames);
    }

    // Control: @state of the picked tile is 0, so "= 0" passes and "= 5" blocks.
    [Theory]
    [InlineData(0, "1")]
    [InlineData(5, "0")]
    public void AValueMatchOnTheTileStateRunsTheToggleOnlyWhenItMatches(int value, string target)
    {
        var (f, owner, frames) = MatchStackWithToggle();

        SaveMatch(f, owner, frames, [0, 1, 0, 0, value, 0, 0], [FurniState.ToString(), "n"]);

        Assert.Equal("ok", SaveAnswer(frames));
        Assert.Equal(target, Fire(f, owner));
    }

    [Theory]
    [InlineData(new[] { 0, 1, 0, 0, 5, 0, 0 }, "n,n")]
    [InlineData(new[] { 0, 4, 0, 0, 5, 0, 0 }, "n,n")]
    [InlineData(new[] { 0, 1, 0, 0, 0, 0, 0 }, "n,n")]
    [InlineData(new[] { 0, 1, 0, 0, 5, 0, 0 }, "")]
    [InlineData(new[] { 0, 4, 0, 0, 5, 0, 0 }, "")]
    [InlineData(new[] { 0, 1, 0, 0, 0, 0, 0 }, "")]
    public void AValueMatchSavedWithNoVariableIsStoredWithEmptySlotsAndBlocks(int[] ints, string variables)
    {
        var (f, owner, frames) = MatchStackWithToggle();

        SaveMatch(f, owner, frames, ints, variables.Length == 0 ? [] : variables.Split(','));

        Assert.Equal("ok", SaveAnswer(frames));
        var reopened = Reopen(f, owner, frames, MatchCondition, ServerPacketHeader.WiredConditionConfigComposer);
        Assert.Equal(ints, reopened.Ints);
        Assert.Equal(["0", "0"], reopened.Variables);
        Assert.Equal("0", Fire(f, owner));
    }

    // Option 1 compares the left variable with a reference variable; with no reference every comparison blocks.
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(2)]
    [InlineData(0)]
    public void AValueMatchAgainstAReferenceSavedWithNoReferenceIsStoredWithAnEmptySlotAndBlocks(int comparison)
    {
        var (f, owner, frames) = MatchStackWithToggle();
        int[] ints = [0, comparison, 1, 0, 0, 0, 0];

        SaveMatch(f, owner, frames, ints, [FurniState.ToString(), "n"]);

        Assert.Equal("ok", SaveAnswer(frames));
        var reopened = Reopen(f, owner, frames, MatchCondition, ServerPacketHeader.WiredConditionConfigComposer);
        Assert.Equal(ints, reopened.Ints);
        Assert.Equal([FurniState.ToString(), "0"], reopened.Variables);
        Assert.Equal("0", Fire(f, owner));
    }

    [Fact]
    public void AToggleStateSavedWithNoIntsReopensWithIntsZeroAndTogglesItsPick()
    {
        var (f, owner, frames) = MatchFixture(condition: false);

        SaveToggle(f, owner, frames, []);

        Assert.Equal("ok", SaveAnswer(frames));
        var reopened = Reopen(f, owner, frames, MatchToggle, ServerPacketHeader.WiredEffectConfigComposer);
        Assert.Equal([0], reopened.Ints);
        Assert.Equal("1", Fire(f, owner));
        Assert.Equal("2", Fire(f, owner));
    }

    private sealed class NoUserVariables : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 1;
        public WiredVariableDefinition? Find(uint id) => null;
    }
}
