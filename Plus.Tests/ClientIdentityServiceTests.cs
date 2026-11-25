using Plus.Communication.Packets.Incoming.Handshake;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public sealed class ClientIdentityServiceTests
{
    [Fact]
    public async Task IdentityHandlerDecodesBothStringsBeforeDelegatingTheMachineId()
    {
        var identity = new RecordingIdentity();
        var packet = HabbiconTestSupport.Incoming("machine", "fingerprint");
        await new UniqueIdEvent(identity).Parse(null!, packet);
        Assert.Equal("machine", identity.MachineId);
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public async Task TruncatedIdentityFrameDoesNotDelegate()
    {
        var identity = new RecordingIdentity();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new UniqueIdEvent(identity).Parse(null!, HabbiconTestSupport.Incoming("ignored")));
        Assert.Null(identity.MachineId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IdentityIsCapturedBeforeTheBanCheckAndOnlyAnAllowedMachineGetsAReply(bool banned)
    {
        var (client, sent) = HabbiconTestSupport.Client(null);
        var disconnected = false;
        client.DisconnectRequested = () => disconnected = true;
        var moderation = CatalogSnapshotTestSupport.Proxy<IModerationManager>((method, args) =>
        {
            Assert.Equal(nameof(IModerationManager.HasBan), method);
            Assert.Equal(ModerationBanType.Machine, args![0]);
            Assert.Equal("machine", args[1]);
            Assert.Equal("machine", client.MachineId);
            Assert.False(disconnected);
            Assert.Empty(sent);

            return banned;
        });

        new ClientIdentityService(moderation).SetMachineIdentity(client, "machine");

        Assert.Equal(banned, disconnected);
        Assert.Equal(banned, client.Closed.IsCancellationRequested);

        if (banned) {
            Assert.Empty(sent);
        }
        else {
            var response = Assert.Single(sent);
            Assert.Equal(ServerPacketHeader.SetUniqueIdComposer, response.Header);
            var body = new Plus.Communication.Flash.FlashIncomingPacket { Buffer = response.Payload };
            Assert.Equal("machine", body.ReadString());
            Assert.False(body.HasDataRemaining());
        }
    }

    [Fact]
    public void OnlyTheFirstMachineIdOfAConnectionIsRecordedAndEchoed()
    {
        var (client, sent) = HabbiconTestSupport.Client(null);
        var service = new ClientIdentityService(NoBans());

        service.SetMachineIdentity(client, "first");
        service.SetMachineIdentity(client, "second");

        Assert.Equal("first", client.MachineId);
        Assert.Equal(new[] { "first" }, Echoes(sent));
        Assert.False(client.Closed.IsCancellationRequested);
    }

    [Fact]
    public void MachineIdAfterLoginIsIgnored()
    {
        var (client, sent) = HabbiconTestSupport.Client(new Habbo { Id = 1 });

        new ClientIdentityService(NoBans()).SetMachineIdentity(client, "late");

        Assert.Equal(string.Empty, client.MachineId);
        Assert.Empty(sent);
        Assert.False(client.Closed.IsCancellationRequested);
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    [InlineData(64, false)]
    [InlineData(2000, false)]
    public void MachineIdLongerThanABanValueIsIgnoredAndKeepsTheConnection(int length, bool accepted)
    {
        var (client, sent) = HabbiconTestSupport.Client(null);
        var machineId = new string('A', length);

        new ClientIdentityService(NoBans()).SetMachineIdentity(client, machineId);

        Assert.Equal(accepted ? new[] { machineId } : Array.Empty<string>(), Echoes(sent));
        Assert.Equal(accepted ? machineId : string.Empty, client.MachineId);
        Assert.False(client.Closed.IsCancellationRequested);
    }

    [Fact]
    public void IgnoredLongMachineIdLeavesRoomForAValidOne()
    {
        var (client, sent) = HabbiconTestSupport.Client(null);
        var service = new ClientIdentityService(NoBans());

        service.SetMachineIdentity(client, new string('A', 64));
        service.SetMachineIdentity(client, "valid");

        Assert.Equal("valid", client.MachineId);
        Assert.Equal(new[] { "valid" }, Echoes(sent));
        Assert.False(client.Closed.IsCancellationRequested);
    }

    private static IModerationManager NoBans() => CatalogSnapshotTestSupport.Proxy<IModerationManager>((method, _) =>
        method == nameof(IModerationManager.HasBan) ? false : throw new NotSupportedException(method));

    private static string[] Echoes(List<(uint Header, byte[] Payload)> sent) => sent.Select(packet =>
    {
        Assert.Equal(ServerPacketHeader.SetUniqueIdComposer, packet.Header);

        return new Plus.Communication.Flash.FlashIncomingPacket { Buffer = packet.Payload }.ReadString();
    }).ToArray();

    private sealed class RecordingIdentity : IClientIdentityService
    {
        public string? MachineId;
        public void SetMachineIdentity(GameClient session, string machineId) => MachineId = machineId;
    }
}
