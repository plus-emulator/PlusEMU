using System.Collections.Concurrent;
using System.Reflection;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Rooms.Furni;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private Item PlacedRecolorable(uint id, string stored = "", ItemDefinition? definition = null, bool temporary = false)
    {
        definition ??= RecolorableFurniTests.Block();
        var item = new Item { Id = id, OwnerId = 7, IsTemporary = temporary, Definition = definition, ExtraData = FurniExtraData.Load(definition, stored, keepLegacy: true) };

        if (temporary) {
            // Wired places temporary furni outside SetFloorItem.
            var floor = (ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling)
                .GetField("_floorItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_room.GetRoomItemHandler())!;
            Assert.True(floor.TryAdd(id, item));
        }
        else {
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, item, 1, 1, 0, true, false, false));
        }

        return item;
    }

    // ~recolorable_furni.color.rgb (~146) has a value and can be written. Its red (~140), green (~141) and blue (~142) parts only have a value.
    [Fact]
    public void RecolorableColourVariablesReadTheFurniColour()
    {
        var item = PlacedRecolorable(220, "state\t2\nr\t18\ng\t171\nb\t127");
        var other = Furni(221, InteractionType.None, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, other, 2, 2, 0, true, false, false));
        var holder = WiredVariableRuntimeFrames.FurniHolder(item);
        var otherHolder = WiredVariableRuntimeFrames.FurniHolder(other);
        var frame = new WiredVariableFrame(RoomId, [holder, otherHolder]);
        var builtins = new RoomWiredBuiltinVariables(_room);
        WiredVariableValue? Read(WiredVariableHolder target, string key) => builtins.Read(new(target.Target, "internal:" + key), target, frame);

        Assert.Equal(0x12AB7F, Read(holder, "~recolorable_furni.color.rgb")?.Value);
        Assert.Equal(18, Read(holder, "~recolorable_furni.color.rgb.red")?.Value);
        Assert.Equal(171, Read(holder, "~recolorable_furni.color.rgb.green")?.Value);
        Assert.Equal(127, Read(holder, "~recolorable_furni.color.rgb.blue")?.Value);
        Assert.Equal(2, Read(holder, "@state")?.Value);
        Assert.Null(Read(otherHolder, "~recolorable_furni.color.rgb"));

        foreach (var key in new[] { "~recolorable_furni.color.rgb", "~recolorable_furni.color.rgb.red", "~recolorable_furni.color.rgb.green", "~recolorable_furni.color.rgb.blue" }) {
            Assert.True(RoomWiredBuiltinVariables.HasNumericValue(new(WiredVariableTarget.Furni, "internal:" + key)));
            Assert.Equal(key == "~recolorable_furni.color.rgb",
                RoomWiredBuiltinVariables.SupportsNumericWrite(new(WiredVariableTarget.Furni, "internal:" + key)));
        }
    }

    [Fact]
    public void ABuildersClubRecolorableRefusesAnRgbWriteSilentlyAndKeepsItsColour()
    {
        // bc_block_0_recolorable refuses 0xFF8000, +1, -1, 0x1000000, 0 and 0x00FF00 without an error, keeps white and still reads rgb 0xFFFFFF with its parts.
        Viewer();
        var item = PlacedRecolorable(221);
        var holder = WiredVariableRuntimeFrames.FurniHolder(item);
        var frame = new WiredVariableFrame(RoomId, [holder]);
        var builtins = new RoomWiredBuiltinVariables(_room);
        var rgb = new WiredVariableReference(holder.Target, "internal:~recolorable_furni.color.rgb");
        var before = builtins.Read(rgb, holder, frame)?.Value;
        _client.Packets.Clear();

        Assert.False(builtins.Write(rgb, holder, 0x3366CC, frame));
        Assert.Equal(before, builtins.Read(rgb, holder, frame)?.Value);
        Assert.Empty(_client.Packets);
    }

    [Fact]
    public void WritingTheRgbVariableRecoloursANonClubFurniAndSendsItsMap()
    {
        Viewer();
        var definition = RecolorableFurniTests.Block();
        definition.ItemName = "test_block_recolorable";
        var item = PlacedRecolorable(222, definition: definition);
        item.LegacyDataString = "3";
        var holder = WiredVariableRuntimeFrames.FurniHolder(item);
        var frame = new WiredVariableFrame(RoomId, [holder]);
        var builtins = new RoomWiredBuiltinVariables(_room);
        var rgb = new WiredVariableReference(holder.Target, "internal:~recolorable_furni.color.rgb");
        _client.Packets.Clear();

        Assert.False(builtins.Write(rgb, holder, -1, frame));
        Assert.False(builtins.Write(rgb, holder, 0x1000000, frame));
        Assert.False(builtins.Write(rgb, holder, 0xFFFFFF, frame));
        Assert.False(builtins.Write(new(holder.Target, "internal:~recolorable_furni.color.rgb.red"), holder, 1, frame));
        Assert.Empty(_client.Packets.Where(packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer));

        Assert.True(builtins.Write(rgb, holder, 0x3366CC, frame));

        var data = Assert.IsType<RecolorableDataFormat>(item.ExtraData);
        Assert.Equal((0x3366CC, "3"), (data.Rgb, item.LegacyDataString));
        var update = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer).Body };
        Assert.Equal("222", update.ReadString());
        Assert.Equal((int)FurniDataStructure.Map, update.ReadInt());
        Assert.Equal(4, update.ReadInt());
        Assert.Equal(["r", "51", "b", "204", "g", "102", "state", "3"], Enumerable.Range(0, 8).Select(_ => update.ReadString()).ToArray());
        Assert.Equal(0x3366CC, builtins.Read(rgb, holder, frame)?.Value);
    }

    [Fact]
    public void ARecolorableStateChangeKeepsItsColourOnTheWire()
    {
        Viewer();
        var item = PlacedRecolorable(223, "0,128,255");
        _client.Packets.Clear();

        item.LegacyDataString = "1";
        _room.SendObjectsData([item]);

        var batch = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer).Body };
        Assert.Equal((1, 223, (int)FurniDataStructure.Map, 4), (batch.ReadInt(), batch.ReadInt(), batch.ReadInt(), batch.ReadInt()));
        Assert.Equal(["r", "0", "b", "255", "g", "128", "state", "1"], Enumerable.Range(0, 8).Select(_ => batch.ReadString()).ToArray());
    }

    private static ItemDefinition GrassBlock()
    {
        var definition = RecolorableFurniTests.Block(1);
        definition.Id = 19352;
        definition.SpriteId = 19352;
        definition.ItemName = "bc_block_grass_recolorable";

        return definition;
    }

    private async Task Recolor(uint itemId, int scope, int red, int green, int blue)
    {
        using var stream = PlusMemoryStream.GetStream();
        var output = new FlashOutgoingPacket(stream);
        output.WriteUInteger(itemId);
        output.WriteInteger(scope);
        output.WriteInteger(red);
        output.WriteInteger(green);
        output.WriteInteger(blue);

        await new RecolorFurnitureEvent().Parse(_room, _client, new FlashIncomingPacket { Buffer = stream.ToArray().AsMemory(6) });
    }

    private (uint[] Ids, int Red, int Green, int Blue) RecolorUpdate()
    {
        var input = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.FurniRecolorUpdateComposer).Body };
        Assert.Equal(1, input.ReadInt());
        var ids = Enumerable.Range(0, input.ReadInt()).Select(_ => (uint)input.ReadInt()).ToArray();
        var update = (ids, (int)input.ReadShort(), (int)input.ReadShort(), (int)input.ReadShort());
        Assert.False(input.HasDataRemaining());

        return update;
    }

    private static int Rgb(Item item) => Assert.IsType<RecolorableDataFormat>(item.ExtraData).Rgb;

    [Fact]
    public void RecolourPacketsUseTheInternalRevisionHeaders()
    {
        Assert.Equal(3752u, ClientPacketHeader.RecolorFurnitureEvent);
        Assert.Equal(2132u, ServerPacketHeader.FurniRecolorUpdateComposer);
    }

    [Fact]
    public async Task RecolouringOnlyThisFurniStoresTheColourAndAnswersTheRoom()
    {
        Viewer();
        var target = PlacedRecolorable(230);
        var other = PlacedRecolorable(231);
        target.LegacyDataString = "2";
        _client.Packets.Clear();

        await Recolor(230, 0, 0, 133, 255);

        Assert.Equal((0x0085FF, 0xFFFFFF, "2"), (Rgb(target), Rgb(other), target.LegacyDataString));
        var (ids, red, green, blue) = RecolorUpdate();
        Assert.Equal([230u], ids);
        Assert.Equal((0, 133, 255), (red, green, blue));
        Assert.Equal("state\t2\nr\t0\ng\t133\nb\t255", target.ExtraData.Serialize());
    }

    // furniture.recolor.scope.1-3: same colour, same type, same type and colour. A furni whose palette lacks the colour stays.
    [Theory]
    [InlineData(1, new uint[] { 240, 241, 243 })]
    [InlineData(2, new uint[] { 240, 241, 242 })]
    [InlineData(3, new uint[] { 240, 241 })]
    public async Task ScopesSelectTheRoomsRecolorablesLikeTheirMenuSays(int scope, uint[] expected)
    {
        Viewer();
        var target = PlacedRecolorable(240, "1,2,3");
        PlacedRecolorable(241, "1,2,3");
        PlacedRecolorable(242, "9,9,9");
        var other = PlacedRecolorable(243, "1,2,3", RecolorableFurniTests.Block());
        other.Definition.Id = 19349;
        other.Definition.ItemName = "bc_block_1_recolorable";
        var grass = PlacedRecolorable(244, "1,2,3", GrassBlock());
        _client.Packets.Clear();

        await Recolor(target.Id, scope, 0, 133, 255);

        Assert.Equal(expected, RecolorUpdate().Ids);

        foreach (var item in _room.GetRoomItemHandler().GetFloor) {
            Assert.Equal(expected.Contains(item.Id) ? 0x0085FF : item.Id == 242 ? 0x090909 : 0x010203, Rgb(item));
        }

        Assert.Equal(0x010203, Rgb(grass));
    }

    [Theory]
    [InlineData(0, 1, 2, 3)]
    [InlineData(0, 256, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(4, 255, 255, 255)]
    [InlineData(-1, 255, 255, 255)]
    public async Task ColoursOutsideThePaletteAndUnknownScopesAreRefused(int scope, int red, int green, int blue)
    {
        Viewer();
        var target = PlacedRecolorable(250, "0,133,255");
        _client.Packets.Clear();

        await Recolor(250, scope, red, green, blue);

        Assert.Equal(0x0085FF, Rgb(target));
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.FurniRecolorUpdateComposer);
    }

    [Fact]
    public async Task AGrassBlockOnlyTakesTheGrassPalette()
    {
        Viewer();
        var grass = PlacedRecolorable(255, "", GrassBlock());
        _client.Packets.Clear();

        await Recolor(255, 0, 255, 255, 255);
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.FurniRecolorUpdateComposer);

        await Recolor(255, 0, 0x6A, 0xB1, 0x8E);
        Assert.Equal(0x6AB18E, Rgb(grass));
    }

    // Rights to recolour: owner, rights holders and room controllers.
    [Fact]
    public async Task UsersWithoutRightsCannotRecolour()
    {
        Viewer();
        var target = PlacedRecolorable(260);
        _client.GetHabbo().Username = "johndoe";
        _room.UsersWithRights = [];
        _client.Packets.Clear();

        await Recolor(260, 0, 0, 133, 255);
        Assert.Equal(0xFFFFFF, Rgb(target));

        _room.UsersWithRights.Add(_client.GetHabbo().Id);
        await Recolor(260, 0, 0, 133, 255);
        Assert.Equal(0x0085FF, Rgb(target));
    }

    [Fact]
    public async Task TemporaryAndNonRecolorableFurniAreLeftAlone()
    {
        Viewer();
        var temporary = PlacedRecolorable(270, temporary: true);
        var plain = Furni(271, InteractionType.None, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, plain, 2, 2, 0, true, false, false));
        _client.Packets.Clear();

        await Recolor(270, 0, 0, 133, 255);
        await Recolor(271, 0, 0, 133, 255);

        Assert.Equal(0xFFFFFF, Rgb(temporary));
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.FurniRecolorUpdateComposer);
    }

    [Fact]
    public void RecolourUpdateWritesAirsRecolorUpdateData()
    {
        using var stream = PlusMemoryStream.GetStream();
        var output = new FlashOutgoingPacket(stream);
        new FurniRecolorUpdateComposer([new FurniRecolorUpdate([5, 6], 1, 2, 3), new FurniRecolorUpdate([], 255, 0, 128)]).Compose(output);
        var input = new FlashIncomingPacket { Buffer = stream.ToArray().AsMemory(6) };

        Assert.Equal((2, 2, 5, 6), (input.ReadInt(), input.ReadInt(), input.ReadInt(), input.ReadInt()));
        Assert.Equal((1, 2, 3), ((int)input.ReadShort(), (int)input.ReadShort(), (int)input.ReadShort()));
        Assert.Equal(0, input.ReadInt());
        Assert.Equal((255, 0, 128), ((int)input.ReadShort(), (int)input.ReadShort(), (int)input.ReadShort()));
        Assert.False(input.HasDataRemaining());
    }
}
