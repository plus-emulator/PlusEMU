using System.Reflection;
using System.Text;
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

    [Fact]
    public void UserInfoReadsTheUsernameRightAfterTheCommand()
    {
        var text = UserInfo([PermissionKeys.CommandUserinfo], ["target"]);

        Assert.Contains("target's account", text);
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
