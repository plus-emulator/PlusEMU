using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint SpeedControllerId = 60, SpeedMoverId = 61, SpeedSayId = 62, SpeedToggleId = 63;

    // A placed Roller Speed Controller steps the rollers every 500 ms and shows frame 1 and 2 by turns from the start. Each
    // Toggle State moves it to the first state of the next group (3, 6, 9, 0) and it then shows the two frames after that state.
    // The rollers keep stepping every 500 ms after every toggle.
    [Fact]
    public void ToggleStateMovesTheSpeedControllerToTheNextGroupAndKeepsTheRollerStep()
    {
        var (f, rider, packets) = SpeedControllerRoom();
        var slides = new List<int>();

        SpeedTicks(f, packets, 4, slides);

        for (var toggle = 0; toggle < 4; toggle++) {
            f.Owned(() => rider.OnChat(0, "tg", false));
            SpeedTicks(f, packets, 4, slides);
        }

        Assert.Equal(["1", "2", "1", "2", "3", "4", "5", "4", "5", "6", "7", "8", "7", "8", "9", "10", "11", "10", "11", "0", "1", "2", "1", "2"],
            SpeedControllerTexts(packets));
        Assert.All(slides, count => Assert.Equal(1, count));
    }

    // A use of the controller jumps to the next group and sets the step time of that group, also after a Wired toggle.
    [Fact]
    public void AUseOfTheSpeedControllerAfterAToggleStateSetsTheStepTimeOfItsGroup()
    {
        var (f, rider, packets) = SpeedControllerRoom();
        var slides = new List<int>();
        f.Owned(() => rider.OnChat(0, "tg", false));
        SpeedTicks(f, packets, 2, slides);
        var controller = f.Items[SpeedControllerId];

        f.Owned(() => controller.Interactor.OnTrigger(rider.GetClient(), controller, 0, true));
        slides.Clear();
        SpeedTicks(f, packets, 6, slides);

        Assert.Equal("6", controller.LegacyDataString);
        var steps = slides.Select((count, tick) => (count, tick)).Where(step => step.count > 0).ToArray();
        Assert.Equal(2, steps.Length);
        Assert.All(steps, step => Assert.Equal(1, step.count));
        Assert.Equal(3, steps[1].tick - steps[0].tick);
    }

    // A fresh controller holds an empty state; the rollers turn in a loop, so the mover never leaves them.
    private static (Fixture Fixture, RoomUser Rider, List<byte[]> Packets) SpeedControllerRoom()
    {
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (SpeedSayId, 4, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "tg" })),
            (SpeedToggleId, 4, 9, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [SpeedControllerId], 100)),
            (SpeedControllerId, 9, 9, 0.0, 0, "queue_speed", null),
            (70, 5, 12, 0.0, 2, "roller", null),
            (71, 6, 12, 0.0, 4, "roller", null),
            (72, 6, 13, 0.0, 6, "roller", null),
            (73, 5, 13, 0.0, 0, "roller", null),
            (SpeedMoverId, 5, 12, 0.65, 0, "wf_colortile", null)
        };
        var f = new Fixture(layout, live: true);
        var controller = f.Items[SpeedControllerId];
        controller.Definition.Modes = 12;
        // A use needs the room's furni interactions.
        controller.Attach(f.Room, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);
        f.Owned(() => f.Room.GetGameMap().GenerateMaps());
        var rider = f.Walker(1, 8, 10, 8, 11);

        return (f, rider, CapturePublication(rider));
    }

    // One 500 ms room tick each: the Wired passes, then the roller cycle. Records the number of roller slides per tick.
    private static void SpeedTicks(Fixture f, List<byte[]> packets, int ticks, List<int> slides)
    {
        for (var tick = 0; tick < ticks; tick++) {
            var mark = packets.Count;
            f.Advance(500);
            f.Owned(() => f.Room.GetRoomItemHandler().OnCycle());
            slides.Add(packets.Skip(mark).Count(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.SlideObjectBundleComposer));
        }
    }

    // Every state text the controller sent, in order: Wired batches and its own data updates.
    private static List<string> SpeedControllerTexts(IEnumerable<byte[]> packets)
    {
        var texts = new List<string>();

        foreach (var bytes in packets) {
            var header = BinaryPrimitives.ReadUInt32BigEndian(bytes);

            if (header == ServerPacketHeader.ObjectsDataUpdateComposer) {
                texts.AddRange(ReadBatch((header, bytes[6..])).Where(entry => entry.Id == SpeedControllerId).Select(entry => entry.Text));
            }
            else if (header == ServerPacketHeader.ObjectDataUpdateComposer && ReadSingle((header, bytes[6..])) is (SpeedControllerId, var text)) {
                texts.Add(text);
            }
        }

        return texts;
    }
}
