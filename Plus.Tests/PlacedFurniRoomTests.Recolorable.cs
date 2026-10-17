using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private Item PlacedRecolorable(uint id, string stored = "")
    {
        var definition = RecolorableFurniTests.Block();
        var item = new Item { Id = id, OwnerId = 7, Definition = definition, ExtraData = FurniExtraData.Load(definition, stored, keepLegacy: true) };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, item, 1, 1, 0, true, false, false));

        return item;
    }

    // Observed on hhus (AIR WIN63-202610131318-860371703): ~recolorable_furni.color.rgb (~146) has a value and can be written; its red
    // (~140), green (~141) and blue (~142) parts only have a value.
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
    public void WritingTheRgbVariableRecoloursTheFurniAndSendsItsMap()
    {
        Viewer();
        var item = PlacedRecolorable(222);
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
}
