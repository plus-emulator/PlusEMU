using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private static Fixture ResumeFixture(string counter)
    {
        string Config(int[] parameters, string text = "", params uint[] selected) => JsonSerializer.Serialize(new WiredConfiguration
        {
            IntParams = [.. parameters],
            Text = text,
            SelectedItems = [.. selected]
        });
        var f = new Fixture(
        [
            (CounterId, 8, 12, 0.0, 0, counter, null),
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", Config([1, 0, 0], "cstart")),
            (22, 4, 12, 0.65, 0, "wf_act_control_clock", Config([0, 100], "", CounterId)),
            (23, 5, 12, 0.0, 0, "wf_trg_says_something", Config([1, 0, 0], "cpause")),
            (24, 5, 12, 0.65, 0, "wf_act_control_clock", Config([3, 100], "", CounterId)),
            (25, 6, 12, 0.0, 0, "wf_trg_says_something", Config([1, 0, 0], "cfire")),
            (26, 6, 12, 0.65, 0, "wf_act_control_clock", Config([0, 100], "", CounterId))
        ], live: true);
        f.MovementContext().Room.GetWired().AttachRoomItem(f.Items[CounterId]);
        f.Room.OwnerId = 1;
        f.Room.OwnerName = "user1";
        f.Room.Type = "private";

        return f;
    }

    // Saves box 26 the way the client does: item, operation, text, picks, delay, furni sources, user sources, variables, secondary picks.
    private static void SaveFiredOperation(Fixture f, RoomUser owner, int operation)
    {
        var service = new WiredConfigurationService(new SavedConfigurations([]), null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());
        f.Owned(() => new SaveWiredEffectConfigEvent(service).Parse(owner.GetClient()!,
            EditorTestSupport.Incoming(26, 1, operation, "", 1, CounterId, 0, 1, 100, 0, 0, 0)).GetAwaiter().GetResult());
    }

    private static (uint Id, string Text)[] CounterData(List<(uint Header, byte[] Payload)> frames) =>
    [
        .. frames.Where(frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer).Select(ReadSingle).Where(entry => entry.Id == CounterId),
        .. frames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer).SelectMany(CounterEntries)
    ];

    // A Control Clock saved with operation 5 reopens with 4, and firing it on a game counter that never ran flashes only the
    // boxes. The counter stays at 0.
    [Fact]
    public void AStoredResumeDoesNotStartACounterThatNeverRan()
    {
        var f = ResumeFixture("wf_game_upcounter1");
        var owner = f.Walker(1, 4, 10, 4, 11);
        var frames = CaptureFrames(owner);

        SaveFiredOperation(f, owner, 5);
        Assert.Contains(frames, frame => frame.Header == ServerPacketHeader.HideWiredConfigComposer);
        f.Owned(() => owner.OnChat(0, "cfire", false));
        f.Advance(3500);
        f.Owned(() => owner.OnChat(0, "cfire", false));
        f.Advance(2500);

        Assert.Empty(CounterData(frames));
        Assert.Empty(ObjectUpdates(frames));
        Assert.Equal("", f.Items[CounterId].LegacyDataString);
    }

    [Fact]
    public void AStoredResumeContinuesAPausedCounter()
    {
        var f = ResumeFixture("wf_upcounter1");
        var owner = f.Walker(1, 4, 10, 4, 11);
        SaveFiredOperation(f, owner, 4);
        var counter = f.Items[CounterId];

        f.Owned(() => owner.OnChat(0, "cstart", false));
        f.Advance(2600);
        f.Owned(() => owner.OnChat(0, "cpause", false));
        f.Advance(2000);
        var paused = counter.LegacyDataString;
        var frames = CaptureFrames(owner);
        f.Owned(() => owner.OnChat(0, "cfire", false));
        f.Advance(2600);

        Assert.Equal("2", paused);
        Assert.Equal(["3", "4"], CounterData(frames).Select(entry => entry.Text).Take(2));
    }
}
