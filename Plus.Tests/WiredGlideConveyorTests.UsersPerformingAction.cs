using System.Text.Json;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Avatar;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Users Performing Action, fired through a real room: the owner acts through the avatar packets, a stack says which users its
// selector picked by turning them.
public sealed partial class WiredGlideConveyorTests
{
    private const int WaveCode = 0;
    private const int BlowCode = 1;
    private const int LaughCode = 2;
    private const int RespectCode = 3;
    private const int DanceCode = 11;

    // A blow or a laugh is never selected, from 0.2 s to 6.5 s after it.
    [Theory]
    [InlineData(2, BlowCode)]
    [InlineData(3, LaughCode)]
    public void BlowAndLaughAreNotSelected(int expression, int code)
    {
        var room = new ActionSelectorRoom();

        room.Express(expression);
        room.Wait(200);
        Assert.Empty(room.Picked(code));

        room.Wait(1000);
        Assert.Empty(room.Picked(code));
    }

    // A blow does not end the wave before it: the wave is still selected 3.8 s after it, with a blow in between.
    [Fact]
    public void ABlowDoesNotReplaceTheWave()
    {
        var room = new ActionSelectorRoom();

        room.Express(1);
        room.Wait(3100);
        room.Express(2);
        room.Wait(700);

        Assert.Equal([room.Owner.VirtualId], room.Picked(WaveCode));
    }

    // Respect is selected 2.0 s after the expression and no longer 3.0 s after it.
    [Fact]
    public void RespectIsSelectedForAboutTwoSeconds()
    {
        var room = new ActionSelectorRoom();

        room.Express(7);
        room.Wait(2000);
        Assert.Equal([room.Owner.VirtualId], room.Picked(RespectCode));

        room.Wait(1000 - ActionSelectorRoom.FireMilliseconds);
        Assert.Empty(room.Picked(RespectCode));
    }

    // Once the dance stops, nobody is selected as dancing, though the dance started 3.5 s before.
    [Fact]
    public void AStoppedDanceIsNotSelected()
    {
        var room = new ActionSelectorRoom();

        room.Dance(1);
        room.Wait(500);
        Assert.Equal([room.Owner.VirtualId], room.Picked(DanceCode));

        room.Wait(2500 - ActionSelectorRoom.FireMilliseconds);
        room.Dance(0);
        room.Wait(500);
        Assert.Empty(room.Picked(DanceCode));
    }

    private sealed class ActionSelectorRoom
    {
        public const int FireMilliseconds = 200;
        private static readonly int[] Codes = [WaveCode, BlowCode, LaughCode, RespectCode, DanceCode];
        private readonly Fixture _fixture;
        private readonly List<byte[]> _packets;
        private readonly RoomAvatarActionService _actions = new(TimeProvider.System,
            CatalogSnapshotTestSupport.Proxy<IQuestManager>((_, _) => null), TestNavigationRewards.Instance);

        public RoomUser Owner { get; }

        public ActionSelectorRoom()
        {
            // One stack per action code: "pick <code>" fires a Users Performing Action selector and turns whoever it picked.
            var layout = Codes.SelectMany((code, index) => new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
            {
                ((uint)(30 + index * 3), 4 + index, 2, 0.0, 0, "wf_trg_says_something",
                    JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = $"pick {code}" })),
                ((uint)(31 + index * 3), 4 + index, 2, 0.65, 0, "wf_slc_users_byaction", ByActionRow(code)),
                ((uint)(32 + index * 3), 4 + index, 2, 1.3, 0, "wf_act_move_rotate_user", TurnSelectedUsersRow())
            }).ToArray();
            _fixture = new Fixture(layout, live: true);
            Owner = _fixture.Walker(1, 6, 5, 6, 6);
            _fixture.Walker(2, 9, 5, 9, 6);
            _packets = CapturePublication(Owner);
        }

        public void Express(int expression) => _fixture.Owned(() =>
            new ActionEvent(_actions).Parse(_fixture.Room, Owner.GetClient()!, IntPacket(expression)).GetAwaiter().GetResult());

        public void Dance(int dance) => _fixture.Owned(() =>
            new DanceEvent(_actions).Parse(_fixture.Room, Owner.GetClient()!, IntPacket(dance)).GetAwaiter().GetResult());

        public void Wait(int milliseconds) => _fixture.Advance(milliseconds);

        public int[] Picked(int code)
        {
            _packets.Clear();
            _fixture.Owned(() => Owner.OnChat(0, $"pick {code}", false));
            _fixture.Advance(FireMilliseconds);

            return [.. ReadMovementEntries(_packets).Where(entry => entry.Type == 3).Select(entry => entry.Id).Order()];
        }

        private static string ByActionRow(int code)
        {
            Assert.True(WiredBoxRegistry.TryGet("wf_slc_users_byaction", out var descriptor));

            return "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
            {
                Category = descriptor.Category,
                NativeCode = descriptor.EditorCode,
                OwnedIntParams = [code],
                Text = "",
                FurniSourceTypes = [],
                UserSourceTypes = [],
                Filter = false,
                Inverse = false
            });
        }

        private static string TurnSelectedUsersRow()
        {
            Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate_user", out var descriptor));

            return "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
            {
                Category = descriptor.Category,
                NativeCode = descriptor.EditorCode,
                OwnedIntParams = [-1, 9],
                FurniSourceTypes = [],
                UserSourceTypes = [WiredSources.Selector],
                Delay = 0
            });
        }

        private static FlashIncomingPacket IntPacket(int value)
        {
            using var stream = PlusMemoryStream.GetStream();
            var packet = new FlashOutgoingPacket(stream);
            packet.WriteInteger(value);

            return new() { Buffer = stream.ToArray().AsMemory(6) };
        }
    }
}
