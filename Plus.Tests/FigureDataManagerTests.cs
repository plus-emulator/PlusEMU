using Microsoft.Extensions.Logging.Abstractions;
using Plus.Core.FigureData;
using Xunit;

namespace Plus.Tests;

public class FigureDataManagerTests
{
    private static FigureDataManager Manager()
    {
        // Init reads Config/figuredata.xml, Habbo's figuredata, from the working directory (the test output folder).
        var manager = new FigureDataManager(NullLogger<FigureDataManager>.Instance);
        manager.Init();

        return manager;
    }

    [Theory]
    [InlineData("hd-180-1.ha-1018")]
    [InlineData("hd-180-1.ca-1813")]
    [InlineData("hd-180-1.wa-2001")]
    [InlineData("hd-180-1.ca-1813-")]
    [InlineData("hd-180-1.ca-1813-x")]
    [InlineData("hd-180-1.hr")]
    [InlineData("hd-180-1.hr-")]
    [InlineData("hd-180-1.hr-abc-45")]
    [InlineData("hd-180-1..ch-215-66")]
    public void MalformedPartsAreDroppedOrNormalisedInsteadOfThrowing(string figure)
    {
        var processed = Manager().ProcessFigure(figure, "M", null!, 2);

        Assert.Contains("hd-180-", processed);
        Assert.All(processed.TrimEnd('.').Split('.'), part => Assert.Matches(@"^[a-z]{2}-\d+-\d+(-\d+)?$", part));
    }

    [Fact]
    public void WellFormedFiguresKeepTheirParts()
    {
        var processed = Manager().ProcessFigure("hd-180-1.ch-215-66.lg-270-82.sh-290-80", "M", null!, 2);

        Assert.Contains("hd-180-1.", processed);
        Assert.Contains("ch-215-66.", processed);
    }

    [Fact]
    public void SellableSetsNeedTheirClothingRedeemed()
    {
        // ch-3438 and mc-3360 are sellable on Habbo; ch-215 is not.
        const string Look = "hd-180-1.ch-3438-66.lg-270-82.mc-3360-66";
        var unowned = Manager().ProcessFigure(Look, "M", [], 0);
        var owned = Manager().ProcessFigure(Look, "M", [new(1, 3438, "clothing_a"), new(2, 3360, "clothing_b")], 0);

        Assert.DoesNotContain("ch-3438-", unowned);
        Assert.DoesNotContain("mc-3360-", unowned);
        Assert.Contains("ch-3438-66.", owned);
        Assert.Contains("mc-3360-66.", owned);
        Assert.Contains("ch-215-66.", Manager().ProcessFigure("hd-180-1.ch-215-66.lg-270-82", "M", [], 0));
    }

    [Fact]
    public void CanonicalFiguresFollowSetTypeOrderAndKeepMissingColoursEmpty()
    {
        // Habbo stores the bot clothes figure in figuredata order and leaves "hr-100-" uncoloured.
        var processed = Manager().ProcessFigureCanonical("hd-180-1.ch-210-66.lg-270-82.sh-290-80.hr-100-", "M", null!, 2);

        Assert.Equal("hr-100.hd-180-1.ch-210-66.lg-270-82.sh-290-80.", processed);
    }

    [Theory]
    [InlineData("M")]
    [InlineData("F")]
    public void RandomFiguresAreWearableAndVaried(string gender)
    {
        var manager = Manager();
        var random = new Random(7);
        var figures = Enumerable.Range(0, 200).Select(_ => manager.GenerateRandomFigure(gender, random)).ToList();

        Assert.True(figures.Distinct().Count() > 190);
        Assert.All(figures, figure =>
        {
            var types = figure.Split('.').Select(part => part.Split('-')[0]).ToList();
            Assert.Contains("hd", types);
            Assert.Contains("ch", types);
            Assert.Contains("lg", types);

            // Validation keeps every generated set, so the client gets the outfit as generated.
            var kept = manager.ProcessFigure(figure, gender, null, 2).TrimEnd('.').Split('.').Select(SetOf).ToHashSet();
            Assert.All(figure.Split('.'), part => Assert.Contains(SetOf(part), kept));
        });
    }

    private static string SetOf(string part) => string.Join('-', part.Split('-').Take(2));

    // Bot Clothes saved by a male user are read back from the bot's UserChange.
    [Theory]
    [InlineData("hd-180-1", "hd-180-1.ch-210-1408.lg-270-1408.")]
    [InlineData("hr-100-61.hd-180-1.ch-210-66.lg-270-82.sh-290-80", "hr-100.hd-180-1.ch-210-66.lg-270-82.sh-290-80.")]
    [InlineData("zz-1-1.hd-180-1", "hd-180-1.ch-210-1408.lg-270-1408.")]
    [InlineData("lg-270-82.hd-180-1.hr-100-61", "hr-100.hd-180-1.ch-210-1408.lg-270-82.")]
    public void CanonicalFiguresFillMissingPartsAndDropColoursOfUncolourableSets(string figure, string stored)
    {
        Assert.Equal(stored, Manager().ProcessFigureCanonical(figure, "M", null!, 2));
    }
}
