using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: a Show Message editor lists the placeholder names of its stack sorted by name. Text
// Output Variable boxes named ptr, cx, ev, ct and nv, stacked in that order above the Show Message, open as ct, cx, ev, nv, ptr.
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

    // The raw WIRED_OPEN packet, then the context list of the Show Message editor frame read the way AIR reads it, up to the text
    // placeholder entry (context 7): its placeholder names and its capturer names.
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
