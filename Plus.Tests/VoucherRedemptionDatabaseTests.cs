using Dapper;
using MySqlConnector;
using Plus.HabboHotel.Catalog.Vouchers;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public sealed class VoucherRedemptionDatabaseFactAttribute : FactAttribute
{
    public VoucherRedemptionDatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLUS_REFACTOR_TEST_CONNECTION_STRING"))) {
            Skip = "Set PLUS_REFACTOR_TEST_CONNECTION_STRING to a disposable task_refactor_tests_ database with the Plus user schema.";
        }
    }
}

[Collection("SharedDatabase")]
public sealed class VoucherRedemptionDatabaseTests : IDisposable
{
    private const int UserId = 935101;
    private readonly HabbiconDatabaseTests.TestDatabase _database;

    public VoucherRedemptionDatabaseTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("PLUS_REFACTOR_TEST_CONNECTION_STRING")!;

        if (!new MySqlConnectionStringBuilder(connectionString).Database.StartsWith("task_refactor_tests_", StringComparison.Ordinal)) {
            throw new InvalidOperationException("Voucher redemption tests require a disposable task_refactor_tests_ schema.");
        }

        _database = new(connectionString);
        Dispose();
        using var connection = _database.Connection();
        connection.Execute("INSERT INTO users (id,username,auth_ticket,credits) VALUES (@UserId,'voucher_probe','',10); " +
            "INSERT INTO catalog_vouchers (voucher,type,value,current_uses,max_uses,enabled) VALUES ('PROBE_CREDITS','credits',25,0,5,1),('PROBE_DUCKETS','duckets',5,0,5,1)",
            new { UserId });
    }

    public void Dispose()
    {
        using var connection = _database.Connection();
        connection.Execute("DELETE FROM user_currencies WHERE user_id=@UserId; DELETE FROM users WHERE id=@UserId; " +
            "DELETE FROM catalog_vouchers WHERE voucher IN ('PROBE_CREDITS','PROBE_DUCKETS')", new { UserId });
    }

    [VoucherRedemptionDatabaseFact]
    public void CreditVoucherCommitsTheRewardWithTheClaim()
    {
        var habbo = new Habbo { Id = UserId, Credits = 10 };

        Redeem(habbo, new Voucher("PROBE_CREDITS", "credit", 25, 0, 5));

        using var connection = _database.Connection();
        Assert.Equal(35, connection.ExecuteScalar<int>("SELECT credits FROM users WHERE id=@UserId", new { UserId }));
        Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_vouchers WHERE user_id=@UserId AND voucher='PROBE_CREDITS'", new { UserId }));
    }

    [VoucherRedemptionDatabaseFact]
    public void DucketVoucherCommitsTheRewardWithTheClaim()
    {
        var habbo = new Habbo { Id = UserId, Duckets = 3 };

        Redeem(habbo, new Voucher("PROBE_DUCKETS", "ducket", 5, 0, 5));

        using var connection = _database.Connection();
        Assert.Equal(8, UserCurrencyStore.Get(connection, UserId, ActivityPointType.Duckets));
        Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_vouchers WHERE user_id=@UserId AND voucher='PROBE_DUCKETS'", new { UserId }));
    }

    private void Redeem(Habbo habbo, Voucher voucher)
    {
        var (client, _) = HabbiconTestSupport.Client(habbo);
        var vouchers = CatalogSnapshotTestSupport.Proxy<IVoucherManager>((method, args) =>
        {
            if (method != "TryGetVoucher") {
                throw new InvalidOperationException(method);
            }

            args[1] = voucher;

            return (string)args[0]! == voucher.Code;
        });

        new VoucherRedemptionService(vouchers, new VoucherClaimStore(_database)).Redeem(client, voucher.Code);
    }
}
