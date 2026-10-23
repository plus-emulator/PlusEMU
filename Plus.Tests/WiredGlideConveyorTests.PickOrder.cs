using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: a card reopens its picks in the order of a Java HashSet<Integer>. The set is built twice: first
// like the copy constructor of JDK 18 and older (table sized from the number of ids sent), then again by adding the first set's ids one by one
// to a default set. Orders below come from Habbo (first row) or the real JDK 21 (java.util.HashSet with the same two steps); the same rule matched 160 of 165
// reopen samples read back from Habbo. With no bucket of nine ids, exactly 12 ids is where the two steps differ from a plain add loop.
public sealed partial class WiredGlideConveyorTests
{
    private const uint HashToggle = 90;

    public static TheoryData<int[], int[]> HashSetPickOrders => new()
    {
        // A 12 id sample read back from Habbo (floor ids 2147418112 + 10 to 130).
        { [2147418195, 2147418151, 2147418214, 2147418125, 2147418131, 2147418137, 2147418206, 2147418127, 2147418242, 2147418167, 2147418122, 2147418135],
            [2147418127, 2147418206, 2147418125, 2147418122, 2147418137, 2147418167, 2147418135, 2147418151, 2147418214, 2147418195, 2147418131, 2147418242] },
        // 12 ids from the real JDK 21, two steps; a plain add loop gives 16, 49, 17, 6, 39, 25, 9, 26, 12, 13, 15, 47.
        { [26, 25, 13, 39, 12, 15, 16, 9, 49, 6, 17, 47], [16, 49, 17, 6, 39, 9, 25, 26, 12, 13, 15, 47] },
        { [38, 14, 51, 57, 24, 53, 25, 44, 58, 10, 30, 43], [51, 53, 38, 24, 57, 25, 10, 58, 43, 44, 14, 30] },
        { [12, 55, 37, 22, 6, 46, 34, 44, 50, 58, 31], [34, 50, 37, 22, 6, 55, 58, 12, 44, 46, 31] },
        { [21, 26, 3, 18, 36, 1, 28, 14, 24, 35, 6, 30, 34], [1, 34, 3, 35, 36, 6, 14, 18, 21, 24, 26, 28, 30] },
        { [58, 13, 39, 30, 4, 23, 9, 14, 46, 11, 48, 29, 57, 12, 33, 43, 51, 3, 8, 2], [33, 2, 3, 4, 39, 8, 9, 11, 43, 12, 13, 14, 46, 48, 51, 23, 57, 58, 29, 30] },
    };

    private static (Fixture Fixture, RoomUser Owner, List<(uint Header, byte[] Payload)> Frames) HashSetFixture(IEnumerable<int> tiles)
    {
        var f = new Fixture(
        [
            .. tiles.Select((id, index) => ((uint)id, 1 + index % 14, 12 + index / 14, 0.0, 0, "wf_colortile", (string?)null))
        ], live: true);
        f.Room.OwnerId = 1;
        f.Room.OwnerName = "user1";
        f.Room.Type = "private";
        f.PlaceWired(HashToggle, 4, 9, 0.65, "wf_act_toggle_state");
        f.Items[HashToggle].Attach(f.Room, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);

        foreach (var tile in tiles) {
            f.Items[(uint)tile].ExtraData = new LegacyDataFormat { Data = "0" };
        }

        var owner = f.Walker(1, 6, 10, 6, 11);

        return (f, owner, CaptureFrames(owner));
    }

    private static int[] SaveToggleAndReopenPicks(int[] sent)
    {
        var (f, owner, frames) = HashSetFixture(sent.Distinct());
        SavePicks(f, owner, frames, HashToggle, [0], sent);
        Assert.Equal("ok", SaveAnswer(frames));

        frames.Clear();
        f.Owned(() => new OpenWiredEvent().Parse(f.Room, owner.GetClient()!, EditorTestSupport.Incoming((int)HashToggle)).GetAwaiter().GetResult());
        var frame = new FlashIncomingPacket { Buffer = Assert.Single(frames, frame => frame.Header == ServerPacketHeader.WiredEffectConfigComposer).Payload };
        frame.ReadInt();
        var picks = new int[frame.ReadInt()];

        for (var index = 0; index < picks.Length; index++) {
            picks[index] = frame.ReadInt();
        }

        return picks;
    }

    [Theory]
    [MemberData(nameof(HashSetPickOrders))]
    public void SavedPicksReopenInTheOrderOfTheTwoStepJavaHashSet(int[] sent, int[] reopened)
    {
        Assert.Equal(reopened, SaveToggleAndReopenPicks(sent));
    }
}
