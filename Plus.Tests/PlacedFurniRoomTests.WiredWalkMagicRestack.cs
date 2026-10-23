using System.Globalization;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// A 50 ms Repeat Short with Furni to User moves a walk magic tile and a 0.15 high tile to a user, with physics through furni and users.
// Both land on the user's tile, so each firing restacks one on top of the other and the stack climbs.
// Every WiredMovements that restacks the walk magic tile under a standing user also moves that user to the new walk magic height
// (avatar entry, animation type 1, 500 ms) after the furni entries, and the user's status update shows "wf".
public partial class PlacedFurniRoomTests
{
    private const uint RestackWalkMagic = 60, RestackTile = 61, RestackStackId = 62;

    [Fact]
    public void FurniToUserRestackingAWalkMagicTileUnderAStandingUserCarriesTheUserInTheSameWiredMovements()
    {
        var (clock, manager) = GridRoom();
        var walkMagic = Add(RestackWalkMagic, 1, 2, type: InteractionType.WalkMagicTile);
        var tile = ExecutorFloor(RestackTile, 1, 2, height: 0.15);
        var actor = ExecutorActor(1, 0);
        AddFollowBox(RestackStackId, "wf_trg_period_short", new() { IntParams = [1] });
        AddFollowBox(RestackStackId + 1, "wf_act_furni_to_user", new() { IntParams = [100, 200], SelectedItems = [walkMagic.Id, tile.Id] });
        AddFollowBox(RestackStackId + 2, "wf_slc_users_bytype", new() { IntParams = [1, 0, 0] });
        AddFollowBox(RestackStackId + 3, "wf_xtra_mov_physics", new() { IntParams = [0, 1, 1, 0, 900, 900, 900] });

        var passes = RunRoom(manager, clock, clock.Ms + 2000, period: 25);

        var stream = passes.SelectMany(pass => pass.Packets).ToArray();
        var restacks = 0;
        double? walkMagicZ = null;
        var carried = false;

        foreach (var packet in stream) {
            if (packet.Header == ServerPacketHeader.WiredMovementsComposer) {
                var entries = MovementEntries(packet);
                var avatars = entries.Select((entry, index) => (entry, index)).Where(e => e.entry.Type == 0 && e.entry.Id == actor.VirtualId).ToArray();
                var restack = entries.SingleOrDefault(entry => entry.Type == 1 && entry.Id == walkMagic.Id
                    && (entry.FromX, entry.FromY, entry.ToX, entry.ToY) == (1, 0, 1, 0));

                if (restack.Type != 1) {
                    Assert.Empty(avatars);
                }
                else {
                    restacks++;
                    var avatar = Assert.Single(avatars);
                    Assert.Equal((1, 0, restack.FromZ, 1, 0, restack.ToZ, 1, 500),
                        (avatar.entry.FromX, avatar.entry.FromY, avatar.entry.FromZ, avatar.entry.ToX, avatar.entry.ToY, avatar.entry.ToZ,
                            avatar.entry.AnimationType, avatar.entry.DurationMs));
                    Assert.Equal(500, restack.DurationMs);
                    Assert.All(entries.Take(avatar.index), entry => Assert.Equal(1, entry.Type));
                    carried = true;
                }

                if (entries.LastOrDefault(entry => entry.Type == 1 && entry.Id == walkMagic.Id) is { Type: 1 } last) {
                    walkMagicZ = double.Parse(last.ToZ, CultureInfo.InvariantCulture);
                }
            }
            else if (carried && packet.Header == ServerPacketHeader.UserUpdateComposer
                     && UserUpdateEntry(packet, actor) is { } update) {
                Assert.Contains("/wf/", update.Status);
                Assert.Equal(walkMagicZ!.Value, double.Parse(update.Z, CultureInfo.InvariantCulture), 3);
            }
        }

        Assert.True(restacks >= 5, $"restacks under the user: {restacks}");
        Assert.Contains(stream, packet => carried && packet.Header == ServerPacketHeader.UserUpdateComposer && UserUpdateEntry(packet, actor) != null);
    }

    private static (int Type, uint Id, int FromX, int FromY, string FromZ, int ToX, int ToY, string ToZ, int AnimationType, int DurationMs)[]
        MovementEntries((uint Header, byte[] Body) packet)
    {
        var body = new FlashIncomingPacket { Buffer = packet.Body };
        var entries = new List<(int, uint, int, int, string, int, int, string, int, int)>();

        for (var count = body.ReadInt(); count > 0; count--) {
            var type = body.ReadInt();

            if (type == 3) {
                entries.Add((type, (uint)body.ReadInt(), 0, 0, "", 0, 0, "", 0, 0));
                body.ReadInt();
                body.ReadInt();
                continue;
            }

            Assert.True(type is 0 or 1, $"movement type {type}");
            var (fromX, fromY, toX, toY) = (body.ReadInt(), body.ReadInt(), body.ReadInt(), body.ReadInt());
            var (fromZ, toZ) = (body.ReadString(), body.ReadString());
            var id = (uint)body.ReadInt();

            if (type == 1) {
                var duration = body.ReadInt();
                body.ReadInt();

                if (body.ReadBool()) {
                    body.ReadInt();
                }

                if (body.ReadBool()) {
                    body.ReadInt();
                }

                entries.Add((type, id, fromX, fromY, fromZ, toX, toY, toZ, 0, duration));
            }
            else {
                var animation = body.ReadInt();
                var duration = body.ReadInt();
                body.ReadInt();
                body.ReadInt();

                if (body.ReadBool()) {
                    body.ReadInt();
                }

                entries.Add((type, id, fromX, fromY, fromZ, toX, toY, toZ, animation, duration));
            }
        }

        return [.. entries];
    }

    private static (string Z, string Status)? UserUpdateEntry((uint Header, byte[] Body) packet, RoomUser actor)
    {
        var body = new FlashIncomingPacket { Buffer = packet.Body };

        for (var count = body.ReadInt(); count > 0; count--) {
            var id = body.ReadInt();
            body.ReadInt();
            body.ReadInt();
            var z = body.ReadString();
            body.ReadInt();
            body.ReadInt();
            body.ReadInt();
            var status = body.ReadString();

            if (id == actor.VirtualId) {
                return (z, status);
            }
        }

        return null;
    }
}
