using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// The Movement Curve add-on stores every save in a normal form. The operand kind is limited to 0..1 and the strength to -1000..1000.
// A reference target other than 0, 1, -10 and -20 becomes 0. A missing int takes the default [0, 160, -20]. An extra int and a text are dropped.
// Two variables get the variable count key. Each save gets exactly one answer, and the box reopens with the stored form. The saves run in order on one box.
public partial class PlacedFurniRoomTests
{
    private const uint MovementCurveBox = 960;
    private const string MovementCurve = "wf_xtra_mov_curve";

    private static readonly (int[] Ints, string Text, string[] Variables, string Answer, int[] Reopened)[] MovementCurveSaves =
    [
        ([0, 0, 0], "", ["0"], "saved", [0, 0, 0]),
        ([0, 1000, 0], "", ["0"], "saved", [0, 1000, 0]),
        ([0, 1001, 0], "", ["0"], "saved", [0, 1000, 0]),
        ([0, 1500, 0], "", ["0"], "saved", [0, 1000, 0]),
        ([0, -1000, 0], "", ["0"], "saved", [0, -1000, 0]),
        ([0, -1001, 0], "", ["0"], "saved", [0, -1000, 0]),
        ([0, 100000, 0], "", ["0"], "saved", [0, 1000, 0]),
        ([0, int.MaxValue, 0], "", ["0"], "saved", [0, 1000, 0]),
        ([0, int.MinValue, 0], "", ["0"], "saved", [0, -1000, 0]),
        ([1, 160, 0], "", ["0"], "saved", [1, 160, 0]),
        ([2, 160, 0], "", ["0"], "saved", [1, 160, 0]),
        ([3, 160, 0], "", ["0"], "saved", [1, 160, 0]),
        ([-1, 160, 0], "", ["0"], "saved", [0, 160, 0]),
        ([100, 160, 0], "", ["0"], "saved", [1, 160, 0]),
        ([0, 160, 1], "", ["0"], "saved", [0, 160, 1]),
        ([0, 160, -10], "", ["0"], "saved", [0, 160, -10]),
        ([0, 160, -20], "", ["0"], "saved", [0, 160, -20]),
        ([0, 160, 2], "", ["0"], "saved", [0, 160, 0]),
        ([0, 160, 3], "", ["0"], "saved", [0, 160, 0]),
        ([0, 160, -5], "", ["0"], "saved", [0, 160, 0]),
        ([0, 160, 99], "", ["0"], "saved", [0, 160, 0]),
        ([0, 160, -1], "", ["0"], "saved", [0, 160, 0]),
        ([], "", ["0"], "saved", [0, 160, -20]),
        ([5], "", ["0"], "saved", [1, 160, -20]),
        ([0, 300], "", ["0"], "saved", [0, 300, -20]),
        ([0, 300, -20, 9], "", ["0"], "saved", [0, 300, -20]),
        ([1, 160, -20], "", [""], "saved", [1, 160, -20]),
        ([1, 160, 1], "", ["user:1", "ctx:2"], "wiredfurni.error.invalid_variable_count", [1, 160, -20]),
        ([0, 160, 0], "abc", ["0"], "saved", [0, 160, 0])
    ];

    [Fact]
    public void MovementCurveStoresEachSaveInTheHabboFormAndReopensIt()
    {
        Assert.True(WiredBoxRegistry.TryGet(MovementCurve, out var descriptor));
        var service = UseVariableRows();
        PlaceVariableBox(MovementCurveBox, MovementCurve);
        var failures = new List<string>();

        foreach (var (ints, text, variables, answer, reopened) in MovementCurveSaves) {
            var packets = SaveMovementCurve(service, descriptor, ints, text, variables);
            var actual = SaveAnswer();
            var (owned, reopenedText, reopenedVariables) = ReopenMovementCurve();

            if (packets != 1 || actual != answer || !owned.SequenceEqual(reopened) || reopenedText != "" || !reopenedVariables.SequenceEqual(["0"])) {
                failures.Add($"[{string.Join(',', ints)}] '{text}' {variables.Length} vars: {packets} answers, {actual}, reopened [{string.Join(',', owned)}] "
                    + $"'{reopenedText}' [{string.Join(',', reopenedVariables)}]; expected {answer}, [{string.Join(',', reopened)}]");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // The raw add-on save: item, ints, text, picks, furni sources, user sources, variable ids, second picks. Returns the packets it answered.
    private int SaveMovementCurve(WiredConfigurationService service, WiredBoxDescriptor descriptor, int[] ints, string text, string[] variables)
    {
        Assert.Equal(WiredBoxCategory.Addon, descriptor.Category);
        object[] body = [(int)MovementCurveBox, ints.Length, .. ints.Cast<object>(), text, 0, 1, 100, 1, 0, variables.Length, .. variables.Cast<object>(), 0];
        _client.Packets.Clear();
        new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket(body)).GetAwaiter().GetResult();

        return _client.Packets.Count;
    }

    // The raw WIRED_OPEN packet, then the text, the owned ints and the variable ids of the add-on editor frame (WiredLegacyProtocol.Write).
    private (int[] Owned, string Text, string[] Variables) ReopenMovementCurve()
    {
        _client.Packets.Clear();
        new OpenWiredEvent().Parse(_room, _client, ClientPacket((int)MovementCurveBox)).GetAwaiter().GetResult();
        var frame = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.WiredAddonConfigComposer).Body };
        frame.ReadInt();
        _ = Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadInt()).ToArray();
        _ = Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadInt()).ToArray();
        frame.ReadInt();
        frame.ReadInt();
        var text = frame.ReadString();
        int[] owned = [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadInt())];
        string[] variables = [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadString())];

        return (owned, text, variables);
    }
}
