using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    // The state follows the first (empty) Users packet and comes before the floor items.
    [Fact]
    public void TheEntryStateIsSentRightAfterTheFirstUsersPacketAndBeforeTheFloorItems()
    {
        using var f = new TeleportFixture();
        f.Habbo.Gender = "M";
        f.Habbo.Motto = "";
        f.Habbo.Look = "test";
        f.Habbo.HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0);
        f.Room.GetWired().RememberHighlight([1], 0, 100, 0xFFEA00, 0, 4);
        f.Room.GetWired().RememberUserHighlight([f.User.VirtualId], 0, 100, 0xFFEA00, 0, 4);
        var sent = Capture(f.Client);

        f.Room.SendObjects(f.Client);

        var headers = sent.Select(reply => reply.Header).ToList();
        var users = headers.IndexOf(ServerPacketHeader.UsersComposer);
        Assert.True(users >= 0);
        Assert.Equal(users + 1, headers.IndexOf(ServerPacketHeader.WiredFurniVisualizationStateComposer));
        Assert.Equal(users + 2, headers.IndexOf(ServerPacketHeader.WiredUserVisualizationStateComposer));
        Assert.True(headers.IndexOf(ServerPacketHeader.WiredUserVisualizationStateComposer) < headers.IndexOf(ServerPacketHeader.ObjectsComposer));
    }
}
