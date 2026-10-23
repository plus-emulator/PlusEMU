using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private IWiredConfiguredItem PlaceSelector(WiredComponent wired, string name)
    {
        var box = WiredBox(601, name, 1, 1);
        box.Item.ExtraData = new LegacyDataFormat { Data = "0" };
        Assert.True(wired.AddBox(box));

        return box;
    }

    private IWiredConfiguredItem SaveSelector(IWiredConfiguredItem box, bool filter, bool invert = false)
    {
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor) with { Filter = filter, Inverse = invert };
        Assert.True(WiredNativeEditorProjection.TryCompile(box.Item.Id, box.Descriptor, native, out var runtime));
        CanonicalService(new CanonicalStore(null)).Save(_client, new(box.Item.Id, WiredBoxCategory.Selector, runtime, native));
        Assert.Equal((filter, invert), (box.Configuration.Origin!.Native!.Filter, box.Configuration.Origin.Native.Inverse));

        return box;
    }

    // Every save of a selector is followed by ObjectDataUpdate (374) with its resting state, 3 (green) with "Filter existing selection" and 0 (orange) without. Invert has no colour of its own.
    [Theory]
    [InlineData(true, false, "3")]
    [InlineData(false, false, "0")]
    [InlineData(false, true, "0")]
    [InlineData(true, true, "3")]
    public void SavingASelectorShowsItsRestingStateAtOnce(bool filter, bool invert, string state)
    {
        var (wired, _) = PrepareSpeech(new SpeechClock());
        var box = PlaceSelector(wired, "wf_slc_users_bytype");
        // A box that fired before the save shows a flash state.
        box.Item.LegacyDataString = filter ? "2" : "5";
        wired.FlushFlashes();
        _client.Packets.Clear();

        SaveSelector(box, filter, invert);

        Assert.Equal(state, box.Item.LegacyDataString);
        var update = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer).Body };
        Assert.Equal("601", update.ReadString());
        Assert.Equal(0, update.ReadInt());
        Assert.Equal(state, update.ReadString());

        // A save that keeps the resting state still sends it.
        _client.Packets.Clear();
        SaveSelector(box, filter, invert);
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer);
    }

    [Fact]
    public void UntickingFilterReturnsASelectorToOrange()
    {
        var (wired, _) = PrepareSpeech(new SpeechClock());
        var box = PlaceSelector(wired, "wf_slc_furni_bytype");

        Assert.Equal("3", SaveSelector(box, true).Item.LegacyDataString);
        Assert.Equal("4", WiredFlashStates.Next(box.Item.LegacyDataString, box));
        Assert.Equal("0", SaveSelector(box, false).Item.LegacyDataString);
        Assert.Equal("1", WiredFlashStates.Next(box.Item.LegacyDataString, box));
    }

    // Room entry lists each selector in its last state, a flash state included (a fired filter box "5", a plain one "1" or "2").
    [Theory]
    [InlineData(true, "5")]
    [InlineData(false, "1")]
    [InlineData(false, "2")]
    public void ObjectListsCarryTheLastStateOfASelector(bool filter, string flash)
    {
        var (wired, _) = PrepareSpeech(new SpeechClock());
        var box = PlaceSelector(wired, "wf_slc_users_bytype");
        SaveSelector(box, filter);
        box.Item.LegacyDataString = flash;

        var snapshot = Assert.Single(RoomFurnitureSnapshot.Capture([box.Item], 7, "johndoe").Items);

        Assert.Equal(new FurnitureDataSnapshot.Legacy(flash), snapshot.Data);
    }
}
