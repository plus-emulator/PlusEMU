using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Chat;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.AI.Speech;
using Plus.HabboHotel.Rooms.Chat;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.HabboHotel.Rooms.Chat.Logs;
using Plus.HabboHotel.Rooms.Chat.Styles;
using Plus.Core.Settings;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A Bot Move fired by a said keyword walks the bot to the picked tile. Said again while the bot stands on that tile, the
    // client gets only the box flash and the chat line: no UserUpdate for the bot at all.
    [Fact]
    public void BotMoveToTheTileTheBotStandsOnSendsNoUserUpdate()
    {
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (30, 5, 11, 0.0, 0, "wf_trg_says_something", SaysConfig("bmv1")),
            (31, 5, 11, 0.65, 0, "wf_act_bot_move", JsonSerializer.Serialize(new WiredConfiguration
            {
                Text = "Robbie",
                SelectedItems = [40],
                FurniSources = ImmutableDictionary<string, int>.Empty.Add("items", 100),
                UserSources = ImmutableDictionary<string, int>.Empty.Add("bots", 100)
            })),
            (40, 8, 12, 0.0, 0, "wf_colortile", null)
        };
        var f = new Fixture(layout, live: true);
        f.Room.MutedUsers = [];
        var owner = f.Walker(7, 6, 10);
        var bot = PlaceBot(f, 50, "Robbie", 5, 12);
        f.Advance(1000);
        var packets = CapturePublication(owner);
        var chat = new ChatEvent(ChatService());

        Say(f, chat, owner, "bmv1");
        f.Advance(4000);

        Assert.Equal((8, 12), (bot.X, bot.Y));
        Assert.Contains(packets, bytes => BotStatus(bytes, bot) is { } status && status.Contains("mv 6,12,"));

        f.Advance(4000);
        packets.Clear();

        Say(f, chat, owner, "bmv1");
        f.Advance(4000);

        Assert.Equal((8, 12), (bot.X, bot.Y));
        Assert.DoesNotContain(packets, bytes => BotStatus(bytes, bot) != null);
        Assert.Equal(new[] { ServerPacketHeader.ObjectsDataUpdateComposer, ServerPacketHeader.ChatComposer },
            packets.Select(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes)).ToArray());
        Assert.Contains("bmv1", Encoding.UTF8.GetString(packets[1]));
    }

    // The said line as the client sends it: the raw chat request through its handler, run as the room does.
    private static void Say(Fixture f, ChatEvent chat, RoomUser speaker, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var body = new byte[2 + bytes.Length + 4];
        BinaryPrimitives.WriteUInt16BigEndian(body, (ushort)bytes.Length);
        bytes.CopyTo(body, 2);
        Exception? failure = null;
        f.Owned(() =>
        {
            try {
                chat.Parse(speaker.GetClient()!, new FlashIncomingPacket { Buffer = body }).GetAwaiter().GetResult();
            }
            catch (Exception e) {
                failure = e;
            }
        });
        Assert.Null(failure);
    }

    private static RoomUser PlaceBot(Fixture f, int virtualId, string name, int x, int y)
    {
        var speeches = new List<RandomSpeech>();
        var data = new RoomBot(1, 14, "generic", "stand", name, "", "hd-180-1", x, y, 0, 2, 0, 0, 0, 0, ref speeches, "M", 0, 7,
            false, 7, false, 0);
        var bot = new RoomUser(0, 14, virtualId, f.Room, null, TestChatEmotions.Unused, TestRewardProgress.Unused)
        {
            InternalRoomId = virtualId,
            BotData = data
        };
        bot.InitializePosition(x, y, f.Room.GetGameMap().SqAbsoluteHeight(x, y));
        f.ReplaceUser(bot);
        f.Owned(() => f.Room.GetGameMap().Navigation!.Admit(bot));

        return bot;
    }

    private static IRoomChatService ChatService(TimeProvider? clock = null) => new RoomChatService(NoOp<IChatStyleManager>.Create(), NoOp<IChatlogManager>.Create(),
        new WordFilterManager(NoOp<Plus.Database.IDatabase>.Create()), NoOp<ICommandManager>.Create(), NoOp<IModerationManager>.Create(),
        NoOp<ISettingsManager>.Create(), NoOp<IQuestManager>.Create(), NoOp<IRewardTrackManager>.Create(), NoOp<IGameClientManager>.Create(),
        clock ?? TimeProvider.System);

    // The bot's status string in a UserUpdate, or null when the packet is no UserUpdate or does not list the bot.
    private static string? BotStatus(byte[] bytes, RoomUser bot)
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
            packet.ReadInt();
            packet.ReadInt();
            packet.ReadInt();
            var status = packet.ReadString();

            if (id == bot.VirtualId) {
                return status;
            }
        }

        return null;
    }

    private class NoOp<T> : DispatchProxy where T : class
    {
        public static T Create() => DispatchProxy.Create<T, NoOp<T>>();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType == typeof(Task) ? Task.CompletedTask :
            targetMethod?.ReturnType.IsValueType == true && targetMethod.ReturnType != typeof(void) ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }
}
