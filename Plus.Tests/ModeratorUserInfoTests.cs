using System.Reflection;
using System.Text;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms.Chat.Commands.Moderator;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public sealed class ModeratorUserInfoTests
{
    private const string Mail = "target@example.com";
    private const string EmailRight = "moderation.user_email";

    [Fact]
    public void ModToolSendsAnEmptyEmailToModeratorsWithoutTheEmailRight()
    {
        Assert.Equal("", ModToolEmail([PermissionKeys.ModerationTool]));
    }

    [Fact]
    public void ModToolSendsTheEmailToStaffWithTheEmailRight()
    {
        Assert.Equal(Mail, ModToolEmail([PermissionKeys.ModerationTool, EmailRight]));
    }

    [Fact]
    public void UserInfoHidesTheEmailFromModeratorsWithoutTheEmailRight()
    {
        var text = UserInfo([PermissionKeys.CommandUserinfo], ["target"]);

        Assert.Contains("target's account", text);
        Assert.DoesNotContain(Mail, text);
        Assert.DoesNotContain("Email:", text);
    }

    [Fact]
    public void UserInfoShowsTheEmailToStaffWithTheEmailRight()
    {
        var text = UserInfo([PermissionKeys.CommandUserinfo, EmailRight], ["target"]);

        Assert.Contains($"Email: {Mail}", text);
    }

    [Fact]
    public void UserInfoReadsTheUsernameRightAfterTheCommand()
    {
        var text = UserInfo([PermissionKeys.CommandUserinfo], ["target"]);

        Assert.Contains("target's account", text);
    }

    // Reads the whole packet in the order ModeratorUserInfoComposer writes it, so a changed layout fails too.
    private static string ModToolEmail(string[] rights)
    {
        var (session, sent) = HabbiconTestSupport.Client(Viewer(rights));
        var clients = RoomChatServiceTests.ClientDirectory.Create(out _);

        new ModeratorUserInfoService(new Users(), clients, null!, TimeProvider.System).Send(session, 42);

        var (header, payload) = Assert.Single(sent);
        Assert.Equal(ServerPacketHeader.ModeratorUserInfoComposer, header);
        var packet = new FlashIncomingPacket { Buffer = payload };
        Assert.Equal(42, packet.ReadInt());
        Assert.Equal("target", packet.ReadString());
        Assert.Equal("figure", packet.ReadString());
        packet.ReadInt();
        packet.ReadInt();
        Assert.False(packet.ReadBool());

        for (var i = 0; i < 5; i++) {
            packet.ReadInt();
        }

        Assert.Equal("0", packet.ReadString());
        Assert.Equal("", packet.ReadString());
        packet.ReadInt();
        packet.ReadInt();
        var email = packet.ReadString();
        Assert.Equal("", packet.ReadString());
        Assert.Equal(0, packet.Buffer.Length);

        return email;
    }

    private static string UserInfo(string[] rights, string[] parameters)
    {
        var (session, sent) = HabbiconTestSupport.Client(Viewer(rights));
        var clients = RoomChatServiceTests.ClientDirectory.Create(out _);

        new UserInfoCommand(new Users(), clients, AccessLookup.Create()).Execute(session, null!, parameters);

        return Encoding.UTF8.GetString(Assert.Single(sent).Payload);
    }

    private static Habbo Viewer(string[] rights) => new() { Id = 1, Username = "viewer", Access = EditorTestSupport.Access(rights, 50) };

    private sealed class Users : IModerationUserStore
    {
        private readonly ModerationUserData _target = new() { Id = 42, Username = "target", Look = "figure", Mail = Mail };
        public ModerationUserData? Find(int userId) => userId == _target.Id ? _target : null;
        public ModerationUserData? Find(string username) => username == _target.Username ? _target : null;
    }

    public class AccessLookup : DispatchProxy
    {
        public static IAccessControl Create() => DispatchProxy.Create<IAccessControl, AccessLookup>();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name == nameof(IAccessControl.Resolve)
            ? UserAccess.Empty
            : throw new NotSupportedException(targetMethod?.Name);
    }
}
