using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing.Rooms.Permissions;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public class RoomEntryPacketLayoutTests
{
    [Fact]
    public void OpenConnectionCarriesTheFlatId()
    {
        var packet = Read(new OpenConnectionComposer(4521));

        Assert.Equal(4521, packet.ReadInt());
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void YouAreOwnerCarriesTheFlatId()
    {
        var packet = Read(new YouAreOwnerComposer(4521));

        Assert.Equal(4521, packet.ReadInt());
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void WiredEnvironmentEndsAtTheEnabledAchievements()
    {
        // Click user wired, n, achievement x n.
        var packet = Read(new WiredEnvironmentComposer(true, ["ACH_RoomEntry"]));

        Assert.True(packet.ReadBool());
        Assert.Equal(1, packet.ReadInt());
        Assert.Equal("ACH_RoomEntry", packet.ReadString());
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void WiredPermissionsEndsAtCanRead()
    {
        // canModify, then canRead, and nothing after them.
        var packet = Read(new WiredPermissionsComposer(canModify: true, canRead: false));

        Assert.True(packet.ReadBool());
        Assert.False(packet.ReadBool());
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void UserChangeEndsAtTheBadgesRank()
    {
        // id, figure, sex, motto, achievement score, string, n x (int, int, int), badges rank.
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
        // Common fields, then the branch for the user type, and nothing after it.
        var habbo = new RoomUserSnapshot(7, "Dennis", "hi", "hd-180-1", 1, 2, 3, "1.5", 2, 1, "m", 9, "Club", 12,
            false, 0, 0, "", false, false);
        var pet = new RoomUserSnapshot(10, "Rex", "", "0 0 ffffff", 2, 4, 5, "0", 0, 2, "", 0, "", 0,
            true, 13, 7, "Dennis", true, false, 6);
        // A generic bot lists skills 1-5 and 19-23.
        var bot = new RoomUserSnapshot(11, "Frank", "hello", "hd-180-1", 3, 6, 7, "0", 0, 4, "f", 0, "", 0,
            false, 0, 7, "Dennis", false, false, BotSkills: [1, 2, 3, 4, 5, 19, 20, 21, 22, 23]);
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
        var skills = new short[packet.ReadInt()];

        for (var skill = 0; skill < skills.Length; skill++) {
            skills[skill] = packet.ReadShort();
        }

        Assert.Equal(new short[] { 1, 2, 3, 4, 5, 19, 20, 21, 22, 23 }, skills);

        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void ObjectAddEndsAtTheOwnerNameAfterTheFloorEntry()
    {
        // id, type, x, y, dir, z, sizeZ, extra, stuff data, expiry, usage, owner (the static class only when type is negative), then the owner name.
        var packet = Read(new ObjectAddComposer(FloorItem()));

        ReadFloorEntry(packet);
        Assert.Equal("Dennis", packet.ReadString());
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void ObjectsEndsAtTheFloorEntryWithNoMetadataInts()
    {
        // Owner map, count, then the object data per entry.
        var packet = Read(new ObjectsComposer(new RoomFurnitureSnapshot([FloorItem()], [])));

        Assert.Equal(0, packet.ReadInt());
        Assert.Equal(1, packet.ReadInt());
        ReadFloorEntry(packet);
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void ItemAddEndsAtTheOwnerNameAfterTheWallEntry()
    {
        // id string, type, location, data, expiry, usage, owner, then the owner name.
        var packet = Read(new ItemAddComposer(WallItem()));

        ReadWallEntry(packet);
        Assert.Equal("Dennis", packet.ReadString());
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void ItemsEndsAtTheWallEntryWithNoMetadataInts()
    {
        // Owner map, count, then the item data per entry.
        var packet = Read(new ItemsComposer(new RoomFurnitureSnapshot([WallItem()], [])));

        Assert.Equal(0, packet.ReadInt());
        Assert.Equal(1, packet.ReadInt());
        ReadWallEntry(packet);
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void FurnitureMetadataBatchCarriesSevenValuesPerItemId()
    {
        // Plus-only message (header 5120): count, then per item id stackable, isSeat, isBed, walkable, width, length, teleport id.
        var packet = Read(new FurnitureMetadataComposer([FloorItem()]));

        Assert.Equal(1, packet.ReadInt());
        Assert.Equal(7, packet.ReadInt());
        Assert.Equal(1, packet.ReadInt());
        Assert.Equal(0, packet.ReadInt());
        Assert.Equal(0, packet.ReadInt());
        Assert.Equal(0, packet.ReadInt());
        Assert.Equal(2, packet.ReadInt());
        Assert.Equal(1, packet.ReadInt());
        Assert.Equal(0, packet.ReadInt());
        Assert.False(packet.HasDataRemaining());
    }

    private static RoomItemSnapshot FloorItem() => new(7, 1234, 1, 2, 0, "0.5", "1.0", 0, new FurnitureDataSnapshot.Empty(),
        0, 0, "", "", 0, 9, "Dennis", new FurnitureMetadata(true, false, false, false, 2, 1));

    private static RoomItemSnapshot WallItem() => new(8, 1234, 0, 0, 0, "0", "0", 0, new FurnitureDataSnapshot.Empty(),
        0, 0, ":w=2,1 l=3,4 r", "1", 0, 9, "Dennis", new FurnitureMetadata(false, false, false, false, 0, 0));

    private static void ReadFloorEntry(FlashIncomingPacket packet)
    {
        Assert.Equal(7, packet.ReadInt());
        Assert.Equal(1234, packet.ReadInt());
        Assert.Equal(1, packet.ReadInt());
        Assert.Equal(2, packet.ReadInt());
        Assert.Equal(0, packet.ReadInt());
        Assert.Equal("0.5", packet.ReadString());
        Assert.Equal("1.0", packet.ReadString());
        Assert.Equal(0, packet.ReadInt());
        Assert.Equal((int)FurniDataStructure.Empty, packet.ReadInt());
        Assert.Equal(-1, packet.ReadInt());
        Assert.Equal(0, packet.ReadInt());
        Assert.Equal(9, packet.ReadInt());
    }

    private static void ReadWallEntry(FlashIncomingPacket packet)
    {
        Assert.Equal("8", packet.ReadString());
        Assert.Equal(1234, packet.ReadInt());
        Assert.Equal(":w=2,1 l=3,4 r", packet.ReadString());
        Assert.Equal("1", packet.ReadString());
        Assert.Equal(-1, packet.ReadInt());
        Assert.Equal(0, packet.ReadInt());
        Assert.Equal(9, packet.ReadInt());
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
