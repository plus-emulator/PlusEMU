using System.Data;
using Dapper;
using MySqlConnector;
using Plus.Database;
using Plus.HabboHotel.Crafting;
using Xunit;

namespace Plus.Tests;

public sealed class CraftingDatabaseTests
{
    [CraftingDatabaseFact]
    public void CraftingConsumesExactInventoryRowsAndAtomicallyCreatesTheRewardAndDiscovery()
    {
        using var fixture = new Fixture();
        var store = fixture.Store;
        var recipe = Assert.Single(store.Load(100, 7));
        Assert.True(recipe.Secret);
        Assert.False(recipe.Discovered);
        Assert.Equal([(101u, 2)], recipe.Ingredients.Select(row => (row.ItemId, row.Amount)));
        Assert.Null(store.Craft(7, 42, 10, recipe, [20, 20], true));
        Assert.Null(store.Craft(7, 42, 10, recipe, [20, 22], true)); // Foreign owner.
        Assert.Null(store.Craft(7, 42, 10, recipe, [20, 23], true)); // Already placed.
        Assert.Null(store.Craft(7, 43, 10, recipe, [20, 21], true)); // Foreign room.
        Assert.Null(store.Craft(8, 42, 10, recipe, [22, 24], true)); // Foreign altar.
        Assert.Null(store.Craft(7, 42, 10, recipe, [20, 21], false)); // Undiscovered secret.
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));
        Assert.Empty(fixture.Connection.Query<int>("SELECT recipe_id FROM user_crafting_recipes"));

        var crafted = Assert.IsType<CraftedItem>(store.Craft(7, 42, 10, recipe, [21, 20], true));
        Assert.True(crafted.Discovered);
        Assert.Equal((7, 0u, 102u), fixture.Connection.QuerySingle<(int, uint, uint)>(
            "SELECT user_id,room_id,base_item FROM items WHERE id=@id", new { id = crafted.ItemId }));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT remaining FROM crafting_recipes WHERE id=1"));
        Assert.True(Assert.Single(store.Load(100, 7)).Discovered);
        Assert.Null(store.Craft(7, 42, 10, recipe, [20, 21], true));
        fixture.Connection.Execute("INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES(30,7,0,101,''),(31,7,0,101,'')");
        var repeated = Assert.IsType<CraftedItem>(store.Craft(7, 42, 10, recipe, [30, 31], false));
        Assert.False(repeated.Discovered);
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_crafting_recipes WHERE user_id=7 AND recipe_id=1"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT remaining FROM crafting_recipes WHERE id=1"));
    }

    [CraftingDatabaseFact]
    public void PersistedLimitedIngredientsRejectStaleSelectionsWithoutConsumingStockOrDiscovery()
    {
        using var fixture = new Fixture();
        var recipe = Assert.Single(fixture.Store.Load(100, 7));

        foreach (var column in new[] { "limited_number", "limited_stack" }) {
            fixture.Connection.Execute($"UPDATE items SET {column}=1 WHERE id=20");
            Assert.Null(fixture.Store.Craft(7, 42, 10, recipe, [20, 21], true));
            Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));
            Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT remaining FROM crafting_recipes WHERE id=1"));
            Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_crafting_recipes"));
            Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE base_item=102"));
            fixture.Connection.Execute($"UPDATE items SET {column}=0 WHERE id=20");
        }
    }

    [CraftingDatabaseFact]
    public void FailedOutputInsertRollsBackIngredientsStockAndDiscovery()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("""
            CREATE TRIGGER reject_crafting_reward BEFORE INSERT ON items FOR EACH ROW
            BEGIN
                IF NEW.base_item=102 THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced output failure';
                END IF;
            END
            """);
        var recipe = Assert.Single(fixture.Store.Load(100, 7));
        Assert.Throws<MySqlException>(() => fixture.Store.Craft(7, 42, 10, recipe, [20, 21], true));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT remaining FROM crafting_recipes WHERE id=1"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_crafting_recipes"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE base_item=102"));
    }

    [CraftingDatabaseFact]
    public async Task ConcurrentOwnersCannotBothConsumeTheLastLimitedRecipe()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("UPDATE crafting_recipes SET remaining=1; INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES(11,8,42,100,'')");
        var recipe = Assert.Single(fixture.Store.Load(100, 7));
        using var start = new Barrier(2);
        var results = await Task.WhenAll(Task.Run(() => { start.SignalAndWait(); return fixture.Store.Craft(7, 42, 10, recipe, [20, 21], true); }),
            Task.Run(() => { start.SignalAndWait(); return fixture.Store.Craft(8, 42, 11, recipe, [22, 24], true); }));
        Assert.Single(results, result => result is not null);
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE base_item=102"));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21,22,24)"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_crafting_recipes"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT remaining FROM crafting_recipes"));
    }

    [CraftingDatabaseFact]
    public void PortableSeedResolvesOnlyCompleteUniqueFurnitureAndPreservesConfiguredCodes()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("""
            INSERT INTO furniture(id,item_name,sprite_id) VALUES
            (900,'hween_c15_altar',8388),(901,'clothing_firehelm',8342),(902,'hween_c15_purecrystal3',8373),
            (903,'hween_c15_purecrystal2',8363),(904,'hween_c15_purecrystal2',8363),(905,'clothing_airhelm',8340);
            INSERT INTO crafting_recipes(code,product_code,reward_item_id,secret,remaining) VALUES('clothing_airhelm','custom',999,TRUE,4);
            """);
        var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/45_Crafting.sql"));
        fixture.Connection.Execute(migration);
        var seeded = Assert.Single(fixture.Store.Load(900, 7));
        Assert.Equal("clothing_firehelm", seeded.Code);
        Assert.Equal(901u, seeded.RewardId);
        Assert.Equal(new CraftingIngredient(902, 4), Assert.Single(seeded.Ingredients));
        Assert.False(seeded.Secret);
        Assert.Equal(("custom", 999u, 4), fixture.Connection.QuerySingle<(string, uint, int)>(
            "SELECT product_code,reward_item_id,remaining FROM crafting_recipes WHERE code='clothing_airhelm'"));
        fixture.Connection.Execute(migration);
        Assert.Equal(3, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM crafting_recipes"));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM crafting_recipes_ingredients"));
    }

    [CraftingDatabaseFact]
    public void InterruptedSeedDoesNotLeaveAPartialRecipeAndTheSameConnectionCanRetry()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("""
            INSERT INTO furniture(id,item_name,sprite_id) VALUES
            (900,'hween_c15_altar',8388),(901,'clothing_firehelm',8342),(902,'hween_c15_purecrystal3',8373);
            CREATE TRIGGER reject_seed_ingredient BEFORE INSERT ON crafting_recipes_ingredients FOR EACH ROW
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced seed interruption';
            """);
        var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/45_Crafting.sql"));
        Assert.Throws<MySqlException>(() => fixture.Connection.Execute(migration));
        fixture.Connection.Execute("ROLLBACK");
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM crafting_recipes"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM crafting_altars_recipes"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM crafting_recipes_ingredients"));
        fixture.Connection.Execute("DROP TRIGGER reject_seed_ingredient");
        fixture.Connection.Execute(migration);
        Assert.Equal("clothing_firehelm", Assert.Single(fixture.Store.Load(900, 7)).Code);
        fixture.Connection.Execute(migration);
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM crafting_recipes"));
    }

    private const string ParityMigration = "Database/Migrations/176_CraftingHabboParity.sql";

    [CraftingDatabaseFact]
    public void IngredientsLoadInTheirRecipeOrderNotByFurnitureId()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("""
            INSERT INTO crafting_recipes(id,code,product_code,reward_item_id) VALUES(2,'ordered','ordered',102);
            INSERT INTO crafting_altars_recipes VALUES(100,2);
            INSERT INTO crafting_recipes_ingredients(recipe_id,item_id,amount,position) VALUES(2,103,2,2),(2,105,1,1),(2,104,1,3);
            """);

        var recipe = Assert.Single(fixture.Store.Load(100, 7), recipe => recipe.Code == "ordered");

        Assert.Equal([105u, 103u, 104u], recipe.Ingredients.Select(ingredient => ingredient.ItemId));
        Assert.Equal([105u, 103u, 104u], fixture.Store.Find("ordered", 7)!.Ingredients.Select(ingredient => ingredient.ItemId));
    }

    [CraftingDatabaseFact]
    public void TheParityMigrationAddsTheSurvivalistAndSnowmanRecipesInHabboOrder()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("INSERT INTO furniture(id,item_name,sprite_id) VALUES " + string.Join(',', SurvivalAndSnowman
            .Select((row, index) => $"({1000 + index},'{row.Name}',{row.Sprite})")));
        fixture.Connection.Execute("INSERT INTO furniture(id,item_name,sprite_id,interaction_type) VALUES(1100,'xmas_c20_crafter',1,'default'),(1101,'jungle_c16_worktable',2,'vendingmachine')");
        var migration = File.ReadAllText(HabbiconPacketTests.Repo(ParityMigration));

        fixture.Connection.Execute(migration);
        fixture.Connection.Execute(migration);

        var table = Id("survival_c26_craftingtable");
        var snowman = Id("xmas_c19_snowcraft");
        Assert.Equal(["crafting_table", "crafting_table", "crafting_table", "vendingmachine"], fixture.Connection.Query<string>(
            "SELECT interaction_type FROM furniture WHERE id IN (@table,@snowman,1100,1101) ORDER BY id", new { table, snowman }));
        var survival = fixture.Store.Load(table, 7);
        Assert.Equal(15, survival.Length);
        Assert.All(survival, recipe => Assert.False(recipe.Secret));
        Assert.All(survival, recipe => Assert.Equal(recipe.Code, recipe.ProductCode));
        Assert.Equal([(Id("survival_c26_stream"), 1), (Id("survival_c26_sleepingbag"), 2), (Id("survival_c26_foodsupplies"), 1)],
            survival.Single(recipe => recipe.Code == "survival_c26_cave").Ingredients.Select(row => (row.ItemId, row.Amount)));
        Assert.Equal([(Id("survival_cloth_c26_survivalmask"), 1), (Id("survival_c26_flare"), 1), (Id("survival_c26_rope"), 1)],
            survival.Single(recipe => recipe.Code == "survival_cloth_c26_survivaljumpsuit").Ingredients.Select(row => (row.ItemId, row.Amount)));
        Assert.Equal(Id("survival_c26_cave"), survival.Single(recipe => recipe.Code == "survival_c26_cave").RewardId);
        var snow = fixture.Store.Load(snowman, 7);
        Assert.Equal(["xmas_c19_angelfigure2", "xmas_c19_dragonfigure2", "xmas_c19_reindeerfigure2", "xmas_c19_robinfigure2", "xmas_c19_unicornfigure2"],
            snow.Select(recipe => recipe.Code).Order(StringComparer.Ordinal));
        Assert.Equal([(Id("xmas_c19_angelfigure"), 5), (Id("xmas_c19_jarofgold"), 2)],
            snow.Single(recipe => recipe.Code == "xmas_c19_angelfigure2").Ingredients.Select(row => (row.ItemId, row.Amount)));
        Assert.Equal(21, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM crafting_recipes"));
        Assert.Equal(1 + 34 + 8, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM crafting_recipes_ingredients"));
    }

    [CraftingDatabaseFact]
    public void TheParityMigrationFixesTheAltarRecipesAndAddsTheMissingOnes()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("""
            INSERT INTO furniture(id,item_name,sprite_id) VALUES
            (900,'hween_c15_altar',8388),(901,'hween14_skelepieces',6178),(902,'deadduck',208),(903,'hween_c15_purecrystal1',8404),
            (904,'hween_c15_purecrystal2',8363),(905,'duck_afro',5002),(906,'hween13_bldtrail',5300);
            """);
        fixture.Connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/45_Crafting.sql")));
        fixture.Connection.Execute("""
            INSERT INTO furniture(id,item_name,sprite_id) VALUES(907,'hween14_doll3',6203);
            """);
        var migration = File.ReadAllText(HabbiconPacketTests.Repo(ParityMigration));

        fixture.Connection.Execute(migration);
        fixture.Connection.Execute(migration);

        Assert.Equal("crafting_table", fixture.Connection.ExecuteScalar<string>("SELECT interaction_type FROM furniture WHERE id=900"));
        var altar = fixture.Store.Load(900, 7);
        Assert.Equal([(902u, 4), (903u, 1)], altar.Single(recipe => recipe.Code == "hween14_skelepieces").Ingredients.Select(row => (row.ItemId, row.Amount)));
        Assert.Equal([(902u, 2), (903u, 1)], altar.Single(recipe => recipe.Code == "hween13_bldtrail").Ingredients.Select(row => (row.ItemId, row.Amount)));
        var doll = altar.Single(recipe => recipe.Code == "hween14_doll3");
        Assert.True(doll.Secret);
        Assert.Equal(907u, doll.RewardId);
        Assert.Equal([(901u, 3), (905u, 1), (904u, 1)], doll.Ingredients.Select(row => (row.ItemId, row.Amount)));
    }

    private static readonly (string Name, int Sprite)[] SurvivalAndSnowman =
    [
        ("survival_c26_craftingtable", 19220), ("survival_c26_cave", 19219), ("survival_c26_stream", 19237), ("survival_c26_sleepingbag", 19235),
        ("survival_c26_foodsupplies", 19229), ("survival_c26_factionsymbols", 19223), ("survival_c26_stonesymbol", 19236), ("survival_c26_flare", 19225),
        ("survival_c26_firekit", 19224), ("survival_c26_foliage", 19227), ("survival_c26_rope", 19234), ("survival_c26_foliagefloor", 19228),
        ("survival_c26_hammock", 19230), ("survival_c26_podium", 19233), ("survival_c26_surveillancetree", 19240), ("survival_c26_treebridge", 19242),
        ("survival_c26_trap", 19241), ("survival_cloth_c26_braid", 19246), ("survival_cloth_c26_drone", 19248),
        ("survival_cloth_c26_survivaljumpsuit", 19250), ("survival_cloth_c26_survivalmask", 19251),
        ("xmas_c19_snowcraft", 10911), ("xmas_c19_angelfigure", 10884), ("xmas_c19_angelfigure2", 10934), ("xmas_c19_jarofgold", 10881),
        ("xmas_c19_dragonfigure", 10930), ("xmas_c19_dragonfigure2", 10915), ("xmas_c19_reindeerfigure", 10907), ("xmas_c19_reindeerfigure2", 10897),
        ("xmas_c19_robinfigure", 10902), ("xmas_c19_robinfigure2", 10933), ("xmas_c19_unicornfigure", 10940), ("xmas_c19_unicornfigure2", 10929)
    ];

    private static uint Id(string name) => (uint)(1000 + Array.FindIndex(SurvivalAndSnowman, row => row.Name == name));

    private sealed class Fixture : IDisposable
    {
        private readonly MySqlConnection _admin;
        private readonly string _schema = "task_crafting_" + Guid.NewGuid().ToString("N");
        public MySqlConnection Connection { get; }
        public CraftingStore Store { get; }

        public Fixture()
        {
            var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CRAFTING_DATABASE")!)
            { Database = "information_schema", Pooling = false, AllowZeroDateTime = true, ConvertZeroDateTime = true };
            _admin = new MySqlConnection(options.ConnectionString);
            _admin.Open();
            _admin.Execute($"CREATE DATABASE `{_schema}`");
            options.Database = _schema;
            Connection = new MySqlConnection(options.ConnectionString);
            Store = new CraftingStore(new Database(options.ConnectionString));

            try {
                Connection.Open();
                var dump = File.ReadAllText(HabbiconPacketTests.Repo("Resources/SQLs/Original Database.sql"));
                var start = dump.IndexOf("CREATE TABLE `items` (", StringComparison.Ordinal);
                Connection.Execute(dump[start..(dump.IndexOf(';', start) + 1)]);
                start = dump.IndexOf("CREATE TABLE `furniture` (", StringComparison.Ordinal);
                Connection.Execute(dump[start..(dump.IndexOf(';', start) + 1)]);
                Connection.Execute("CREATE TABLE users(id INT PRIMARY KEY); INSERT INTO users VALUES(7),(8)");
                Connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/45_Crafting.sql")));
                Connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo(ParityMigration)));
                Connection.Execute("""
                    INSERT INTO crafting_recipes(id,code,product_code,reward_item_id,secret,remaining) VALUES(1,'test_recipe','test_product',102,TRUE,2);
                    INSERT INTO crafting_altars_recipes VALUES(100,1);
                    INSERT INTO crafting_recipes_ingredients(recipe_id,item_id,amount) VALUES(1,101,2);
                    INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES
                    (10,7,42,100,''),(20,7,0,101,''),(21,7,0,101,''),(22,8,0,101,''),(23,7,42,101,''),(24,8,0,101,'');
                    """);
            }
            catch {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Connection.Dispose();
            _admin.Execute($"DROP DATABASE IF EXISTS `{_schema}`");
            _admin.Dispose();
        }

        private sealed class Database(string connectionString) : IDatabase
        {
            public bool IsConnected() => true;
            public IDbConnection Connection() => new MySqlConnection(connectionString);
        }
    }
}

public sealed class CraftingDatabaseFactAttribute : FactAttribute
{
    public CraftingDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CRAFTING_DATABASE")!)) {
            Skip = "Set CRAFTING_DATABASE to run the isolated crafting database regression.";
        }
    }
}
