using System.Text;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540 (three runs, identical): Text Output Username refuses and stores a name exactly as Text Output Furni
// Name does, with or without picks and on the box's own frame (no name, ints [0]); Text Output Variable also refuses an empty name on its own
// frame. The answer is a WiredValidationError packet with the key and no parameters (hex 0028 + "wiredfurni.error.placeholder.name_length" +
// 00000000). A name is stored in lower case; any non-zero first int is the show-multiple flag.
public partial class PlacedFurniRoomTests
{
    private const string UsernameOutput = "wf_xtra_text_output_username";
    private const string PlaceholderPrefix = "wiredfurni.error.placeholder.";
    private const uint UsernameBox = 701, TextVariableBox = 702;

    [Theory]
    [InlineData(0, "", false, "name_length")]
    [InlineData(0, "", true, "name_length")]
    [InlineData(1, "\t,", false, "name_length")]
    [InlineData(1, "\t,", true, "name_length")]
    [InlineData(0, "\ta", false, "name_length")]
    [InlineData(1, "\ta", false, "name_length")]
    [InlineData(0, " ", false, "name_syntax")]
    [InlineData(0, " ", true, "name_syntax")]
    [InlineData(0, "a b", true, "name_syntax")]
    [InlineData(0, "a-b", false, "name_syntax")]
    [InlineData(1, " \t,", true, "name_syntax")]
    [InlineData(0, "trigger_text", false, "reserved")]
    [InlineData(1, "trigger_text\t,", true, "reserved")]
    [InlineData(1, "ab", false, "delimiter_empty")]
    [InlineData(1, "a\t", false, "delimiter_empty")]
    [InlineData(1, "a\t\t", false, "delimiter_empty")]
    [InlineData(1, "a\t,,,,,,", false, "delimiter_length")]
    [InlineData(2, "ab", false, "delimiter_empty")]
    [InlineData(-1, "ab", false, "delimiter_empty")]
    [InlineData(100, "ab", false, "delimiter_empty")]
    public void TextOutputUsernameRefusesAnInvalidNameWithTheKeyAndNoParameters(int multiple, string text, bool picks, string key)
    {
        var service = UseVariableRows();
        PlaceVariableBox(UsernameBox, UsernameOutput);

        var answer = SaveUsernameOutput(service, [multiple], text, picks ? [0, 1] : []);

        Assert.Equal(PlaceholderPrefix + key, answer);
        Assert.Equal(RefusalBody(PlaceholderPrefix + key), Assert.Single(_client.Packets, p => p.Header == ServerPacketHeader.WiredValidationErrorComposer).Body);
    }

    [Fact]
    public void TextOutputUsernameRefusesAFortyOneCharacterNameAndKeepsFortyCharacters()
    {
        var service = UseVariableRows();
        PlaceVariableBox(UsernameBox, UsernameOutput);

        Assert.Equal(PlaceholderPrefix + "name_length", SaveUsernameOutput(service, [0], new string('a', 41), [0, 1]));
        Assert.Equal(PlaceholderPrefix + "name_length", SaveUsernameOutput(service, [1], new string('a', 41) + "\t,", [0, 1]));
        Assert.Equal("saved", SaveUsernameOutput(service, [0], new string('a', 40), [0, 1]));
        AssertUsername(new string('a', 40), [0]);
    }

    // The stored text and the show-multiple int of a save that is kept, with or without picks (the rows with picks are those saved with picks).
    // A single name keeps the name only; a placeholder for several values keeps the name and the delimiter and cuts a third part. Missing and
    // extra ints take the frame's one int.
    [Theory]
    [InlineData(new[] { 0 }, "a", "a", 0, false)]
    [InlineData(new[] { 0 }, "a", "a", 0, true)]
    [InlineData(new[] { 0 }, "A", "a", 0, true)]
    [InlineData(new[] { 1 }, "a\t,", "a\t,", 1, true)]
    [InlineData(new[] { 1 }, "A\t,", "a\t,", 1, true)]
    [InlineData(new int[0], "ab", "ab", 0, false)]
    [InlineData(new[] { 0, 5 }, "ab", "ab", 0, false)]
    [InlineData(new[] { 0, 0, 0 }, "ab", "ab", 0, false)]
    [InlineData(new[] { 0 }, "A", "a", 0, false)]
    [InlineData(new[] { 0 }, "a\tb", "a", 0, false)]
    [InlineData(new[] { 0 }, "a\t,\tx", "a", 0, false)]
    [InlineData(new[] { 0 }, "a\t,,,,,,", "a", 0, false)]
    [InlineData(new[] { 0 }, "a\t", "a", 0, false)]
    [InlineData(new[] { 0 }, "Ab\t, x", "ab", 0, false)]
    [InlineData(new[] { 1 }, "a\t,", "a\t,", 1, false)]
    [InlineData(new[] { 1 }, "A\t,", "a\t,", 1, false)]
    [InlineData(new[] { 1 }, "a\tb", "a\tb", 1, false)]
    [InlineData(new[] { 1 }, "a\t,\tx", "a\t,", 1, false)]
    [InlineData(new[] { 1 }, "a\t,,,,,", "a\t,,,,,", 1, false)]
    [InlineData(new[] { 1 }, "Ab\t, x", "ab\t, x", 1, false)]
    [InlineData(new[] { 1 }, "a\t,\t", "a\t,", 1, false)]
    public void TextOutputUsernameSavesAValidNameInTheStoredForm(int[] ints, string text, string stored, int storedMultiple, bool picks)
    {
        var service = UseVariableRows();
        PlaceVariableBox(UsernameBox, UsernameOutput);

        Assert.Equal("saved", SaveUsernameOutput(service, ints, text, picks ? [0, 1] : []));
        AssertUsername(stored, [storedMultiple]);
    }

    // Text Output Variable on its own frame (ints [0, 1, 0], no variable, no name) is refused with the same key.
    [Fact]
    public void TextOutputVariableRefusesAnEmptyNameOnItsOwnFrame()
    {
        var service = UseVariableRows();
        PlaceVariableBox(TextVariableBox, "wf_xtra_text_output_variable");
        object[] body = [(int)TextVariableBox, 3, 0, 1, 0, "", 0, 1, 100, 1, 0, 1, "0", 0];
        _client.Packets.Clear();
        new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket(body)).GetAwaiter().GetResult();

        Assert.Equal(PlaceholderPrefix + "name_length", SaveAnswer());
        Assert.Equal(RefusalBody(PlaceholderPrefix + "name_length"), Assert.Single(_client.Packets, p => p.Header == ServerPacketHeader.WiredValidationErrorComposer).Body);
    }

    // The same with show-multiple on.
    [Fact]
    public void TextOutputVariableRefusesAnEmptyNameWithShowMultipleOn()
    {
        var service = UseVariableRows();
        PlaceVariableBox(TextVariableBox, "wf_xtra_text_output_variable");
        object[] body = [(int)TextVariableBox, 3, 1, 1, 0, "", 0, 1, 100, 1, 0, 1, "0", 0];
        _client.Packets.Clear();
        new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket(body)).GetAwaiter().GetResult();

        Assert.Equal(PlaceholderPrefix + "name_length", SaveAnswer());
    }

    private static byte[] RefusalBody(string key) => [(byte)(key.Length >> 8), (byte)key.Length, .. Encoding.ASCII.GetBytes(key), 0, 0, 0, 0];

    // The raw add-on save of the box's own frame: ints, text, picks, no furni source, the user source 0, no variable and no second picks.
    private string SaveUsernameOutput(WiredConfigurationService service, int[] ints, string text, int[] picks, int[]? users = null)
    {
        users ??= [0];
        object[] body = [(int)UsernameBox, ints.Length, .. ints.Cast<object>(), text, picks.Length, .. picks.Select(id => (object)(int)(SecondListBase + id)), 0,
            users.Length, .. users.Cast<object>(), 0, 0];
        _client.Packets.Clear();
        new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket(body)).GetAwaiter().GetResult();

        return SaveAnswer();
    }

    // A user source Habbo refuses is answered before an empty name.
    [Fact]
    public void AnInvalidUserSourceIsAnsweredBeforeAnEmptyName()
    {
        var service = UseVariableRows();
        PlaceVariableBox(UsernameBox, UsernameOutput);

        Assert.Equal("wiredfurni.error.invalid_user_source", SaveUsernameOutput(service, [0], "", [], [100]));
    }

    // User sources of the probe: the source values that save, the ones that are refused with a key, one that gets no answer, two sources, and none.
    [Theory]
    [InlineData(new[] { 0 }, "saved", new[] { 0 })]
    [InlineData(new[] { 10 }, "saved", new[] { 10 })]
    [InlineData(new[] { 11 }, "saved", new[] { 11 })]
    [InlineData(new[] { 200 }, "saved", new[] { 200 })]
    [InlineData(new[] { 201 }, "saved", new[] { 201 })]
    [InlineData(new int[0], "saved", new[] { 0 })]
    [InlineData(new[] { 100 }, "wiredfurni.error.invalid_user_source", new[] { 0 })]
    [InlineData(new[] { 101 }, "wiredfurni.error.invalid_user_source", new[] { 0 })]
    [InlineData(new[] { 900 }, "wiredfurni.error.invalid_user_source", new[] { 0 })]
    [InlineData(new[] { 0, 200 }, "wiredfurni.error.invalid_user_source_count", new[] { 0 })]
    [InlineData(new[] { 1 }, "dropped", new[] { 0 })]
    public void TextOutputUsernameAnswersItsUserSources(int[] users, string answer, int[] stored)
    {
        var service = UseVariableRows();
        PlaceVariableBox(UsernameBox, UsernameOutput);

        Assert.Equal(answer, SaveUsernameOutput(service, [0], "ab", [], users));

        if (answer == "saved") {
            Assert.Equal(stored, ReopenUsername().Users);
        }
    }

    private void AssertUsername(string text, int[] ints)
    {
        var (actualText, actualInts, _) = ReopenUsername();
        Assert.Equal(text, actualText);
        Assert.Equal(ints, actualInts);
    }

    // The raw WIRED_OPEN packet, then the text and the ints of the add-on frame that follows the two pick lists, the sprite and the item id.
    private (string Text, int[] Ints, int[] Users) ReopenUsername()
    {
        _client.Packets.Clear();
        new OpenWiredEvent().Parse(_room, _client, ClientPacket((int)UsernameBox)).GetAwaiter().GetResult();
        var frame = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, p => p.Header == ServerPacketHeader.WiredAddonConfigComposer).Body };
        frame.ReadInt();

        for (var list = 0; list < 2; list++) {
            for (var count = frame.ReadInt(); count > 0; count--) {
                frame.ReadInt();
            }
        }

        frame.ReadInt();
        frame.ReadInt();
        var text = frame.ReadString();
        int[] Ints() => [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadInt())];
        var ints = Ints();

        for (var variables = frame.ReadInt(); variables > 0; variables--) {
            frame.ReadString();
        }

        Ints();

        return (text, ints, Ints());
    }
}
