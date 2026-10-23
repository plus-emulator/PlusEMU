using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Change Variable Value and Give Variable save with an empty variable slot (a "0" or "n" destination or reference, with the variable option on, same holder on or off)
// on a furni or a user destination, and then change nothing. With same holder on, a furni or user destination takes a value, a reference of its own kind from its own holder
// (each tile its own value), and nothing from a reference of the other kind.
// With same holder off and a reference read from several holders, Habbo pairs destination and reference holders by their position in a pick set.
// That pairing is not stable, so those rows are left out.
public partial class PlacedFurniRoomTests
{
    private const uint SameFurniReference = 982, SameUserReference = 983, SameBox = 984, SameSpeech = 985;
    private const string FurniDestination = "furni:863", FurniReference = "furni:982", UserDestination = "user:861", UserReference = "user:983";

    // One row per shape, with the values each holder ended on: the two furni for a furni destination, the speaker for a user one.
    public static TheoryData<string, int, string, int, bool, long?, long?> SameHolderRows()
    {
        var data = new TheoryData<string, int, string, int, bool, long?, long?>();

        foreach (var card in new[] { Change, Give }) {
            foreach (var present in card == Change ? new[] { true } : new[] { true, false }) {
                long? Start(long value) => present ? value : null;

                // A furni destination (target 0): holders t1 and t2.
                foreach (var same in new[] { 0, 1 }) {
                    data.Add(card, 0, "value", same, present, 9, 9);
                    data.Add(card, 0, "empty", same, present, Start(100), Start(100));
                }

                data.Add(card, 0, "own kind", 1, present, 3, 5);
                data.Add(card, 0, "other kind", 1, present, Start(100), Start(100));
                data.Add(card, 0, "other kind", 0, present, 4, 4);

                // A user destination (target 1): the speaker.
                foreach (var same in new[] { 0, 1 }) {
                    data.Add(card, 1, "value", same, present, 9, null);
                    data.Add(card, 1, "own kind", same, present, 4, null);
                    data.Add(card, 1, "empty", same, present, Start(100), null);
                }

                data.Add(card, 1, "other kind", 1, present, Start(100), null);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SameHolderRows))]
    public void SameHolderReadsEachDestinationHolderItself(string card, int target, string shape, int sameHolder, bool present, long? first, long? second)
    {
        var (actor, service, module, holders) = SameHolderRoom(present);
        SaveSameHolderBox(service, card, target, shape, sameHolder);

        actor.OnChat(0, "go", false);
        _room.GetWired().OnFastCycle();
        _room.GetWired().OnCycle();

        var frame = new WiredVariableFrame(RoomId, holders);

        if (target == 0) {
            Assert.Equal([first, second], [ReadSame(module, frame, holders[0], 863), ReadSame(module, frame, holders[1], 863)]);
        }
        else {
            Assert.Equal(first, ReadSame(module, frame, holders[2], 861));
        }
    }

    private static long? ReadSame(WiredVariableModule module, WiredVariableFrame frame, WiredVariableHolder holder, uint variable) =>
        module.Read(new(holder.Target, "custom:" + variable), holder, frame)?.Value;

    private (RoomUser Actor, WiredConfigurationService Service, WiredVariableModule Module, WiredVariableHolder[] Holders) SameHolderRoom(bool present)
    {
        var actor = EditedVariableRoom();
        var service = new WiredConfigurationService(new EmptyConfigurationStore(), null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());
        PlaceVariableBox(SameFurniReference, "wf_var_furni");
        Assert.Equal("saved", SaveVariable(service, SameFurniReference, [1, 1], "ec_f2"));
        PlaceVariableBox(SameUserReference, "wf_var_user");
        Assert.Equal("saved", SaveVariable(service, SameUserReference, [0, 1], "ec_u2"));
        AddSpeechBox(SameSpeech, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0, 1, 1);

        var module = _room.GetWired().Variables.Module;
        var tiles = new[] { Assert.IsType<Plus.HabboHotel.Items.Item>(_room.GetRoomItemHandler().GetItem(HeldFurniA)),
            Assert.IsType<Plus.HabboHotel.Items.Item>(_room.GetRoomItemHandler().GetItem(HeldFurniC)) };
        WiredVariableHolder[] holders = [.. tiles.Select(WiredVariableRuntimeFrames.FurniHolder), WiredVariableRuntimeFrames.UserHolder(actor)];
        var frame = new WiredVariableFrame(RoomId, holders);

        foreach (var (holder, reference) in new[] { (holders[0], 3L), (holders[1], 5L) }) {
            Assert.True(module.Mutate(new(holder.Target, "custom:982"), holder, WiredVariableMutation.Replace, reference, frame));

            if (present) {
                Assert.True(module.Mutate(new(holder.Target, "custom:863"), holder, WiredVariableMutation.Replace, 100, frame));
            }
        }

        Assert.True(module.Mutate(new(holders[2].Target, "custom:983"), holders[2], WiredVariableMutation.Replace, 4, frame));

        if (present) {
            Assert.True(module.Mutate(new(holders[2].Target, "custom:861"), holders[2], WiredVariableMutation.Replace, 100, frame));
        }

        return (actor, service, module, holders);
    }

    // The raw action save: Give [target, value high, value low, override, option, reference target, same holder], Change [target, operator, option,
    // value high, value low, reference target, same holder]; both picks, the picked furni and the triggering user as sources of both selectors.
    private void SaveSameHolderBox(WiredConfigurationService service, string card, int target, string shape, int sameHolder)
    {
        PlaceSavedBox(SameBox, card, 1, 1, 1);
        var furni = target == 0;
        var (destination, own, other) = furni ? (FurniDestination, FurniReference, UserReference) : (UserDestination, UserReference, FurniReference);
        // The ints: a value is 9 with reference target -10, a reference shape has value 0 and the target of its variable (-10 when empty).
        var (reference, option, referenceTarget) = shape switch
        {
            "value" => ("0", 0, -10),
            "own kind" => (own, 1, target),
            "other kind" => (other, 1, 1 - target),
            _ => ("0", 1, -10)
        };
        var value = shape == "value" ? 9 : 0;
        object[] ints = card == Give ? [target, 0, value, 1, option, referenceTarget, sameHolder] : [target, 0, option, 0, value, referenceTarget, sameHolder];
        object[] body = [(int)SameBox, 7, .. ints, "", 2, (int)HeldFurniA, (int)HeldFurniC, 0, 2, 100, 100, 2, 0, 0, 2, destination, reference, 0];
        _client.Packets.Clear();
        new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket(body)).GetAwaiter().GetResult();
        Assert.Equal("saved", SaveAnswer());
    }

    // Both cards save with the destination, the reference or both naming no variable ("0", "n", or a number no variable has),
    // with the value or the variable option and same holder on or off, and reopen with their ints as sent.
    [Theory]
    [InlineData("0", "0", "0", "0")]
    [InlineData("0", "5", "0", "5")]
    [InlineData("5", "0", "5", "0")]
    [InlineData("0", "furni:863", "0", "furni:863")]
    [InlineData("furni:863", "0", "furni:863", "0")]
    [InlineData("n", "n", "0", "0")]
    public void AnEmptyOrUnresolvedSlotSavesWithEveryOption(string destination, string reference, string reopenedDestination, string reopenedReference)
    {
        foreach (var card in new[] { Change, Give }) {
            foreach (var option in new[] { 0, 1 }) {
                foreach (var sameHolder in new[] { 0, 1 }) {
                    var service = UseVariableRows();
                    _variableBoxes.Clear();
                    var box = (uint)(SameBox + 10 * (uint)(option * 2 + sameHolder) + (card == Give ? 100u : 0));
                    PlaceVariableBox(box, card);
                    object[] ints = card == Give ? [0, 0, 1, 1, option, 0, sameHolder] : [0, 0, option, 0, 1, 0, sameHolder];
                    object[] body = [(int)box, 7, .. ints, "", 0, 0, 2, 100, 200, 2, 0, 200, 2, destination, reference, 0];
                    _client.Packets.Clear();
                    new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket(body)).GetAwaiter().GetResult();

                    Assert.Equal("saved", SaveAnswer());
                    var (reopenedInts, reopenedVariables) = ReopenSlots(box);
                    Assert.Equal(ints.Cast<int>(), reopenedInts);
                    Assert.Equal([reopenedDestination, reopenedReference], reopenedVariables);
                }
            }
        }
    }

    private (int[] Ints, string[] Variables) ReopenSlots(uint id)
    {
        _client.Packets.Clear();
        new OpenWiredEvent().Parse(_room, _client, ClientPacket((int)id)).GetAwaiter().GetResult();
        var frame = new Plus.Communication.Flash.FlashIncomingPacket
        {
            Buffer = Assert.Single(_client.Packets, packet => packet.Header == Plus.Communication.Packets.Outgoing.ServerPacketHeader.WiredEffectConfigComposer).Body
        };

        frame.ReadInt();
        SkipInts(frame);
        SkipInts(frame);
        frame.ReadInt();
        frame.ReadUInt();
        frame.ReadString();
        int[] ints = [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadInt())];

        return (ints, [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadString())]);
    }
}
