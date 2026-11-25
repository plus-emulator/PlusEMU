using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms.Chat.Commands.Moderator;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public sealed class StaffCommandParameterTests
{
    [Theory]
    [InlineData("credits", "-10")]
    [InlineData("credits", "0")]
    [InlineData("duckets", "-10")]
    [InlineData("duckets", "0")]
    [InlineData("diamonds", "-10")]
    [InlineData("gotw", "-10")]
    [InlineData("1001", "-10")]
    public async Task GiveRefusesAnAmountBelowOne(string type, string amount)
    {
        var (actor, sent) = Giver(100);

        await new GiveCommand().Execute(actor.Client!, null!, actor, [type, amount]);

        Assert.Equal(100, Balance(actor, type));
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData("credits")]
    [InlineData("duckets")]
    [InlineData("diamonds")]
    [InlineData("gotw")]
    [InlineData("1001")]
    public async Task GiveRefusesAnAmountThatOverflowsTheBalance(string type)
    {
        var (actor, sent) = Giver(4870);

        await new GiveCommand().Execute(actor.Client!, null!, actor, [type, int.MaxValue.ToString()]);

        Assert.Equal(4870, Balance(actor, type));
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData("credits")]
    [InlineData("duckets")]
    [InlineData("diamonds")]
    [InlineData("gotw")]
    [InlineData("1001")]
    public async Task GiveAcceptsAnAmountThatFillsTheBalanceExactly(string type)
    {
        var (actor, _) = Giver(int.MaxValue - 5);

        await new GiveCommand().Execute(actor.Client!, null!, actor, [type, "5"]);

        Assert.Equal(int.MaxValue, Balance(actor, type));
    }

    [Theory]
    [InlineData]
    [InlineData("-50")]
    [InlineData("0")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("soon")]
    public async Task MuteRefusesAMissingOrInvalidTimeBeforeTheDatabase(params string[] parameters)
    {
        var actor = new Habbo { Id = 1, Username = "actor", Access = EditorTestSupport.Access([PermissionKeys.ModerationMuteLimitOverride], 90) };
        var target = new Habbo { Id = 2, Username = "target", Access = EditorTestSupport.Access([], 50) };
        var (session, _) = HabbiconTestSupport.Client(actor);
        var (targetSession, targetSent) = HabbiconTestSupport.Client(target);
        target.Client = targetSession;

        await new MuteCommand(EditorTestSupport.UntouchableDatabase()).Execute(session, null!, target, parameters);

        Assert.Equal(0, target.TimeMuted);
        Assert.Empty(targetSent);
    }

    private static (Habbo Actor, List<(uint Header, byte[] Payload)> Sent) Giver(int balance)
    {
        var actor = new Habbo
        {
            Id = 1,
            Username = "actor",
            Access = EditorTestSupport.Access([PermissionKeys.CommandGiveCoins, PermissionKeys.CommandGivePixels, PermissionKeys.CommandGiveDiamonds, PermissionKeys.CommandGiveGotw], 50),
            Credits = balance,
            Duckets = balance,
            Diamonds = balance,
            GotwPoints = balance
        };
        actor.Currencies[ActivityPointType.Emerald] = balance;
        var (session, sent) = HabbiconTestSupport.Client(actor);
        actor.Client = session;

        return (actor, sent);
    }

    private static int Balance(Habbo habbo, string type) => type switch
    {
        "credits" => habbo.Credits,
        "duckets" => habbo.Duckets,
        "diamonds" => habbo.Diamonds,
        "gotw" => habbo.GotwPoints,
        _ => habbo.Currencies[int.Parse(type)]
    };
}
