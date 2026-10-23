using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// "same holder" saves on Change Variable Value, Give Variable and Variable Value Match for every pair of destination and
// reference kind (furni, user, global) with a variable, with a value or an empty slot when both are global, and with an empty
// reference slot on a furni destination. Each save reopens with its ints unchanged. Read on a holder of another kind (a global
// destination with a furni or user reference, a furni destination with a global reference), the reference gives nothing: Change
// Variable Value then changes nothing.
public sealed partial class WiredGlideConveyorTests
{
    private const string ChangeBox = "wf_act_change_var_val", GiveBox = "wf_act_give_var", MatchBox = "wf_cnd_var_val_match";

    // One custom variable of each kind, and a second global one for the reference.
    private static readonly Dictionary<int, string> DestinationVariables = new() { [0] = "furni:301", [1] = "user:302", [-10] = "room:303" };
    private static readonly Dictionary<int, string> ReferenceVariables = new() { [0] = "furni:301", [1] = "user:302", [-10] = "room:304" };

    private sealed class CustomVariables(uint room) : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 1;

        public WiredVariableDefinition? Find(uint id) => id switch
        {
            301 => new(id, room, 1, "furni", WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, true),
            302 => new(id, room, 1, "user", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            303 or 304 => new(id, room, 1, "global" + id, WiredVariableTarget.Global, WiredVariableAvailability.RoomActive, true),
            _ => null
        };
    }

    public static TheoryData<string, int, int> SameHolderKindPairs()
    {
        var data = new TheoryData<string, int, int>();

        foreach (var box in new[] { ChangeBox, GiveBox, MatchBox }) {
            foreach (var destination in new[] { 0, 1, -10 }) {
                foreach (var reference in new[] { 0, 1, -10 }) {
                    data.Add(box, destination, reference);
                }
            }
        }

        return data;
    }

    // Change: target, operator, option, value high, value low, reference target, same holder. Give: target, value high, value low,
    // override, option, reference target, same holder. Match: target, comparison, option, value high, value low, reference target, same holder.
    private static int[] SameHolderInts(string box, int destination, int reference, int option = 1, int value = 0) => box switch
    {
        ChangeBox => [destination, 0, option, 0, value, reference, 1],
        GiveBox => [destination, 0, value, 0, option, reference, 1],
        _ => [destination, 1, option, 0, value, reference, 1]
    };

    private static (Fixture Fixture, RoomUser Owner, List<(uint Header, byte[] Payload)> Frames) VariableBoxFixture(string box)
    {
        if (box == MatchBox) {
            var (f, owner, frames) = MatchFixture(variables: room => new CustomVariables(room));
            SaveToggle(f, owner, frames, [0]);
            Assert.Equal("ok", SaveAnswer(frames));

            return (f, owner, frames);
        }

        return MatchFixture(variableBox: box, variables: room => new CustomVariables(room));
    }

    // The client sends an action save as: item, ints, text, picks, delay, furni sources, user sources, variables, secondary picks.
    private static void SaveVariableBox(Fixture f, RoomUser owner, List<(uint Header, byte[] Payload)> frames, string box, int[] ints, string[] variables)
    {
        if (box == MatchBox) {
            SaveMatch(f, owner, frames, ints, variables);

            return;
        }

        object[] body = [(int)MatchCondition, ints.Length, .. ints.Cast<object>(), "", 1, (int)MatchState, 0, 2, 100, 200, 2, 0, 200,
            variables.Length, .. variables.Cast<object>(), 0];
        Send(f, owner, frames, new SaveWiredEffectConfigEvent(MatchService()), body);
    }

    private static uint VariableBoxHeader(string box) =>
        box == MatchBox ? ServerPacketHeader.WiredConditionConfigComposer : ServerPacketHeader.WiredEffectConfigComposer;

    [Theory]
    [MemberData(nameof(SameHolderKindPairs))]
    public void SameHolderSavesForEveryPairOfDestinationAndReferenceKind(string box, int destination, int reference)
    {
        var (f, owner, frames) = VariableBoxFixture(box);
        var ints = SameHolderInts(box, destination, reference);
        string[] variables = [DestinationVariables[destination], ReferenceVariables[reference]];

        SaveVariableBox(f, owner, frames, box, ints, variables);

        Assert.True(SaveAnswer(frames) == "ok", $"{box} {destination}/{reference}: {SaveAnswer(frames)}");
        var reopened = Reopen(f, owner, frames, MatchCondition, VariableBoxHeader(box));
        Assert.Equal(ints, reopened.Ints);
        Assert.Equal(variables, reopened.Variables);
    }

    // Empty slots with same holder on, on a global destination: the save is stored and the box then changes nothing.
    [Theory]
    [InlineData(ChangeBox, "room:303,n")]
    [InlineData(ChangeBox, "n,room:304")]
    [InlineData(GiveBox, "room:303,n")]
    [InlineData(GiveBox, "n,room:304")]
    public void SameHolderSavesWithAnEmptySlotOnAGlobalDestination(string box, string variables)
    {
        var (f, owner, frames) = VariableBoxFixture(box);
        var ints = SameHolderInts(box, -10, -10);

        SaveVariableBox(f, owner, frames, box, ints, variables.Split(','));

        Assert.True(SaveAnswer(frames) == "ok", $"{box} {variables}: {SaveAnswer(frames)}");
        var reopened = Reopen(f, owner, frames, MatchCondition, VariableBoxHeader(box));
        Assert.Equal(ints, reopened.Ints);
        Assert.Equal(variables.Split(',').Select(token => token == "n" ? "0" : token), reopened.Variables);
    }

    // An empty reference slot with same holder on saves on a furni destination too (the box then changes nothing), with its ints
    // as sent.
    [Theory]
    [InlineData(ChangeBox)]
    [InlineData(GiveBox)]
    public void SameHolderSavesWithAnEmptyReferenceOnAFurniDestination(string box)
    {
        var (f, owner, frames) = VariableBoxFixture(box);
        var ints = SameHolderInts(box, 0, 0);

        SaveVariableBox(f, owner, frames, box, ints, ["furni:301", "n"]);

        Assert.True(SaveAnswer(frames) == "ok", $"{box}: {SaveAnswer(frames)}");
        var reopened = Reopen(f, owner, frames, MatchCondition, VariableBoxHeader(box));
        Assert.Equal(ints, reopened.Ints);
        Assert.Equal(["furni:301", "0"], reopened.Variables);
    }

    // Same holder on with a value instead of a variable, on a global destination.
    [Theory]
    [InlineData(ChangeBox)]
    [InlineData(GiveBox)]
    public void SameHolderSavesWithAValueInsteadOfAVariableOnAGlobalDestination(string box)
    {
        var (f, owner, frames) = VariableBoxFixture(box);
        var ints = SameHolderInts(box, -10, -10, option: 0, value: 9);

        SaveVariableBox(f, owner, frames, box, ints, ["room:303", "n"]);

        Assert.True(SaveAnswer(frames) == "ok", $"{box}: {SaveAnswer(frames)}");
        var reopened = Reopen(f, owner, frames, MatchCondition, VariableBoxHeader(box));
        Assert.Equal(ints, reopened.Ints);
        Assert.Equal(["room:303", "0"], reopened.Variables);
    }

    // A furni state set from a global variable takes its value; with same holder on it reads the global on the furni, finds
    // nothing and the state stays. This test uses the built-in @furni_count.
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void ChangeVariableValueReadingAGlobalSetsTheStateOnlyWithoutSameHolder(int sameHolder, bool changes)
    {
        var (f, owner, frames) = MatchFixture(variableBox: ChangeBox);
        int[] ints = [0, 0, 1, 0, 0, -10, sameHolder];

        SaveVariableBox(f, owner, frames, ChangeBox, ints, ["-110", "-300"]);

        Assert.Equal("ok", SaveAnswer(frames));
        Fire(f, owner);
        Assert.Equal(changes, f.Items[MatchState].LegacyDataString != "0");
    }
}
