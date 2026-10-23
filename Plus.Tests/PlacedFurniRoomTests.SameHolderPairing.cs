using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540 (Change Variable Value: 270 firings in three runs; Give Variable: 150 firings in three runs): with
// same holder off and a reference read from several furni holders, Habbo gives each furni destination the value of one reference holder, at
// random and anew at every firing. The same sets fired six times gave six different pairings, and a set of three holders used as both sides
// (30 firings) gave all six pairings about evenly (6, 6, 6, 5, 4 and 3 times; over 90 firings identity, swap and 3-cycle came 19, 48 and 23 times).
// With as many reference holders as destinations every reference value is used once; with more reference holders only some of them are used,
// each once; with fewer, each reference value is used the same number of times, give or take one. With one reference holder every destination gets
// its value. Users were not probed (one account).
public partial class PlacedFurniRoomTests
{
    private const int PairingFirings = 40;

    // Destination holders (A, B, C by index), reference holders and the reference list (first list or second), and what each firing must give.
    public static TheoryData<string, int[], int[], bool, string> PairingRows()
    {
        var data = new TheoryData<string, int[], int[], bool, string>();

        foreach (var card in new[] { Change, Give }) {
            data.Add(card, [0, 1, 2], [0, 1, 2], false, "all values once");
            data.Add(card, [0, 1], [0, 1, 2], true, "different values");
            data.Add(card, [0, 1, 2], [0, 1], true, "values shared evenly");
            data.Add(card, [0, 1], [2], true, "the one value");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PairingRows))]
    public void SameHolderOffPairsEachDestinationWithAReferenceHolderAtRandom(string card, int[] destinations, int[] references, bool secondList, string rule)
    {
        var (actor, service, module, holders) = PairingRoom();
        SavePairingBox(service, card, destinations, references, secondList);
        long[] referenceValues = [3, 4, 5];
        var outcomes = new HashSet<string>();
        var frame = new WiredVariableFrame(RoomId, holders);

        for (var firing = 0; firing < PairingFirings; firing++) {
            foreach (var holder in holders[..3]) {
                Assert.True(module.Mutate(new(holder.Target, "custom:863"), holder, WiredVariableMutation.Replace, 100, frame));
            }

            actor.OnChat(0, "go", false);
            _room.GetWired().OnFastCycle();
            _room.GetWired().OnCycle();
            var given = destinations.Select(index => module.Read(new(holders[index].Target, "custom:863"), holders[index], frame)!.Value).ToArray();
            var allowed = references.Select(index => referenceValues[index]).ToArray();
            Assert.All(given, value => Assert.Contains(value, allowed));

            switch (rule) {
                case "all values once":
                    Assert.Equal(allowed.Order(), given.Order());
                    break;
                case "different values":
                    Assert.Equal(given.Length, given.Distinct().Count());
                    break;
                case "values shared evenly":
                    var counts = given.GroupBy(value => value).Select(group => group.Count()).Order().ToArray();
                    Assert.Equal([1, 2], counts);
                    break;
                default:
                    Assert.All(given, value => Assert.Equal(allowed[0], value));
                    break;
            }

            outcomes.Add(string.Join(',', given));
        }

        // The pairing is drawn anew at every firing: with one reference holder there is one outcome, otherwise many. With three destinations and
        // two reference holders the one destination that has its value alone is drawn too (six outcomes), not only the values.
        var minimum = rule == "the one value" ? 1 : rule == "values shared evenly" ? 3 : 2;
        Assert.True(rule == "the one value" ? outcomes.Count == 1 : outcomes.Count >= minimum, $"{outcomes.Count} different outcomes in {PairingFirings} firings");
    }

    // Furni A, B and C hold the reference variable 982 as 3, 4 and 5 and the destination variable 863 as 100.
    private (RoomUser Actor, WiredConfigurationService Service, WiredVariableModule Module, WiredVariableHolder[] Holders) PairingRoom()
    {
        var actor = EditedVariableRoom();
        var service = new WiredConfigurationService(new EmptyConfigurationStore(), null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());
        PlaceVariableBox(SameFurniReference, "wf_var_furni");
        Assert.Equal("saved", SaveVariable(service, SameFurniReference, [1, 1], "ec_f2"));
        AddSpeechBox(SameSpeech, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0, 1, 1);
        var module = _room.GetWired().Variables.Module;
        var tiles = new[] { HeldFurniA, HeldFurniB, HeldFurniC }.Select(id => Assert.IsType<Plus.HabboHotel.Items.Item>(_room.GetRoomItemHandler().GetItem(id))).ToArray();
        WiredVariableHolder[] holders = [.. tiles.Select(WiredVariableRuntimeFrames.FurniHolder), WiredVariableRuntimeFrames.UserHolder(actor)];
        var frame = new WiredVariableFrame(RoomId, holders);

        foreach (var (holder, reference) in new[] { (holders[0], 3L), (holders[1], 4L), (holders[2], 5L) }) {
            Assert.True(module.Mutate(new(holder.Target, "custom:982"), holder, WiredVariableMutation.Replace, reference, frame));
            Assert.True(module.Mutate(new(holder.Target, "custom:863"), holder, WiredVariableMutation.Replace, 100, frame));
        }

        return (actor, service, module, holders);
    }

    // The raw action save with the destination furni in the first list and the reference furni in the first list (source 100) or the second (101).
    private void SavePairingBox(WiredConfigurationService service, string card, int[] destinations, int[] references, bool secondList)
    {
        PlaceSavedBox(SameBox, card, 1, 1, 1);
        uint[] wire = [HeldFurniA, HeldFurniB, HeldFurniC];
        object[] ints = card == Give ? [0, 0, 0, 1, 1, 0, 0] : [0, 0, 1, 0, 0, 0, 0];
        object[] body = [(int)SameBox, 7, .. ints, "", destinations.Length, .. destinations.Select(index => (object)(int)wire[index]), 0, 2, 100, secondList ? 101 : 100, 2, 0, 0, 2,
            FurniDestination, FurniReference, secondList ? references.Length : 0, .. secondList ? references.Select(index => (object)(int)wire[index]) : []];
        _client.Packets.Clear();
        new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket(body)).GetAwaiter().GetResult();
        Assert.Equal("saved", SaveAnswer());
    }
}
