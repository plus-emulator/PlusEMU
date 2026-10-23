using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// A Show Message editor lists the placeholder names of its stack sorted by name.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public async Task ShowMessageEditorListsThePlaceholderNamesSortedByName()
    {
        await DelayedPointerRoom(executeInOrder: false);
        // A stack of its own, away from the speech trigger the room starts with, as that trigger brings $(trigger_text).
        _dpTile = (2, 2);
        var show = _dpNext;
        await DpShow("$(ptr)", 0);
        await DpOutput("ptr", DpUser, "user:" + DpPu);
        await DpOutput("cx", DpContext, "ctx:" + DpCtx);
        await DpOutput("ev", DpContext, "-459");
        await DpOutput("ct", DpContext, "-460");
        await DpOutput("nv", DpContext, "-462");

        var (placeholders, capturers) = await OpenMessageEditorPlaceholders(show);

        Assert.Equal(["ct", "cx", "ev", "nv", "ptr"], placeholders);
        Assert.Empty(capturers);
    }

    // Placeholder names sort by character code of the lower case name (a capital is stored in lower case). Capturer names sort the same way and are listed apart.
    [Fact]
    public async Task ShowMessageEditorSortsTheNamesOfAllSourcesAndTheCapturers()
    {
        await DelayedPointerRoom(executeInOrder: false);
        _dpTile = (2, 2);
        var show = _dpNext;
        AddSpeechBox(_dpNext++, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "hello" }, _dpZ++, 2, 2);
        await DpShow("x", 0);
        await DpOutput("Zed", DpContext, "ctx:" + DpCtx);
        await DpOutput("x10", DpContext, "ctx:" + DpCtx);
        await DpCapturer("zcap");
        await DpOutput("x2", DpContext, "ctx:" + DpCtx);
        await DpCapturer("acap");
        await DpOutput("a_b", DpContext, "ctx:" + DpCtx);
        await DpOutput("x_", DpContext, "ctx:" + DpCtx);

        var (placeholders, capturers) = await OpenMessageEditorPlaceholders(show + 1);

        Assert.Equal(["a_b", "trigger_text", "x10", "x2", "x_", "zed"], placeholders);
        Assert.Equal(["acap", "zcap"], capturers);
    }

    /// <summary>Text Input Variable in number mode: the capture name and a context variable.</summary>
    private Task DpCapturer(string name) => DpSave(WiredBoxCategory.Addon, "wf_xtra_text_input_variable", [0], name, [], null, [], [], ["ctx:" + DpCtx], []);

    // The raw WIRED_OPEN packet, then the context list of the Show Message editor frame up to the text placeholder entry (context 7): its placeholder names and its capturer names.
    private async Task<(string[] Placeholders, string[] Capturers)> OpenMessageEditorPlaceholders(uint id)
    {
        _client.Packets.Clear();
        await new OpenWiredEvent().Parse(_room, _client, ClientPacket((int)id));
        var frame = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.WiredEffectConfigComposer).Body };
        int[] Ints() => [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadInt())];
        string[] Strings() => [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadString())];

        frame.ReadInt();
        Ints();
        Ints();
        frame.ReadInt();
        Assert.Equal(id, frame.ReadUInt());
        frame.ReadString();
        Ints();
        Strings();
        Ints();
        Ints();
        frame.ReadInt();
        frame.ReadInt();
        frame.ReadBool();

        for (var groups = 0; groups < 2; groups++) {
            for (var count = frame.ReadInt(); count > 0; count--) {
                Ints();
            }
        }

        Ints();
        Ints();
        frame.ReadBool();

        for (var count = frame.ReadInt(); count > 0; count--) {
            switch (frame.ReadInt()) {
                case 0:
                    frame.ReadInt();
                    break;
                case 7:
                    return (Strings(), Strings());
                case var type:
                    throw new InvalidDataException($"Unexpected context type {type}.");
            }
        }

        throw new InvalidDataException("The editor has no text placeholder context.");
    }
}
