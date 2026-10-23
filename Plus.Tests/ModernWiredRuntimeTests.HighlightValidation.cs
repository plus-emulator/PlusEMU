using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

// What Set Furni Highlight accepts and the key of each refusal.
public partial class ModernWiredRuntimeTests
{
    private const string HighlightKey = "wiredfurni.error.set_furni_highlight.";
    private const int HighlightYellow = 0xFFEA00;
    private const int HighlightOrange = 0xFFB700;
    private const int White = 0xFFFFFF;

    // The 21 colours the editor offers.
    private static readonly int[] HighlightPalette =
    [
        16777215, 16772910, 16758528, 16747008, 16734774, 16724016, 16724855, 16733627, 15741144, 12076287, 7883007,
        4611327, 2395391, 2146303, 2156016, 1628592, 2149480, 5300272, 10546464, 16557530, 12291417
    ];

    private static readonly int[] StrengthProbes = [-1, 0, 1, 10, 20, 25, 38, 40, 49, 50, 51, 60, 75, 76, 99, 100, 101];

    public static TheoryData<int, int, bool> StrengthRules()
    {
        int[][] allowed = [[0, 100], [0, 100], [0], [0, 25, 50, 75, 100], [0, 38], [0, 100]];
        var data = new TheoryData<int, int, bool>();

        for (var type = 0; type < allowed.Length; type++) {
            foreach (var strength in StrengthProbes) {
                data.Add(type, strength, allowed[type].Contains(strength));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(StrengthRules))]
    public async Task HighlightStrengthIsLimitedPerType(int type, int strength, bool accepted)
    {
        var f = new MovementOracleSession(Highlight);
        var color = type is 2 or 4 ? White : HighlightOrange;

        await SaveEffect(f, FurniVisualSave([0, type, strength, color, 1, 0, 0]));

        AssertSaved(f, accepted, HighlightKey + "invalid_strength");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 255)]
    [InlineData(0, 0xFF0000)]
    [InlineData(0, 0x00FF00)]
    [InlineData(0, 0x808080)]
    [InlineData(0, 0xFFFFFE)]
    [InlineData(1, 0)]
    [InlineData(1, 0xFF0000)]
    [InlineData(3, 0)]
    [InlineData(3, 255)]
    [InlineData(3, 0xFFFFFE)]
    // White is not a colour tint.
    [InlineData(3, White)]
    [InlineData(5, 0)]
    [InlineData(5, 0x808080)]
    [InlineData(2, 255)]
    [InlineData(4, 255)]
    public async Task HighlightColourOutsideTheTypesColoursIsRefused(int type, int color)
    {
        var f = new MovementOracleSession(Highlight);
        var strength = type switch { 2 => 0, 3 => 50, 4 => 38, _ => 100 };

        await SaveEffect(f, FurniVisualSave([0, type, strength, color, 1, 0, 0]));

        AssertSaved(f, false, HighlightKey + "invalid_color");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(3, false)]
    [InlineData(5, true)]
    public async Task HighlightAcceptsEveryPaletteColourAndYellow(int type, bool white)
    {
        foreach (var color in HighlightPalette.Where(color => white || color != White).Append(HighlightYellow)) {
            var f = new MovementOracleSession(Highlight);
            var strength = type == 3 ? 50 : 100;

            await SaveEffect(f, FurniVisualSave([0, type, strength, color, 1, 0, 0]));

            AssertSaved(f, true, HighlightKey + "invalid_color");
        }
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, White)]
    [InlineData(2, HighlightYellow)]
    [InlineData(2, HighlightOrange)]
    [InlineData(4, 0)]
    [InlineData(4, White)]
    [InlineData(4, HighlightYellow)]
    [InlineData(4, HighlightOrange)]
    public async Task DarkenAndBrightenAcceptTheColoursTheEditorDoesNotShow(int type, int color)
    {
        var f = new MovementOracleSession(Highlight);

        await SaveEffect(f, FurniVisualSave([0, type, type == 4 ? 38 : 0, color, 1, 0, 0]));

        AssertSaved(f, true, HighlightKey + "invalid_color");
    }

    [Theory]
    // Type, then strength, then colour decide which key the player sees.
    [InlineData(new[] { 0, 6, 999, 5, 1, 0, 0 }, "invalid_type")]
    [InlineData(new[] { 9, 6, 100, HighlightYellow, 0, 4, 0 }, "invalid_type")]
    [InlineData(new[] { 0, -1, 100, HighlightYellow, 0, 4, 0 }, "invalid_type")]
    [InlineData(new[] { 0, 0, 999, 5, 1, 0, 0 }, "invalid_strength")]
    // Brighten must pulse; the strength is checked first, then the pulse, then the colour.
    [InlineData(new[] { 0, 4, 99, White, 0, 0, 0 }, "invalid_strength")]
    [InlineData(new[] { 0, 4, 38, 255, 0, 0, 0 }, "pulse_only")]
    [InlineData(new[] { 0, 4, 38, White, 0, 0, 0 }, "pulse_only")]
    public async Task HighlightRefusalsFollowTheOrderTypeStrengthPulseColour(int[] ints, string key)
    {
        var f = new MovementOracleSession(Highlight);

        await SaveEffect(f, FurniVisualSave(ints));

        AssertSaved(f, false, HighlightKey + key);
    }

    [Theory]
    // A steady colour tint of furni that is not from the Builders Club is refused, after the colour.
    [InlineData(new[] { 0, 3, 0, HighlightOrange, 0, 0, 0 }, new uint[] { 8 }, "normal_furni")]
    [InlineData(new[] { 0, 3, 25, HighlightOrange, 0, 0, 0 }, new uint[] { 8 }, "normal_furni")]
    [InlineData(new[] { 0, 3, 50, HighlightOrange, 0, 0, 0 }, new uint[] { 3, 8 }, "normal_furni")]
    [InlineData(new[] { 0, 3, 50, 255, 0, 0, 0 }, new uint[] { 8 }, "invalid_color")]
    [InlineData(new[] { 0, 3, 50, White, 0, 0, 0 }, new uint[] { 8 }, "invalid_color")]
    [InlineData(new[] { 0, 3, 7, HighlightOrange, 0, 0, 0 }, new uint[] { 8 }, "invalid_strength")]
    // Brighten on a normal furni is refused for its steady animation first.
    [InlineData(new[] { 0, 4, 38, White, 0, 0, 0 }, new uint[] { 8 }, "pulse_only")]
    public async Task ASteadyColourTintOfNormalFurniIsRefused(int[] ints, uint[] picks, string key)
    {
        var f = new MovementOracleSession(Highlight, buildersClubPicks: [3]);

        await SaveEffect(f, FurniVisualSave(ints, picks.Select(pick => (int)pick).ToArray()));

        AssertSaved(f, false, HighlightKey + key);
    }

    [Theory]
    [InlineData(new[] { 0, 3, 50, HighlightOrange, 0, 0, 0 }, new[] { 3 }, new[] { 100 })]
    [InlineData(new[] { 0, 3, 50, HighlightOrange, 0, 0, 0 }, new int[0], new[] { 100 })]
    // Pulsing, users as targets and a furni selector are not checked.
    [InlineData(new[] { 0, 3, 50, HighlightOrange, 1, 0, 0 }, new[] { 8 }, new[] { 100 })]
    [InlineData(new[] { 0, 3, 50, HighlightOrange, 0, 0, 1 }, new[] { 8 }, new[] { 100 })]
    [InlineData(new[] { 0, 3, 50, HighlightOrange, 0, 0, 0 }, new[] { 8 }, new[] { 200 })]
    public async Task OtherColourTintsAreAccepted(int[] ints, int[] picks, int[] furniSources)
    {
        var f = new MovementOracleSession(Highlight, buildersClubPicks: [3]);

        await SaveEffect(f, FurniVisualSave(ints, picks, furniSources: furniSources));

        AssertSaved(f, true, HighlightKey + "normal_furni");
    }

    // A save is stored with the hide-editor reply, or refused with one key and no parameters.
    private static void AssertSaved(MovementOracleSession f, bool saved, string refusal)
    {
        if (saved) {
            Assert.Contains(f.Client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
            Assert.Single(f.Store.Saves);

            return;
        }

        Assert.Empty(f.Store.Saves);
        Assert.Equal(refusal, RefusalKey(f));
    }
}
