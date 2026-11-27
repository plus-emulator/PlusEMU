using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Says-something trigger, Furni By Type (arrows), Furni In Neighborhood of a picked stack magic tile filtering the arrows,
// Filter Furni 1 and Furni To User, every box saved through the client's raw packets.
public partial class PlacedFurniRoomTests
{
    private const uint NbTrigger = 501, NbByType = 502, NbNeighborhood = 503, NbFilter = 504, NbAction = 505, NbMagic = 510;

    [Theory]
    [InlineData(1, 2, 2, 1, 1)]
    [InlineData(2, 2, 2, 1, 1)]
    [InlineData(2, 3, 3, 1, 0)]
    [InlineData(2, 3, 3, 387, 1)]
    [InlineData(1, 2, 2, 510, 0)]
    [InlineData(1, 2, 2, 0, 0)]
    public async Task NeighborhoodOfAMagicTileCountsFromItsRootTile(int size, int arrowX, int arrowY, int firstWord, int moved)
    {
        var clock = new SpeechClock();
        var (wired, owner) = PrepareSpeech(clock);
        Stand(owner, 0, 3);

        var magic = Furni(NbMagic, InteractionType.Stacktool, WiredBoxType.None);
        magic.Definition.Id = 7001;
        magic.Definition.Width = size;
        magic.Definition.Length = size;
        magic.Definition.Height = 0;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, magic, 2, 2, 0, true, false, false));

        var arrowDefinition = Furni(0, InteractionType.None, WiredBoxType.None).Definition;
        arrowDefinition.Id = 7000;
        arrowDefinition.Height = 0.1;
        Item Arrow(uint id, int x, int y)
        {
            var arrow = Furni(id, InteractionType.None, WiredBoxType.None);
            arrow.Definition = arrowDefinition;
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, arrow, x, y, 0, true, false, false));

            return arrow;
        }

        var onTile = new[] { Arrow(601, arrowX, arrowY), Arrow(602, arrowX, arrowY) };
        var away = Arrow(603, 3, 0);

        var service = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(),
            TestLogging.For<WiredConfigurationService>());
        AddSpeechBox(NbTrigger, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "go" }, 0);
        PlaceUnsaved(NbByType, "wf_slc_furni_bytype", 1);
        PlaceUnsaved(NbNeighborhood, "wf_slc_furni_neighborhood", 2);
        PlaceUnsaved(NbFilter, "wf_xtra_filter_furni", 3);
        PlaceUnsaved(NbAction, "wf_act_furni_to_user", 4);
        _client.Packets.Clear();

        // Furni By Type: [match state 0], picks the away arrow, Filter off, Invert off, furni source 100.
        await new SaveWiredSelectorConfigEvent(service).Parse(_client,
            ClientPacket((int)NbByType, 1, 0, "", 1, (int)away.Id, false, false, 1, 100, 0, 0, 0));
        // Neighborhood as the editor saves it: [furni anchor 0, root 0, 0, 14 mask words with only the centre bit], the magic tile picked,
        // Filter on, Invert off, furni source 100, user source 0.
        var neighborhood = new List<object> { (int)NbNeighborhood, 17, 0, 0, 0, firstWord };
        neighborhood.AddRange(Enumerable.Repeat((object)0, 13));
        neighborhood.AddRange(["", 1, (int)NbMagic, true, false, 1, 100, 1, 0, 0, 0]);
        await new SaveWiredSelectorConfigEvent(service).Parse(_client, ClientPacket([.. neighborhood]));
        // Filter Furni: amount 1, literal, target 1, no variable.
        await new SaveWiredAddonConfigEvent(service).Parse(_client,
            ClientPacket((int)NbFilter, 3, 1, 0, 1, "", 0, 1, 0, 1, 0, 1, "n", 0));
        // Furni To User: no picks, no delay, furni from the selector (200), the triggering user (0).
        await new SaveWiredEffectConfigEvent(service).Parse(_client,
            ClientPacket((int)NbAction, 0, "", 0, 0, 1, 200, 1, 0, 0, 0));
        Assert.Empty(_client.Packets.Where(packet => packet.Header == ServerPacketHeader.WiredValidationErrorComposer));
        Assert.Equal(4, _client.Packets.Count(packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer));

        owner.OnChat(0, "go", false);
        Advance(wired, clock, 1000);

        Assert.Equal(moved, onTile.Count(arrow => (arrow.GetX, arrow.GetY) == (0, 3)));
        Assert.Equal((3, 0), (away.GetX, away.GetY));
    }
}
