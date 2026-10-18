using Plus.Database.Migrator;
using Xunit;

namespace Plus.Tests;

public sealed class SchemaMigrationTests
{
    [Fact]
    public void SplitsOnSemicolonsOutsideQuotesAndComments()
    {
        var statements = SqlScript.Split("""
            -- a comment; not a statement
            INSERT INTO t VALUES ('a;b', "c;d", `e;f`); # trailing; comment
            /* block; comment */ UPDATE t SET v = 'it''s; \' fine' WHERE id = 1;
            SELECT 5--1;
            """);

        Assert.Equal(3, statements.Count);
        Assert.StartsWith("INSERT INTO t VALUES ('a;b', \"c;d\", `e;f`)", statements[0]);
        Assert.EndsWith(@"SET v = 'it''s; \' fine' WHERE id = 1", statements[1]);
        Assert.Equal("SELECT 5--1", statements[2]);
    }

    [Fact]
    public void HonoursDelimiterLinesForRoutines()
    {
        var statements = SqlScript.Split("""
            DELIMITER //
            CREATE PROCEDURE p()
            BEGIN
                SELECT 1;
                SELECT 2;
            END//
            DELIMITER ;
            CALL p();
            DROP PROCEDURE p;
            """);

        Assert.Equal(3, statements.Count);
        Assert.StartsWith("CREATE PROCEDURE p()", statements[0]);
        Assert.EndsWith("END", statements[0]);
        Assert.Contains("SELECT 2;", statements[0]);
        Assert.Equal("CALL p()", statements[1]);
    }

    [Fact]
    public void KeepsExecutableCommentsAndDropsCommentOnlyStatements()
    {
        var statements = SqlScript.Split("/*!40101 SET NAMES utf8mb4 */;\n/* nothing */;\n-- nothing\n;\n");

        Assert.Equal(new[] { "/*!40101 SET NAMES utf8mb4 */" }, statements);
    }

    [Fact]
    public void EveryShippedMigrationIsEmbeddedAndOrdered()
    {
        var scripts = MigrationScripts.Load(typeof(SchemaMigrator).Assembly);
        var files = Directory.GetFiles(HabbiconPacketTests.Repo("Database/Migrations"), "*.sql").Select(Path.GetFileName)
            .Concat(Directory.GetFiles(HabbiconPacketTests.Repo("Resources/SQLs/Updates"), "*.sql").Select(f => "Updates/" + Path.GetFileName(f)));

        Assert.Equal(files.Order(StringComparer.Ordinal), scripts.Select(s => s.Id).Order(StringComparer.Ordinal));
        Assert.Equal(scripts.Count, scripts.Select(s => s.Id).Distinct().Count());

        // New migrations follow every legacy one, by number.
        var added = scripts.SkipWhile(s => s.Probe != null).ToList();
        Assert.All(added, s => Assert.Null(s.Probe));
        Assert.Equal(added.Select(s => MigrationScripts.Number(s.Id)).Order(), added.Select(s => MigrationScripts.Number(s.Id)));
    }

    [Fact]
    public void ChecksumIgnoresLineEndings() =>
        Assert.Equal(MigrationScripts.Checksum("SELECT 1;\nSELECT 2;\n"), MigrationScripts.Checksum("SELECT 1;\r\nSELECT 2;\r\n"));
}
