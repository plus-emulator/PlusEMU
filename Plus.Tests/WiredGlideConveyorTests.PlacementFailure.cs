using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610131318-860371703: a Place Furni run that cannot place its copies shows every user in the room one
// NotificationDialog wired.error, at most once per 5.0 s since that user's last dialog; the rest of the stack still runs.
public sealed partial class WiredGlideConveyorTests
{
    private static Fixture PlacementFailureRoom(string trigger, WiredConfiguration triggerConfig)
    {
        var template = new ItemDefinition
        {
            Id = 5,
            Type = ItemType.Floor,
            Width = 1,
            Length = 1,
            Height = 0.1,
            Stackable = true,
            Modes = 1,
            ItemName = "placement_template",
            PublicName = "placement_template",
            InteractionName = "default",
            AdjustableHeights = [],
            VendingIds = []
        };
        // Both saved copies sit off the floor plan, so neither can be placed.
        var place = new WiredConfiguration
        {
            TemporaryPlacement = new(Altitude: WiredPlaceAltitudeType.SourceAltitude),
            Snapshots = [new(900, 5, 60, 60, 0, 0, "0"), new(901, 5, 61, 60, 0, 0, "0")]
        };
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (61, 4, 12, 0.0, 0, trigger, JsonSerializer.Serialize(triggerConfig)),
            (62, 4, 12, 0.65, 0, "wf_act_place_furni", JsonSerializer.Serialize(place)),
            (63, 4, 12, 0.7, 0, "wf_act_log", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [0, 0], Text = "after place" }))
        };
        var f = new Fixture(layout, live: true, definitions: new TestWiredDefinitions(() => new() { [5] = template }));
        f.Room.OwnerId = 1;

        return f;
    }

    private static Fixture SayPlacementFailureRoom() =>
        PlacementFailureRoom("wf_trg_says_something", new WiredConfiguration { IntParams = [1, 0, 0], Text = "p6" });

    private static List<Dictionary<string, string>> CaptureDialogs(RoomUser user)
    {
        var dialogs = new List<Dictionary<string, string>>();
        user.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();

            if (BinaryPrimitives.ReadUInt32BigEndian(bytes) != ServerPacketHeader.RoomNotificationComposer) {
                return false;
            }

            var position = 6;
            string String()
            {
                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position));
                var value = Encoding.UTF8.GetString(bytes, position + 2, length);
                position += 2 + length;

                return value;
            }
            var values = new Dictionary<string, string> { [""] = String() };
            var count = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(position));
            position += 4;

            for (var index = 0; index < count; index++) {
                var key = String();
                values[key] = String();
            }

            dialogs.Add(values);

            return false;
        };

        return dialogs;
    }

    [Fact]
    public void FailedPlacementShowsTheOwnerOneWiredErrorDialogAndTheStackStillRuns()
    {
        var f = SayPlacementFailureRoom();
        var owner = f.Walker(1, 4, 10, 4, 11);
        var dialogs = CaptureDialogs(owner);

        f.Owned(() => owner.OnChat(0, "p6", false));
        f.Advance(500);

        // Two copies failed; one dialog.
        var dialog = Assert.Single(dialogs);
        Assert.Equal(new Dictionary<string, string>
        {
            [""] = "wired.error",
            ["image"] = "${image.library.url}album1584/WIRD2.png",
            ["message"] = "${room.error.wired.placement_failure}"
        }, dialog);
        Assert.DoesNotContain(f.Items.Values, item => item.IsTemporary);
        Assert.Single(f.Room.GetWired().ReadLogs(0, 50, query: "after place").Entries);
    }

    [Fact]
    public void PlacementFailureDialogWaitsFiveSecondsSinceTheLastDialogShown()
    {
        var f = SayPlacementFailureRoom();
        var owner = f.Walker(1, 4, 10, 4, 11);
        var dialogs = CaptureDialogs(owner);
        var shown = new List<int>();
        var elapsed = 0;

        foreach (var at in new[] { 0, 2600, 3600, 4400, 6600, 10000, 15600 }) {
            f.Advance(at - elapsed);
            var before = dialogs.Count;
            f.Owned(() => owner.OnChat(0, "p6", false));
            f.Advance(50);
            elapsed = at + 50;

            if (dialogs.Count > before) {
                shown.Add(at);
            }
        }

        Assert.Equal([0, 6600, 15600], shown);
        // Every run still reached the effect after Place Furni.
        Assert.Equal(7, f.Room.GetWired().ReadLogs(0, 50, query: "after place").Total);
        // The monitor counts the dialogs shown, not the failed runs.
        var tally = Assert.Single(f.Room.GetWired().ReadMonitor().Logs.Tallies, tally => tally.Source == WiredLogSource.PlacementFailure);
        Assert.Equal(3, tally.Count);
        Assert.Equal(3, Assert.Single(f.Room.GetWired().ReadErrors(), row => row.ErrorName == "PLACEMENT_FAILURE").ThrowCount);
    }

    [Fact]
    public void TimerRunsShowTheOwnerThePlacementFailureDialog()
    {
        // Repeater [10] fires every 5 s with no triggering user.
        var f = PlacementFailureRoom("wf_trg_periodically", new WiredConfiguration { IntParams = [10] });
        var owner = f.Walker(1, 4, 10, 4, 11);
        var dialogs = CaptureDialogs(owner);

        f.Advance(20000);

        Assert.InRange(dialogs.Count, 3, 4);
        Assert.Equal(dialogs.Count, f.Room.GetWired().ReadLogs(0, 50, query: "after place").Total);
    }

    // Observed on AIR WIN63-202610131318-860371703: the dialog goes to every user in the room, the visitor without rights too, each on its
    // own 5 s cooldown: says at 0 (owner), 1.8 (visitor), 3.0 (owner), 8.1 (visitor), 9.5 (owner) show both users a dialog at 0 and 8.1 only.
    [Fact]
    public void PlacementFailureDialogGoesToEveryUserInTheRoom()
    {
        var f = SayPlacementFailureRoom();
        var owner = f.Walker(1, 4, 10, 4, 11);
        var guest = f.Walker(2, 6, 10, 6, 11);
        var ownerDialogs = CaptureDialogs(owner);
        var guestDialogs = CaptureDialogs(guest);
        var ownerShown = new List<int>();
        var guestShown = new List<int>();
        var elapsed = 0;

        foreach (var (at, speaker) in new[] { (0, owner), (1800, guest), (3000, owner), (8100, guest), (9500, owner) }) {
            f.Advance(at - elapsed);
            elapsed = at;
            var (ownerBefore, guestBefore) = (ownerDialogs.Count, guestDialogs.Count);
            f.Owned(() => speaker.OnChat(0, "p6", false));
            f.Advance(100);
            elapsed += 100;

            if (ownerDialogs.Count > ownerBefore) {
                ownerShown.Add(at);
            }

            if (guestDialogs.Count > guestBefore) {
                guestShown.Add(at);
            }
        }

        Assert.Equal([0, 8100], ownerShown);
        Assert.Equal([0, 8100], guestShown);
    }
}
