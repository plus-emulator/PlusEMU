using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public async Task ARollerLandsTheUserOnTopOfTheWalkableFurniOnTheTargetTile()
    {
        FallbackModel(EdgeRoom);
        ExecutorRoller(10, 2, 1);
        ExecutorFloor(11, 3, 1, height: 0.2);
        var actor = FallbackActor(0, 1);
        EnableExecutorRollers();
        await EdgeMove().Parse(_client, ClientPacket(2, 1));
        var slides = new List<(int ToX, string ToZ)>();

        for (var tick = 0; tick < 8 && slides.Count == 0; tick++) {
            ExecutorTick();
            slides.AddRange(LandingAvatarSlides(actor));
        }

        Assert.Equal([(3, "0.2")], slides);
        ExecutorTick();
        var update = ExecutorUpdate(actor);
        Assert.Equal((3, 1, "0.2"), (update.X, update.Y, update.Z));
        Assert.Equal(0.2, actor.Z, 3);
    }

    // The roller is 0.5 high. A raised roller stands on a 0.2 floor item, so its rider stands at 0.7.
    [Theory]
    [InlineData(false, 0.25, true, "0.25")]
    [InlineData(false, 0.5, true, "0.5")]
    [InlineData(false, 0.75, false, null)]
    [InlineData(true, 0.5, true, "0.5")]
    [InlineData(true, 0.75, false, null)]
    [InlineData(true, null, true, "0.0")]
    public async Task ARollerMovesTheUserOnlyOntoATargetNotAboveTheUser(bool raised, double? target, bool slid, string? toZ)
    {
        FallbackModel(EdgeRoom);

        if (raised) {
            ExecutorFloor(12, 2, 1, height: 0.2);
        }

        ExecutorRoller(10, 2, 1);

        if (target is { } height) {
            ExecutorFloor(11, 3, 1, height: height);
        }

        var actor = FallbackActor(0, 1);
        EnableExecutorRollers();
        await EdgeMove().Parse(_client, ClientPacket(2, 1));
        var slides = new List<(int ToX, string ToZ)>();

        for (var tick = 0; tick < 10; tick++) {
            ExecutorTick();
            slides.AddRange(LandingAvatarSlides(actor));
        }

        var rider = raised ? 0.7 : 0.5;
        Assert.Equal(slid ? [(3, toZ!)] : [], slides);
        Assert.Equal(slid ? (3, 1, double.Parse(toZ!, System.Globalization.CultureInfo.InvariantCulture)) : (2, 1, rider),
            (actor.X, actor.Y, Math.Round(actor.Z, 3)));
    }

    private List<(int ToX, string ToZ)> LandingAvatarSlides(RoomUser actor)
    {
        var slides = new List<(int ToX, string ToZ)>();

        foreach (var sent in _client.Packets.Where(p => p.Header == ServerPacketHeader.SlideObjectBundleComposer)) {
            var packet = new FlashIncomingPacket { Buffer = sent.Body.ToArray() };
            packet.ReadInt();
            packet.ReadInt();
            var toX = packet.ReadInt();
            packet.ReadInt();

            if (packet.ReadInt() != 0) {
                continue;
            }

            packet.ReadUInt();
            packet.ReadInt();

            if (packet.ReadInt() != actor.VirtualId) {
                continue;
            }

            packet.ReadString();
            slides.Add((toX, packet.ReadString()));
        }

        return slides;
    }
}
