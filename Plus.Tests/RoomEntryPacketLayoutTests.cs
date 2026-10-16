using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing.Rooms.Permissions;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

/// <summary>Reads each composer back in the order the WIN63-202610131318-860371703 parser reads it and requires no bytes left over.</summary>
public class RoomEntryPacketLayoutTests
{
    [Fact]
    public void OpenConnectionCarriesTheFlatId()
    {
        // class_2691.parse L22-26: flat id.
        var packet = Read(new OpenConnectionComposer(4521));

        Assert.Equal(4521, packet.ReadInt());
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void YouAreOwnerCarriesTheFlatId()
    {
        // class_4114.parse L27-30: flat id.
        var packet = Read(new YouAreOwnerComposer(4521));

        Assert.Equal(4521, packet.ReadInt());
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void WiredEnvironmentEndsAtTheEnabledAchievements()
    {
        // class_3405.parse L25-42: click user wired, n, achievement x n.
        var packet = Read(new WiredEnvironmentComposer(true, ["ACH_RoomEntry"]));

        Assert.True(packet.ReadBool());
        Assert.Equal(1, packet.ReadInt());
        Assert.Equal("ACH_RoomEntry", packet.ReadString());
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void UserChangeEndsAtTheBadgesRank()
    {
        // class_2587.parse L67-90: id, figure, sex, motto, achievement score, string, n x (int, int, int), badges rank.
        var packet = Read(new UserChangeComposer(new AvatarChangeSnapshot(-1, "hd-180-1", "M", "hi", 12)));

        Assert.Equal(-1, packet.ReadInt());
        Assert.Equal("hd-180-1", packet.ReadString());
        Assert.Equal("M", packet.ReadString());
        Assert.Equal("hi", packet.ReadString());
        Assert.Equal(12, packet.ReadInt());
        Assert.Equal("", packet.ReadString());
        Assert.Equal(0, packet.ReadInt());
        Assert.Equal(-1, packet.ReadInt());
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void UsersWritesEveryKindInTheParserOrder()
    {
        // class_2291.parse L72-186: common fields, then the branch for the user type, and nothing after it.
        var habbo = new RoomUserSnapshot(7, "Dennis", "hi", "hd-180-1", 1, 2, 3, "1.5", 2, 1, "m", 9, "Club", 12,
            false, 0, 0, "", false, false);
        var pet = new RoomUserSnapshot(10, "Rex", "", "0 0 ffffff", 2, 4, 5, "0", 0, 2, "", 0, "", 0,
            true, 13, 7, "Dennis", true, false, 6);
        var bot = new RoomUserSnapshot(11, "Frank", "hello", "hd-180-1", 3, 6, 7, "0", 0, 4, "f", 0, "", 0,
            false, 0, 7, "Dennis", false, false);
        var packet = Read(new UsersComposer([habbo, pet, bot]));

        Assert.Equal(3, packet.ReadInt());

        ReadCommon(packet, 7, "Dennis", 1);
        Assert.Equal("m", packet.ReadString());
        Assert.Equal(9, packet.ReadInt());
        Assert.Equal(0, packet.ReadInt());
        Assert.Equal("Club", packet.ReadString());
        Assert.Equal("", packet.ReadString());
        Assert.Equal(12, packet.ReadInt());
        Assert.False(packet.ReadBool());
        Assert.Equal(-1, packet.ReadInt());

        ReadCommon(packet, 10, "Rex", 2);
        Assert.Equal(13, packet.ReadInt());
        Assert.Equal(7, packet.ReadInt());
        Assert.Equal("Dennis", packet.ReadString());
        Assert.Equal(1, packet.ReadInt());
        Assert.True(packet.ReadBool());
        Assert.False(packet.ReadBool());
        Assert.False(packet.ReadBool());
        Assert.False(packet.ReadBool());
        Assert.False(packet.ReadBool());
        Assert.False(packet.ReadBool());
        Assert.Equal(6, packet.ReadInt());
        Assert.Equal("", packet.ReadString());

        ReadCommon(packet, 11, "Frank", 4);
        Assert.Equal("f", packet.ReadString());
        Assert.Equal(7, packet.ReadInt());
        Assert.Equal("Dennis", packet.ReadString());
        var skills = packet.ReadInt();

        for (var skill = 0; skill < skills; skill++) {
            packet.ReadShort();
        }

        Assert.False(packet.HasDataRemaining());
    }

    private static void ReadCommon(FlashIncomingPacket packet, int id, string name, int kind)
    {
        Assert.Equal(id, packet.ReadInt());
        Assert.Equal(name, packet.ReadString());
        packet.ReadString();
        packet.ReadString();
        packet.ReadInt();
        packet.ReadInt();
        packet.ReadInt();
        packet.ReadString();
        packet.ReadInt();
        Assert.Equal(kind, packet.ReadInt());
    }

    private static FlashIncomingPacket Read(IServerPacket composer)
    {
        using var stream = PlusMemoryStream.GetStream();
        composer.Compose(new FlashOutgoingPacket(stream));

        return new() { Buffer = stream.ToArray()[6..] };
    }
}
