using System.Text;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Using an antenna by hand (UseFurniture) sends its signal: every Receive Signal stack that picks this antenna runs once,
// with the user as the triggering user. The signal carries no users. A user without rights changes nothing.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData("wf_antenna1")]
    [InlineData("wf_antenna2")]
    public async Task UsingAnAntennaFiresItsReceiveSignalStacksForTheUser(string name)
    {
        var antenna = Antenna(40, name, 1, 1);
        var other = Antenna(41, name, 2, 2);
        Receiver(110, 0, [antenna], "got signal");
        Receiver(112, 1, [antenna, other], "got either");
        Receiver(114, 2, [other], "got other");
        Viewer(3, 3);

        await UseByPacket(antenna);

        Assert.Equal("1", antenna.LegacyDataString);
        Assert.Equal("0", other.LegacyDataString);
        Assert.Equal(["got either", "got signal"], WhisperTexts().Order());
    }

    [Fact]
    public async Task EveryUseOfAnAntennaSendsOneSignal()
    {
        var antenna = Antenna(40, "wf_antenna2", 1, 1);
        Receiver(110, 0, [antenna], "got signal");
        Viewer(3, 3);

        await UseByPacket(antenna);
        await UseByPacket(antenna);
        await UseByPacket(antenna);

        Assert.Equal("1", antenna.LegacyDataString);
        Assert.Equal(["got signal", "got signal", "got signal"], WhisperTexts());
    }

    [Fact]
    public async Task ManualSignalCarriesNoUsers()
    {
        var antenna = Antenna(40, "wf_antenna2", 1, 1);
        Receiver(110, 0, [antenna], "got signal");
        var signalUsers = WiredBox(112, "wf_act_show_message", 0, 0);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(signalUsers,
            new() { IntParams = [WiredSources.Signal, 0, 34, -1], Text = "signal user" }, out var config, out var error), error);
        signalUsers.ApplyConfiguration(config);
        Assert.True(_room.GetWired().AddBox(signalUsers));
        Viewer(3, 3);

        await UseByPacket(antenna);

        Assert.Equal(["got signal"], WhisperTexts());
    }

    [Fact]
    public async Task UsingAnAntennaWithoutRightsSendsNoSignal()
    {
        var antenna = Antenna(40, "wf_antenna2", 1, 1);
        Receiver(110, 0, [antenna], "got signal");
        Viewer(3, 3);
        _room.OwnerName = "someone else";
        _room.OwnerId = 8;
        _room.UsersWithRights = [];

        await UseByPacket(antenna);

        Assert.Equal("0", antenna.LegacyDataString);
        Assert.Empty(WhisperTexts());
    }

    private Item Antenna(uint id, string name, int x, int y)
    {
        var item = Furni(id, InteractionType.None, WiredBoxType.None);
        item.Definition.ItemName = name;
        item.Definition.InteractionName = "antenna";
        item.Definition.Modes = 2;
        InitializeNativeState(item);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, x, y, 0, true, false, false));

        return item;
    }

    // A Receive Signal stack on (x, 0) that whispers `text` to its triggering user.
    private void Receiver(uint id, int x, Item[] antennas, string text)
    {
        var trigger = WiredBox(id, "wf_trg_recv_signal", x, 0);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(trigger,
            new() { IntParams = [0, 100], SelectedItems = [.. antennas.Select(antenna => antenna.Id)] }, out var config, out var error), error);
        trigger.ApplyConfiguration(config);
        Assert.True(_room.GetWired().AddBox(trigger));
        var whisper = WiredBox(id + 1, "wf_act_show_message", x, 0);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(whisper, new() { IntParams = [0, 0, 34, -1], Text = text }, out config, out error), error);
        whisper.ApplyConfiguration(config);
        Assert.True(_room.GetWired().AddBox(whisper));
    }

    private async Task UseByPacket(Item item)
    {
        await new UseFurnitureEvent(new FurnitureUseService(null!, TestItemRuntime.Quests)).Parse(_room, _client, ClientPacket((int)item.Id, 0));

        for (var pass = 0; pass < 3; pass++) {
            _room.GetWired().OnFastCycle();
            _room.GetWired().OnCycle();
        }
    }

    private string[] WhisperTexts() => _client.Packets
        .Where(packet => packet.Header == ServerPacketHeader.WhisperComposer)
        .Select(packet => WhisperText(packet.Body))
        .ToArray();

    private static string WhisperText(byte[] body)
    {
        var length = (body[4] << 8) | body[5];

        return Encoding.UTF8.GetString(body, 6, length);
    }
}
