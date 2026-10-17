using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610151745-318700621: Control Clock (Start) and Give Score To Team in one stack give the score in the game
    // they start, with the give above or below the clock. The effects' default order is random per firing, so the give must still follow.
    [Theory]
    [InlineData(0.65, 1.3)]
    [InlineData(1.3, 0.65)]
    public void GiveTeamScoreInTheStackThatStartsTheGameCounts(double clockHeight, double giveHeight)
    {
        string Config(int[] parameters, params uint[] selected) => JsonSerializer.Serialize(new WiredConfiguration
        {
            IntParams = [.. parameters],
            SelectedItems = [.. selected]
        });

        for (var attempt = 0; attempt < 12; attempt++) {
            var f = new Fixture(
            [
                (CounterId, 8, 12, 0.0, 0, "wf_game_upcounter1", null),
                (21, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "pulse" })),
                (22, 4, 12, clockHeight, 0, "wf_act_control_clock", Config([0, 100], CounterId)),
                (23, 4, 12, giveHeight, 0, "wf_act_give_score_tm", Config([1000, 0, 2]))
            ], live: true);
            f.MovementContext().Room.GetWired().AttachRoomItem(f.Items[CounterId]);
            var rider = f.Walker(1, 4, 10, 4, 11);
            f.Advance(500);
            f.Owned(() => rider.OnChat(0, "pulse", false));

            Assert.Equal(1000, f.Room.GetGameManager().Points[(int)Team.Green]);
        }
    }
}
