using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Chat;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A user says "lv" to run Leave Team and "jr" to run Join Team (Wired Red). Leave Team tells the user it is not playing every time,
    // and sends the effect 0 only when the user was in a team.
    [Fact]
    public void LeaveTeamForAUserInNoTeamSendsNoAvatarEffect()
    {
        var effects = EffectsAfterSaying("lv");

        Assert.Empty(effects);
    }

    [Fact]
    public void LeaveTeamForAUserInATeamSendsEffectZero()
    {
        var effects = EffectsAfterSaying("jr", "lv");

        Assert.Equal([223, 0], effects.Select(effect => effect.Effect));
    }

    [Fact]
    public void LeaveTeamTwiceInARowSendsEffectZeroOnlyForTheFirstLeave()
    {
        var effects = EffectsAfterSaying("jr", "lv", "lv");

        Assert.Equal([223, 0], effects.Select(effect => effect.Effect));
    }

    [Fact]
    public void JoinTeamAfterLeaveTeamSendsTheTeamEffectAgain()
    {
        var effects = EffectsAfterSaying("lv", "jr", "lv", "jr");

        Assert.Equal([223, 0, 223], effects.Select(effect => effect.Effect));
    }

    private List<(long At, int Effect)> EffectsAfterSaying(params string[] lines)
    {
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (30, 4, 9, 0.0, 0, "wf_trg_says_something", SaysConfig("lv")),
            (31, 4, 9, 0.65, 0, "wf_act_leave_team", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [0] })),
            (32, 5, 9, 0.0, 0, "wf_trg_says_something", SaysConfig("jr")),
            (33, 5, 9, 0.65, 0, "wf_act_join_team", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [0, 1, 0, 0] }))
        };
        var f = new Fixture(layout, live: true);
        f.Room.MutedUsers = [];
        var owner = f.Walker(7, 6, 11, 5, 12);
        f.Advance(1000);
        var packets = CapturePublication(owner);
        var clock = new ChatClock();
        var chat = new ChatEvent(ChatService(clock));
        var seen = new List<(long At, int Effect)>();
        var now = 1000;

        foreach (var line in lines) {
            f.Advance(150);
            now += 150;
            clock.Skip(TimeSpan.FromSeconds(10));
            Say(f, chat, owner, line);

            for (var end = now + 2000; now < end; now += 50) {
                f.Advance(50);

                foreach (var bytes in packets.Where(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.AvatarEffectComposer)) {
                    seen.Add((now + 50, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(10))));
                }

                packets.Clear();
            }
        }

        return seen;
    }

    private sealed class ChatClock : TimeProvider
    {
        private DateTimeOffset _now = FixedTimeProvider.Epoch;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Skip(TimeSpan span) => _now += span;
    }
}
