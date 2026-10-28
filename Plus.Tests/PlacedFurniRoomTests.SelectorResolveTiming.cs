using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// Selector rules of a stack, driven by a real speech trigger and the room cycle, with every box saved from its raw editor packet:
/// a selector is read once when the stack fires, Furni By Type with Match State compares the live states, and a Furni In Area selector
/// sees a sliding furni at its destination tile.
/// </summary>
public partial class PlacedFurniRoomTests
{
    private const uint SlTrigger = 701, SlBase = 1000;
    private const int SlSelectorSource = 200, SlFurniSelectorSource = 200, SlPickedSource = 100;
    private uint _slNext = SlBase;

    private WiredConfigurationService SlService() => _dpService ??= new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!,
        new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());

    private async Task SlSave(WiredBoxCategory category, string name, int x, int y, double z, int[] ints, string text, uint[] picks,
        int delay, int[] furni, int[] users)
    {
        var id = _slNext++;
        PlaceSavedBox(id, name, x, y, z);
        List<object> body = [(int)id, ints.Length, .. ints.Cast<object>(), text, picks.Length, .. picks.Select(pick => (object)(int)pick)];

        if (category == WiredBoxCategory.Action) {
            body.Add(delay);
        }

        if (category == WiredBoxCategory.Selector) {
            body.AddRange([false, false]);
        }

        body.AddRange([furni.Length, .. furni.Cast<object>(), users.Length, .. users.Cast<object>(), 0, 0]);
        _client.Packets.Clear();
        IPacketEvent handler = category == WiredBoxCategory.Selector
            ? new SaveWiredSelectorConfigEvent(SlService()) : new SaveWiredEffectConfigEvent(SlService());
        await handler.Parse(_client, ClientPacket([.. body]));
        Assert.True(_client.Packets.Any(packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer), $"{name} was not saved");
    }

    private (WiredComponent Wired, RoomUser Owner, SpeechClock Clock) SlRoom(string word = "go", int x = 1, int y = 1)
    {
        var clock = new SpeechClock();
        var (wired, owner) = PrepareSpeech(clock);
        AddSpeechBox(SlTrigger + (uint)(x * 10 + y), "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = word }, 0, x, y);

        return (wired, owner, clock);
    }

    private Item SlFurni(uint id, int x, int y, uint definition, string state, int modes = 4)
    {
        var item = Furni(id, InteractionType.None, WiredBoxType.None);
        item.Definition.Id = definition;
        item.Definition.Modes = modes;
        item.ExtraData = new LegacyDataFormat { Data = state };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, x, y, 0, true, false, false));

        return item;
    }

    private static void SlStep(WiredComponent wired, SpeechClock clock, int milliseconds)
    {
        for (var passed = 0; passed < milliseconds; passed += 50) {
            clock.Now = clock.Now.AddMilliseconds(50);
            wired.OnFastCycle();
        }
    }

    // Users In Area holds the owner when the stack fires. Teleport To moves the owner out of the area, and both Show Message boxes (at once and
    // after 4 pulses) still reach the owner, wherever the selector stands in the stack.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task AUsersSelectorIsReadOnceWhenTheStackFiresAndKeptForEveryAction(int selectorPosition)
    {
        var (wired, owner, clock) = SlRoom();
        var target = SlFurni(900, 3, 3, 1, "0");
        Assert.True(owner.X < 2 && owner.Y < 2);
        double z = 1;

        async Task Selector() => await SlSave(WiredBoxCategory.Selector, "wf_slc_users_area", 1, 1, z++, [0, 0, 2, 2], "", [], 0, [], []);
        async Task Teleport() => await SlSave(WiredBoxCategory.Action, "wf_act_teleport_to", 1, 1, z++, [0], "", [target.Id], 0,
            [SlPickedSource], [SlSelectorSource]);
        async Task Show(string text, int delay) => await SlSave(WiredBoxCategory.Action, "wf_act_show_message", 1, 1, z++, [0, 34, -1], text, [],
            delay, [], [SlSelectorSource]);

        if (selectorPosition == 0) {
            await Selector();
        }

        await Teleport();

        if (selectorPosition == 1) {
            await Selector();
        }

        await Show("now", 0);
        await Show("late", 4);

        if (selectorPosition == 2) {
            await Selector();
        }

        _client.Packets.Clear();
        owner.OnChat(0, "go", false);
        SlStep(wired, clock, 3500);

        Assert.True(owner.X >= 2 || owner.Y >= 2);
        Assert.Equal(["now", "late"], SpeechMessages(ServerPacketHeader.WhisperComposer).Order().Reverse());
    }

    // Match State: every furni of a picked furni's class whose current state equals the current state of any picked furni.
    [Fact]
    public async Task FurniByTypeWithMatchStateSelectsTheClassByTheStatesOfAllPicks()
    {
        var (wired, owner, clock) = SlRoom();
        var picks = SlStates(out var others);
        await SlSave(WiredBoxCategory.Selector, "wf_slc_furni_bytype", 1, 1, 1, [1], "", [picks[0].Id, picks[1].Id], 0, [SlPickedSource], []);
        await SlSave(WiredBoxCategory.Action, "wf_act_toggle_state", 1, 1, 2, [0], "", [], 0, [SlFurniSelectorSource], []);

        owner.OnChat(0, "go", false);
        SlStep(wired, clock, 500);

        Assert.Equal(["1", "3", "1", "3", "0"], others.Select(item => item.LegacyDataString));
    }

    // A pick whose state changed after the box was saved matches by its new state.
    [Fact]
    public async Task FurniByTypeWithMatchStateReadsThePicksStatesWhenTheStackFires()
    {
        var (wired, owner, clock) = SlRoom();
        var picks = SlStates(out var others);
        await SlSave(WiredBoxCategory.Selector, "wf_slc_furni_bytype", 1, 1, 1, [1], "", [picks[0].Id, picks[1].Id], 0, [SlPickedSource], []);
        await SlSave(WiredBoxCategory.Action, "wf_act_toggle_state", 1, 1, 2, [0], "", [], 0, [SlFurniSelectorSource], []);
        picks[0].ExtraData = new LegacyDataFormat { Data = "3" };

        owner.OnChat(0, "go", false);
        SlStep(wired, clock, 500);

        // Picks now hold states 3 and 2: the first, the second and the state-3 tile of the class toggle; the state-0 tiles stay.
        Assert.Equal(["0", "3", "0", "0", "0"], others.Select(item => item.LegacyDataString));
    }

    // Tiles of one class with states 0, 2, 0, 3 (picks: the first two), then a tile of another class with state 0.
    private Item[] SlStates(out Item[] all)
    {
        var tiles = new[] { SlFurni(801, 0, 2, 5, "0"), SlFurni(802, 1, 2, 5, "2"), SlFurni(803, 2, 2, 5, "0"), SlFurni(804, 3, 2, 5, "3"),
            SlFurni(805, 0, 3, 6, "0") };
        all = tiles;

        return tiles;
    }

    // From the moment Move/Rotate runs until its slide ends, a Furni In Area selector sees the furni at the destination tile only.
    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, 120)]
    [InlineData(true, 450)]
    [InlineData(true, 650)]
    [InlineData(false, 0)]
    [InlineData(false, 120)]
    [InlineData(false, 450)]
    [InlineData(false, 650)]
    public async Task AFurniAreaSelectorSeesASlidingFurniAtItsDestination(bool destination, int after)
    {
        var (wired, owner, clock) = SlRoom();
        AddSpeechBox(SlTrigger + 90, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "peek" }, 0, 3, 0);
        var mover = SlFurni(850, 0, 2, 7, "0", modes: 2);
        await SlSave(WiredBoxCategory.Action, "wf_act_move_rotate", 1, 1, 1, [5, 0], "", [mover.Id], 0, [SlPickedSource], []);
        await SlSave(WiredBoxCategory.Selector, "wf_slc_furni_area", 3, 0, 1, [destination ? 1 : 0, 2, 1, 1], "", [], 0, [], []);
        await SlSave(WiredBoxCategory.Action, "wf_act_toggle_state", 3, 0, 2, [0], "", [], 0, [SlFurniSelectorSource], []);

        owner.OnChat(0, "go", false);
        SlStep(wired, clock, 50 + after);
        owner.OnChat(0, "peek", false);
        SlStep(wired, clock, 100);

        Assert.Equal(destination ? "1" : "0", mover.LegacyDataString);
    }
}
