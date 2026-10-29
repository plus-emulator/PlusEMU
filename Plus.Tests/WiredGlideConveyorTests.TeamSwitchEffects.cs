using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Chat;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A user says "jr", "jb", "jz" and "lv" to run Join Team (Wired Red, Wired Blue, Battle Banzai Red) and Leave Team.
    // Every join that changes the team or the game type sends YouArePlayingGame true and the effect of the new team, without a 0 in between.
    // A join into the team the user is already in sends nothing. Every leave sends YouArePlayingGame false.
    [Fact]
    public void SwitchingFromRedToBlueSendsTheBlueEffectWithoutEffectZero()
    {
        var sent = SentAfterSaying("jr", "jb", "lv");

        Assert.Equal(["playing", "effect 223", "playing", "effect 224", "not playing", "effect 0"], sent);
    }

    [Fact]
    public void SwitchingFromAWiredTeamToABattleBanzaiTeamSendsTheBattleBanzaiEffectAndLeavingSendsEffectZero()
    {
        var sent = SentAfterSaying("jr", "jz", "lv");

        Assert.Equal(["playing", "effect 223", "playing", "effect 33", "not playing", "effect 0"], sent);
    }

    [Fact]
    public void JoiningTheSameTeamTwiceSendsNothingTheSecondTime()
    {
        var sent = SentAfterSaying("jr", "jr", "lv");

        Assert.Equal(["playing", "effect 223", "not playing", "effect 0"], sent);
    }

    [Fact]
    public void LeaveTeamInNoTeamSendsOnlyNotPlaying()
    {
        var sent = SentAfterSaying("lv", "lv");

        Assert.Equal(["not playing", "not playing"], sent);
    }

    private List<string> SentAfterSaying(params string[] lines)
    {
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (30, 4, 9, 0.0, 0, "wf_trg_says_something", SaysConfig("lv")),
            (31, 4, 9, 0.65, 0, "wf_act_leave_team", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [0] })),
            (32, 5, 9, 0.0, 0, "wf_trg_says_something", SaysConfig("jr")),
            (33, 5, 9, 0.65, 0, "wf_act_join_team", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [0, 1, 0, 0] })),
            (34, 6, 9, 0.0, 0, "wf_trg_says_something", SaysConfig("jb")),
            (35, 6, 9, 0.65, 0, "wf_act_join_team", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [0, 3, 0, 0] })),
            (36, 7, 9, 0.0, 0, "wf_trg_says_something", SaysConfig("jz")),
            (37, 7, 9, 0.65, 0, "wf_act_join_team", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 1, 0, 0] }))
        };
        var f = new Fixture(layout, live: true);
        f.Room.MutedUsers = [];
        var owner = f.Walker(7, 6, 11, 5, 12);
        f.Advance(1000);
        var packets = CapturePublication(owner);
        var clock = new ChatClock();
        var chat = new ChatEvent(ChatService(clock));
        var seen = new List<string>();

        foreach (var line in lines) {
            f.Advance(150);
            clock.Skip(TimeSpan.FromSeconds(10));
            Say(f, chat, owner, line);

            for (var elapsed = 0; elapsed < 2000; elapsed += 50) {
                f.Advance(50);

                foreach (var bytes in packets) {
                    var header = BinaryPrimitives.ReadUInt32BigEndian(bytes);

                    if (header == ServerPacketHeader.AvatarEffectComposer) {
                        seen.Add($"effect {BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(10))}");
                    }
                    else if (header == ServerPacketHeader.YouArePlayingGameComposer) {
                        seen.Add(bytes[^1] == 1 ? "playing" : "not playing");
                    }
                }

                packets.Clear();
            }
        }

        return seen;
    }
}
