using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Chat;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Bot Move walks a bot to the picked tile, and Bot Reaches Furni fires there with a Show Message the bot says. The owner's
    // next status update turns the owner's head one step toward the bot, and 5000 ms after that update the head goes back.
    [Fact]
    public void ABotSayingAShowMessageToEveryoneTurnsTheOwnersHeadTowardTheBot()
    {
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (30, 4, 9, 0.0, 0, "wf_trg_bot_reached_stf", "native:" + """{"Version":2,"Category":0,"NativeCode":13,"OwnedIntParams":[],"Text":"Robbie","PrimaryItems":[{"ItemId":40,"Wall":false}],"SecondaryItems":[],"FurniSourceTypes":[100],"UserSourceTypes":[100],"VariableIds":[],"Delay":null,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[]}}"""),
            (31, 4, 9, 0.65, 0, "wf_act_show_message", "native:" + """{"Version":2,"Category":1,"NativeCode":7,"OwnedIntParams":[1,34,-1],"Text":"brs hit","PrimaryItems":[],"SecondaryItems":[],"FurniSourceTypes":[],"UserSourceTypes":[0],"VariableIds":[],"Delay":0,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[]}}"""),
            (32, 5, 9, 0.0, 0, "wf_trg_says_something", SaysConfig("bt1")),
            (33, 5, 9, 0.65, 0, "wf_act_bot_move", JsonSerializer.Serialize(new WiredConfiguration
            {
                Text = "Robbie",
                SelectedItems = [40],
                FurniSources = ImmutableDictionary<string, int>.Empty.Add("items", 100),
                UserSources = ImmutableDictionary<string, int>.Empty.Add("bots", 100)
            })),
            (40, 8, 13, 0.0, 0, "wf_colortile", null)
        };
        var f = new Fixture(layout, live: true);
        f.Room.MutedUsers = [];
        // The picked tile is placed furni, so the bot's landing on it runs its walk-on as in the room.
        f.Items[40].Attach(f.Room, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);
        var owner = f.Walker(7, 6, 11, 5, 12);
        var bot = PlaceBot(f, 50, "Robbie", 6, 12);
        f.Advance(1000);
        Assert.Equal((5, 12, 5, 5), (owner.X, owner.Y, owner.RotBody, owner.RotHead));
        var packets = CapturePublication(owner);
        var chat = new ChatEvent(ChatService());

        Say(f, chat, owner, "bt1");
        var seen = new List<(int At, byte[] Bytes)>();

        for (var at = 50; at <= 9000; at += 50) {
            f.Advance(50);
            seen.AddRange(packets.Select(bytes => (at, bytes)));
            packets.Clear();
        }

        Assert.Equal((8, 13), (bot.X, bot.Y));
        var line = seen.FindIndex(sent => BinaryPrimitives.ReadUInt32BigEndian(sent.Bytes) == ServerPacketHeader.ChatComposer
            && Encoding.UTF8.GetString(sent.Bytes).Contains("brs hit"));
        Assert.True(line >= 0, "the bot says the Show Message line");
        Assert.Equal(bot.VirtualId, BinaryPrimitives.ReadInt32BigEndian(seen[line].Bytes.AsSpan(6)));

        var owners = seen.Select(sent => (sent.At, Status: HeadAndBody(sent.Bytes, owner))).Where(sent => sent.Status != null).ToList();
        Assert.DoesNotContain(owners, sent => sent.At < seen[line].At);
        Assert.Equal(2, owners.Count);
        Assert.Equal((4, 5), owners[0].Status);
        Assert.InRange(owners[0].At - seen[line].At, 1, 600);
        Assert.Equal((5, 5), owners[1].Status);
        Assert.Equal(5000, owners[1].At - owners[0].At);
    }

    // The head and body of a unit in a raw UserUpdate, or null when the packet is no UserUpdate or does not list the unit.
    private static (int Head, int Body)? HeadAndBody(byte[] bytes, RoomUser unit)
    {
        if (BinaryPrimitives.ReadUInt32BigEndian(bytes) != ServerPacketHeader.UserUpdateComposer) {
            return null;
        }

        var packet = new FlashIncomingPacket { Buffer = bytes.AsMemory(6).ToArray() };
        var count = packet.ReadInt();

        for (var i = 0; i < count; i++) {
            var id = packet.ReadInt();
            packet.ReadInt();
            packet.ReadInt();
            packet.ReadString();
            var (head, body) = (packet.ReadInt(), packet.ReadInt());
            packet.ReadInt();
            packet.ReadString();

            if (id == unit.VirtualId) {
                return (head, body);
            }
        }

        return null;
    }
}
