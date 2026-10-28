using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // At the fastest roller speed a rider lands on every roller of a chain: a status at the middle roller,
    // then the next slide, and Walks Off of the roller left with Walks On of the new one in that same tick.
    [Fact]
    public async Task AFastRollerChainLandsTheUserOnEveryRoller()
    {
        FallbackModel(EdgeRoom);
        ExecutorRoller(10, 2, 1);
        ExecutorRoller(11, 3, 1);
        ExecutorRoller(12, 4, 1);
        ExecutorFloor(13, 5, 1);
        var actor = FallbackActor(0, 1);
        var tick = 0;
        var events = RollerWalkEvents(() => tick);
        EnableExecutorRollers();
        await EdgeMove().Parse(_client, ClientPacket(2, 1));
        var log = new List<(int Tick, int? UpdateX, int? SlideToX, bool UpdateFirst)>();

        for (tick = 1; tick <= 14; tick++) {
            ExecutorTick();
            log.Add((tick, ChainUpdateX(actor), ChainSlideToX(actor), ChainUpdateBeforeSlide(actor)));
        }

        var first = log.First(entry => entry.SlideToX == 3).Tick;
        Assert.Equal(
            [(first + 1, 3, 4, true), (first + 2, 4, 5, true), (first + 3, 5, null, false)],
            log.Where(entry => entry.Tick > first && entry.Tick <= first + 3).Select(entry => (entry.Tick, entry.UpdateX, entry.SlideToX, entry.UpdateFirst)));
        Assert.Equal(
            [
                (first + 1, WiredBoxType.TriggerWalkOffFurni, 10u), (first + 1, WiredBoxType.TriggerWalkOnFurni, 11u),
                (first + 2, WiredBoxType.TriggerWalkOffFurni, 11u), (first + 2, WiredBoxType.TriggerWalkOnFurni, 12u),
                (first + 3, WiredBoxType.TriggerWalkOffFurni, 12u), (first + 3, WiredBoxType.TriggerWalkOnFurni, 13u)
            ],
            events.Where(e => e.Tick > first).OrderBy(e => e.Tick).ThenByDescending(e => e.Kind == WiredBoxType.TriggerWalkOffFurni));
    }

    private int ChainPacketIndex(RoomUser actor, uint header)
    {
        for (var index = 0; index < _client.Packets.Count; index++) {
            var sent = _client.Packets[index];

            if (sent.Header == header && (header == ServerPacketHeader.SlideObjectBundleComposer
                ? ExecutorIsAvatarSlide(sent, actor.VirtualId) : ChainStatusX(sent, actor) != null)) {
                return index;
            }
        }

        return -1;
    }

    private int? ChainUpdateX(RoomUser actor)
    {
        var index = ChainPacketIndex(actor, ServerPacketHeader.UserUpdateComposer);

        return index < 0 ? null : ChainStatusX(_client.Packets[index], actor);
    }

    private int? ChainSlideToX(RoomUser actor)
        => ChainPacketIndex(actor, ServerPacketHeader.SlideObjectBundleComposer) < 0 ? null : ExecutorAvatarSlide(actor).ToX;

    private bool ChainUpdateBeforeSlide(RoomUser actor)
    {
        var update = ChainPacketIndex(actor, ServerPacketHeader.UserUpdateComposer);
        var slide = ChainPacketIndex(actor, ServerPacketHeader.SlideObjectBundleComposer);

        return update >= 0 && slide >= 0 && update < slide;
    }

    private static int? ChainStatusX((uint Header, byte[] Body) sent, RoomUser actor)
    {
        var body = new FlashIncomingPacket { Buffer = sent.Body.ToArray() };
        var count = body.ReadInt();

        for (var i = 0; i < count; i++) {
            var id = body.ReadInt();
            var x = body.ReadInt();
            body.ReadInt();
            body.ReadString();
            body.ReadInt();
            body.ReadInt();
            body.ReadInt();
            body.ReadString();

            if (id == actor.VirtualId) {
                return x;
            }
        }

        return null;
    }
}
