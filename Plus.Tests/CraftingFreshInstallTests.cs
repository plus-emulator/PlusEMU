using System.Text.RegularExpressions;
using Xunit;

namespace Plus.Tests;

public sealed class CraftingFreshInstallTests
{
    private static readonly string[] Tables =
    [
        "crafting_jarhearts", "easter_c23_bunnycraftbot", "easter_c24_craftingtable", "fantasy_c22_craftsman", "fest_c19_craft",
        "graffiti_c24_craftingtable", "hween_c15_altar", "hween_c19_witchcraft", "hween_c21_scrollaltar", "hween_c23_bastetcraft",
        "india_c20_sewingmachine", "jungle_c16_worktable", "plushie_c20_fluffmachine", "stellar_c23_mooncraftingtable", "tokyo_c18_craftbot",
        "xmas_c19_snowcraft", "xmas_c20_crafter", "xmas_c22_penguincrafting", "xmas_c22_reindeercrafting", "xmas_c23_chocolatecraftingtable",
        "survival_c26_craftingtable"
    ];

    [Fact]
    public void TheFreshInstallGivesEveryCraftingTableTheCraftingTableInteraction()
    {
        var rows = Lines().Where(line => Tables.Any(table => line.Contains($",'{table}','", StringComparison.Ordinal)) && line.Contains(",'s',")).ToList();

        Assert.Contains(rows, row => row.Contains(",'survival_c26_craftingtable','"));
        Assert.Contains(rows, row => row.Contains(",'xmas_c19_snowcraft','"));
        Assert.All(rows, row => Assert.Contains(",'crafting_table',", row));
    }

    [Fact]
    public void TheFreshInstallHasTheSurvivalistAndSnowmanRecipesAndKeepsTheIngredientOrder()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var recipes = Section(sql, "crafting_recipes");

        Assert.Equal(15, Regex.Matches(recipes, @"\(\d+,'survival_[a-z0-9_]+','survival_[a-z0-9_]+',\d+,1,0,NULL,''\)").Count);
        Assert.Equal(5, Regex.Matches(recipes, @"\(\d+,'xmas_c19_[a-z]+2','xmas_c19_[a-z]+2',\d+,1,0,NULL,''\)").Count);
        Assert.Matches(@"`position` tinyint\(3\) unsigned NOT NULL DEFAULT 0", Table(sql, "crafting_recipes_ingredients"));
        var ingredients = Section(sql, "crafting_recipes_ingredients");
        Assert.Contains("(12,183,4,1)", ingredients);
        Assert.Contains("(12,8404,1,2)", ingredients);
    }

    private static IEnumerable<string> Lines() =>
        File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")).Split('\n').Where(line => line.StartsWith('('));

    private static string Section(string sql, string table)
    {
        var start = sql.IndexOf($"INSERT INTO `{table}` VALUES", StringComparison.Ordinal);

        return sql[start..sql.IndexOf(';', start)];
    }

    private static string Table(string sql, string table)
    {
        var start = sql.IndexOf($"CREATE TABLE `{table}` (", StringComparison.Ordinal);

        return sql[start..sql.IndexOf(';', start)];
    }
}
