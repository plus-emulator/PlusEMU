using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Give Variable, Change Variable Value and Remove Variable save on any variable id the editor sends, whatever its kind. An id of a variable the room does not have, or of another kind than the target, saves too. A number is kept as a number; any other text is stored as "0", the id of no variable.
// The reference slot of Give and Change is stored the same way. A save with the "from a variable" option on and no variable to read is not covered.
public partial class PlacedFurniRoomTests
{
    private const uint VariableIdTile = 951, VariableIdBox = 952;
    private uint _variableIdBox;
    private const string Give = "wf_act_give_var", Change = "wf_act_change_var_val", Remove = "wf_act_remove_var";

    // Every variable of the room catalog, with its target (0 furni, 1 user, -10 global, -20 context).
    private static readonly (int Target, string Id)[] HabboBuiltIns =
    [
        (0, "-100"), (0, "-101"), (0, "-102"), (0, "-110"), (0, "-120"), (0, "-121"), (0, "-122"), (0, "-123"), (0, "-124"), (0, "-125"), (0, "-126"),
        (0, "-129"), (0, "-130"), (0, "-131"), (0, "-132"), (0, "-133"), (0, "-140"), (0, "-141"), (0, "-180"), (0, "-190"), (1, "-200"), (1, "-205"),
        (1, "-206"), (1, "-210"), (1, "-211"), (1, "-220"), (1, "-221"), (1, "-222"), (1, "-223"), (1, "-230"), (1, "-231"), (1, "-232"), (1, "-233"),
        (1, "-234"), (1, "-237"), (1, "-238"), (1, "-239"), (1, "-240"), (1, "-241"), (1, "-242"), (1, "-243"), (1, "-244"), (1, "-245"), (1, "-250"),
        (1, "-260"), (1, "-261"), (1, "-262"), (1, "-266"), (1, "-267"), (1, "-270"), (1, "-271"), (1, "-272"), (1, "-280"), (1, "-281"), (1, "-282"),
        (1, "-283"), (1, "-284"), (1, "-285"), (1, "-286"), (-10, "-300"), (-10, "-301"), (-10, "-310"), (-10, "-320"), (-10, "-321"), (-10, "-322"),
        (-10, "-323"), (-10, "-330"), (-10, "-331"), (-10, "-332"), (-10, "-333"), (-10, "-380"), (-10, "-381"), (-20, "-400"), (-20, "-401"),
        (-20, "-410"), (-20, "-411"), (-20, "-420"), (-20, "-450"), (-20, "-451"), (-20, "-452"), (-20, "-453"), (-20, "-459"), (-20, "-460"),
        (-20, "-461"), (-20, "-462"), (-20, "-463"), (-20, "-464"), (-20, "-470"), (-20, "-471"), (-20, "-472"), (-20, "-473"), (-20, "-474"),
        (-20, "-478"), (-10, "-1000"), (-10, "-1001"), (-10, "-1002"), (-10, "-1003"), (-10, "-1004"), (-10, "-1005"), (-10, "-1006"), (-10, "-1007"),
        (-10, "-1008"), (-10, "-1009"), (-10, "-1010"), (0, "-2000"), (0, "-2100"), (0, "-2101"), (0, "-2102"), (0, "-2103"), (0, "-2104"),
        (0, "-2105"), (0, "-2106"), (0, "~115"), (0, "~116"), (0, "~114"), (0, "~113"), (0, "~110"), (0, "~111"), (0, "~112"), (0, "~100"),
        (0, "~102"), (0, "~101"), (0, "~130"), (0, "~133"), (0, "~132"), (0, "~135"), (0, "~134"), (0, "~131"), (0, "~122"), (0, "~121"),
        (0, "~120"), (1, "~502"), (1, "~500"), (1, "~501"), (1, "~431"), (1, "~402"), (1, "~401"), (1, "~411"), (1, "~403"), (1, "~400"),
        (1, "~412"), (1, "~413"), (1, "~410"), (1, "~430"), (1, "~420"), (1, "~610"), (1, "~611"), (1, "~612"), (1, "~602"), (1, "~613"),
        (1, "~614"), (1, "~617"), (1, "~600"), (1, "~616"), (1, "~615"), (1, "~601"), (0, "~146"), (0, "~142"), (0, "~141"), (0, "~140"),
        (0, "~103"), (0, "~104")
    ];

    [Fact]
    public void TheHabboCatalogHas162Variables()
    {
        Assert.Equal(162, HabboBuiltIns.Length);
    }

    [Fact]
    public void GiveChangeAndRemoveVariableSaveOnEveryBuiltInVariable()
    {
        var failures = new List<string>();

        foreach (var card in new[] { Give, Change, Remove }) {
            var service = VariableIdRoom(card);

            foreach (var (target, id) in HabboBuiltIns) {
                var answer = SaveVariableCard(service, card, target, id);
                var reopened = ReopenVariableIds();

                if (answer != "saved" || reopened[0] != id) {
                    failures.Add($"{card} {id}: {answer}, reopened {string.Join("/", reopened)}");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join("; ", failures.Take(12)));
    }

    // Each id goes in the first slot of Give Variable, Change Variable Value and Remove Variable; the three cards answer the same.
    [Theory]
    [InlineData("-110", 1, "-110")] // the furni @state id on a user target
    [InlineData("-230", 0, "-230")] // the user @position.x id on a furni target
    [InlineData("-110", -20, "-110")]
    [InlineData("-300", 0, "-300")]
    [InlineData("-420", 0, "-420")]
    [InlineData("313146", 0, "313146")] // a custom id that names nothing
    [InlineData("5", 0, "5")]
    [InlineData("-5", 0, "-5")]
    [InlineData("~999", 0, "~999")]
    [InlineData("0", 0, "0")]
    [InlineData("n", 0, "0")]
    [InlineData("", 0, "0")]
    [InlineData("@state", 0, "0")]
    [InlineData("internal:@state", 0, "0")]
    [InlineData("abc", 0, "0")]
    [InlineData("-110x", 0, "0")]
    [InlineData("+5", 0, "5")]
    [InlineData("007", 0, "7")]
    [InlineData("5.0", 0, "0")]
    [InlineData(" 5", 0, "0")]
    [InlineData("5 ", 0, "0")]
    [InlineData("2147483647", 0, "2147483647")]
    [InlineData("2147483648", 0, "2147483648")]
    [InlineData("99999999999", 0, "99999999999")]
    [InlineData("-2147483648", 0, "-2147483648")]
    [InlineData("-2147483649", 0, "-2147483649")]
    [InlineData("-", 0, "0")]
    [InlineData("~", 0, "0")]
    [InlineData("~-5", 0, "~-5")]
    [InlineData("--5", 0, "--5")]
    [InlineData("0x10", 0, "0")]
    [InlineData("1e3", 0, "0")]
    [InlineData("\u0663", 0, "3")]
    [InlineData("-0", 0, "-0")]
    [InlineData("~0", 0, "~0")]
    [InlineData("00", 0, "0")]
    [InlineData("~131", 0, "~131")]
    [InlineData("~99999999999", 0, "~99999999999")]
    [InlineData("~abc", 0, "0")]
    [InlineData("@5", 0, "0")]
    [InlineData("9223372036854775807", 0, "9223372036854775807")]
    [InlineData("9223372036854775808", 0, "0")]
    [InlineData("-9223372036854775808", 0, "0")]
    [InlineData("-9223372036854775809", 0, "0")]
    [InlineData("~9223372036854775807", 0, "~9223372036854775807")]
    [InlineData("~9223372036854775808", 0, "0")]
    [InlineData("\u0663\u0664", 0, "34")]
    [InlineData("-\u0663", 0, "-3")]
    [InlineData("~\u0663", 0, "~3")]
    [InlineData("\uFF15", 0, "5")]
    [InlineData("\u22125", 0, "0")]
    [InlineData("5\n", 0, "0")]
    [InlineData("+0", 0, "0")]
    [InlineData("-+5", 0, "-5")]
    [InlineData("~+5", 0, "~5")]
    [InlineData("-007", 0, "-7")]
    [InlineData("~007", 0, "~7")]
    [InlineData("- 5", 0, "0")]
    [InlineData("~~5", 0, "0")]
    [InlineData("-~5", 0, "0")]
    [InlineData("0000000000000000000000005", 0, "5")]
    [InlineData("-00", 0, "-0")]
    public void AnyVariableIdSavesAndANonNumberIsStoredAsZero(string sent, int target, string reopened)
    {
        foreach (var card in new[] { Give, Change, Remove }) {
            var service = VariableIdRoom(card);

            Assert.Equal("saved", SaveVariableCard(service, card, target, sent));
            Assert.Equal(reopened, ReopenVariableIds()[0]);
        }
    }

    // The reference slot of Give Variable and Change Variable Value is stored the same way (the value option on and a furni reference target).
    [Theory]
    [InlineData("5", "5")]
    [InlineData("+5", "5")]
    [InlineData("abc", "0")]
    [InlineData("-110", "-110")]
    [InlineData("~131", "~131")]
    [InlineData("", "0")]
    [InlineData("n", "0")]
    public void TheReferenceSlotIsStoredLikeTheVariableSlot(string sent, string reopened)
    {
        foreach (var card in new[] { Give, Change }) {
            var service = VariableIdRoom(card);

            Assert.Equal("saved", SaveVariableCard(service, card, 0, "313279", sent, 0));
            Assert.Equal(["313279", reopened], ReopenVariableIds());
        }
    }

    private WiredConfigurationService VariableIdRoom(string card)
    {
        var service = UseVariableRows();

        if (_room.GetRoomItemHandler().GetItem(VariableIdTile) is null) {
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, Furni(VariableIdTile, InteractionType.None, WiredBoxType.None), 3, 3, 0, true, false, false));
        }

        _variableIdBox = VariableIdBox + (uint)Array.IndexOf([Give, Change, Remove], card);
        PlaceVariableBox(_variableIdBox, card);

        return service;
    }

    // The raw action save the client sends: item, ints, text, picks, delay, furni sources, user sources, variable ids, second picks.
    private string SaveVariableCard(WiredConfigurationService service, string card, int target, string variableId, string reference = "0", int referenceTarget = -10)
    {
        object[] body = card switch
        {
            Give => [(int)_variableIdBox, 7, target, 0, 1, 1, 0, referenceTarget, 0, "", 1, (int)VariableIdTile, 0, 2, 100, 200, 2, 0, 200, 2, variableId, reference, 0],
            Change => [(int)_variableIdBox, 7, target, 0, 0, 0, 1, referenceTarget, 0, "", 1, (int)VariableIdTile, 0, 2, 100, 200, 2, 0, 200, 2, variableId, reference, 0],
            _ => [(int)_variableIdBox, 1, target, "", 1, (int)VariableIdTile, 0, 1, 100, 1, 0, 1, variableId, 0]
        };
        _client.Packets.Clear();
        new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket(body)).GetAwaiter().GetResult();

        return SaveAnswer();
    }

    // The raw WIRED_OPEN packet, then the variable ids of the effect editor frame.
    private string[] ReopenVariableIds()
    {
        _client.Packets.Clear();
        new OpenWiredEvent().Parse(_room, _client, ClientPacket((int)_variableIdBox)).GetAwaiter().GetResult();
        var frame = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.WiredEffectConfigComposer).Body };

        frame.ReadInt();
        SkipInts(frame);
        SkipInts(frame);
        frame.ReadInt();
        frame.ReadUInt();
        frame.ReadString();
        SkipInts(frame);

        return [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadString())];
    }
}
