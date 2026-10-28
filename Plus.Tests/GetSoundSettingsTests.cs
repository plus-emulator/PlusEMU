using Microsoft.Extensions.Logging.Abstractions;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Sound;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Messenger.FriendBar;
using Xunit;

namespace Plus.Tests;

public sealed class GetSoundSettingsTests
{
    private const string FullFrame =
        "00000014" + "0000001E" + "00000028" + "01" + "00" + "01" + "00000001" + "00000000" +
        "01" + "00" + "01" + "00000000" + "01" + "00" + "0006766F6C746572" +
        "00000000" + "00000000" + "00000001" + "00000001" + "00000000";

    private static Habbo User() => new()
    {
        Id = 7,
        ClientVolume = [20, 30, 40],
        ChatPreference = true,
        AllowMessengerInvites = false,
        FocusPreference = true,
        FriendbarState = FriendBarStateUtility.GetEnum(1),
        WiredPreferences = new WiredAccountPreferences(true, false, true, true, false, "volter")
    };

    private static PacketManager Manager() => new([new GetSoundSettingsEvent()], NullLogger<PacketManager>.Instance);

    private static FlashIncomingPacket Request() => new() { Buffer = Array.Empty<byte>() };

    [Fact]
    public void TheRequestHasTheOfficialIdAndAHandlerInTheHeaderTable()
    {
        using var manager = Manager();
        var revision = new Plus.Communication.Revisions.RevisionsCache().InternalRevision;

        Assert.Equal(929u, ClientPacketHeader.GetSoundSettingsEvent);
        Assert.True(manager.IsRegistered(929));
        Assert.Equal(929u, revision.IncomingHeaders[nameof(ClientPacketHeader.GetSoundSettingsEvent)]);
        Assert.Single(revision.IncomingHeaders, pair => pair.Value == 929u);
        Assert.Equal(929u, revision.IncomingIdToInternalIdMapping[929]);
    }

    [Fact]
    public async Task TheRequestIsAnsweredWithTheFullAccountSettingsFrame()
    {
        var (session, sent) = HabbiconTestSupport.Client(User());
        using var manager = Manager();

        await manager.TryExecutePacket(session, 929, Request());

        var answer = Assert.Single(sent);
        Assert.Equal(ServerPacketHeader.SoundSettingsComposer, answer.Header);
        Assert.Equal(692u, answer.Header);
        Assert.Equal(FullFrame, Convert.ToHexString(answer.Payload));
    }

    [Fact]
    public async Task EveryRequestIsAnsweredWithTheCurrentSettings()
    {
        var habbo = User();
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        using var manager = Manager();

        await manager.TryExecutePacket(session, 929, Request());
        habbo.WiredPreferences = habbo.WiredPreferences with { ShowAllNotifications = true, UiStyle = "" };
        habbo.ClientVolume = [1, 2, 3];
        await manager.TryExecutePacket(session, 929, Request());

        Assert.Equal(2, sent.Count);
        Assert.Equal(FullFrame, Convert.ToHexString(sent[0].Payload));
        Assert.Equal("00000001" + "00000002" + "00000003" + "01" + "00" + "01" + "00000001" + "00000000" +
            "01" + "00" + "01" + "00000000" + "01" + "01" + "0000" +
            "00000000" + "00000000" + "00000001" + "00000001" + "00000000", Convert.ToHexString(sent[1].Payload));
    }

    [Fact]
    public async Task TheAnswerEqualsTheFrameOfTheFriendBarChange()
    {
        var habbo = User();
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        using var manager = Manager();

        await manager.TryExecutePacket(session, 929, Request());
        new UserProfileService(null!, null!, null!, null!, null!, TimeProvider.System, null!, null!, null!, null!).SetFriendBarState(session, 1);

        Assert.Equal(2, sent.Count);
        Assert.Equal(sent[1].Payload, sent[0].Payload);
    }
}
