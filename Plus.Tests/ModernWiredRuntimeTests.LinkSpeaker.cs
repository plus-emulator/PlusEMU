using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Addons;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    private static (TeleportFixture F, Item Sender, Item Receiver) SpeakerRoom(bool receiverHears)
    {
        var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        var store = new MemoryLinkStore();
        f.Room.GetWired().LinkStore = store;
        var sender = LinkFurni(f, 50, 0, 1);
        var receiver = LinkFurni(f, 51, 0, 2);
        store.Join(sender.Id, 7, false);
        store.Join(receiver.Id, 7, receiverHears);

        return (f, sender, receiver);
    }

    private static void SpeakerAddon(TeleportFixture f, uint id, int x, int y, int message, int dataframe, params Item[] picks) =>
        OctoberBox<WiredOctoberAddonBox>(f, id, "wf_xtra_link_speaker", x, y, native => native with
        {
            OwnedIntParams = [message, dataframe],
            PrimaryItems = [.. picks.Select(pick => new Plus.HabboHotel.Items.Wired.Configuration.WiredNativeItemReference(pick.Id, false))],
            FurniSourceTypes = [100]
        });

    private static void SpeakerSendDataframe(TeleportFixture f, Item to) =>
        OctoberBox<WiredModernAction>(f, 110, "wf_act_send_dataframe", 0, 0, native => native with
        {
            OwnedIntParams = [2, 0, 5, -20, 3, 0, -20, .. Enumerable.Range(0, 10).SelectMany(_ => new[] { 0, 0, -20 })],
            Text = "[[\"num\",\"\"],[\"txt\",\"hi\"]]",
            PrimaryItems = [new(to.Id, false)],
            FurniSourceTypes = [100, .. Enumerable.Repeat(200, 12)],
            UserSourceTypes = [.. Enumerable.Repeat(0, 12)]
        });

    private static List<byte[]> SpeakerBubbles(List<(uint Header, byte[] Body)> sent) =>
        [.. sent.Where(reply => reply.Header == ServerPacketHeader.ChatComposer).Select(reply => reply.Body)
            .Where(body => System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(body) == -1)];

    private static void AssertBubble(TeleportFixture f, byte[] body, string text, int x, int y)
    {
        var bubble = new WireReader(body);
        Assert.Equal((-1, text, 0, 253, 0, -1, f.User.VirtualId, -1, true, x, y),
            (bubble.Int(), bubble.String(), bubble.Int(), bubble.Int(), bubble.Int(), bubble.Int(), bubble.Int(), bubble.Int(), bubble.Bool(), bubble.Int(), bubble.Int()));
        bubble.End();
    }

    [Fact]
    public void LinkSpeakerShowsABubbleWhenItsLinkReceivesAMessage()
    {
        var (f, sender, receiver) = SpeakerRoom(true);
        using var _ = f;
        LinkSend(f, "msg1", receiver);
        SpeakerAddon(f, 130, 1, 2, 1, 1, receiver);
        var sent = Capture(f.Client);

        f.Fire(land: false);
        f.Advance(50);

        AssertBubble(f, Assert.Single(SpeakerBubbles(sent)), "msg1", 1, 2);
    }

    [Fact]
    public void LinkSpeakerShowsTheRowsOfAReceivedDataframe()
    {
        var (f, sender, receiver) = SpeakerRoom(true);
        using var _ = f;
        SpeakerSendDataframe(f, receiver);
        SpeakerAddon(f, 130, 3, 4, 1, 1, receiver);
        var sent = Capture(f.Client);

        f.Fire(land: false);
        f.Advance(50);

        AssertBubble(f, Assert.Single(SpeakerBubbles(sent)), "[b]num[/b]: 5\n[b]txt[/b]: hi", 3, 4);
    }

    [Fact]
    public void LinkSpeakerSpeaksOncePerSpeakerThatPicksTheLink()
    {
        var (f, sender, receiver) = SpeakerRoom(true);
        using var _ = f;
        LinkSend(f, "msg1", receiver);
        SpeakerAddon(f, 130, 1, 2, 1, 1, receiver);
        SpeakerAddon(f, 131, 3, 2, 1, 1, receiver);
        SpeakerAddon(f, 132, 5, 2, 1, 1, sender);
        var sent = Capture(f.Client);

        f.Fire(land: false);
        f.Advance(50);

        Assert.Equal(2, SpeakerBubbles(sent).Count);
    }

    [Fact]
    public void LinkSpeakerStaysSilentWhenItsLinkDoesNotReceive()
    {
        var (f, _, receiver) = SpeakerRoom(false);
        using var _f = f;
        LinkSend(f, "msg1", receiver);
        SpeakerAddon(f, 130, 1, 2, 1, 1, receiver);
        var sent = Capture(f.Client);

        f.Fire(land: false);
        f.Advance(50);

        Assert.Empty(SpeakerBubbles(sent));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void LinkSpeakerSpeaksOnlyForTheKindsItHasOn(int message, int dataframe)
    {
        var (f, _, receiver) = SpeakerRoom(true);
        using var _f = f;
        LinkSend(f, "msg1", receiver);
        SpeakerAddon(f, 130, 1, 2, message, dataframe, receiver);
        var sent = Capture(f.Client);

        f.Fire(land: false);
        f.Advance(50);

        Assert.Equal(message, SpeakerBubbles(sent).Count);
    }

    [Fact]
    public void LinkSpeakerIsNotASendEndpoint()
    {
        var (f, sender, receiver) = SpeakerRoom(true);
        using var _ = f;
        OctoberBox<WiredModernAction>(f, 110, "wf_act_send_message", 0, 0, native => native with
        {
            OwnedIntParams = [1],
            Text = "msg1",
            FurniSourceTypes = [100]
        });
        SpeakerAddon(f, 130, 0, 0, 1, 1, sender);
        LinkReceive(f, 120, 2, 2, receiver);
        LinkLog(f, 121, 2, 2, "got $(trigger_text)");

        f.Fire(land: false);
        f.Advance(50);

        Assert.Empty(LinkLogs(f));
    }
}
