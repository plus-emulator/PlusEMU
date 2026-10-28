using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms;
using Plus.Communication.Packets.Incoming.WiredVariables;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

// The Variable Management window's page request on Plus's own headers, through the raw packets in a real room. The fields come in the
// order of the AIR client's request: sort, then user type. The answer repeats them as user type, then sort.
public partial class PlacedFurniRoomTests
{
    private Task<(uint Header, byte[] Body)[]> VbHolderPage(bool exact, string id, int page, int size, int sort, int userType, TestClient? client = null)
    {
        var menus = new WiredVariableMenuService();

        return exact
            ? VbAsk(new WiredVariableHoldersPage64Event(menus), client ?? _client, 1, id, page, size, sort, userType)
            : VbAsk(new WiredVariableHoldersPageEvent(menus), client ?? _client, id, page, size, sort, userType);
    }

    private static (string Id, int Total, int Page, int Amount, string Users, int UserType, int Sort) VbReadHolderPage((uint Header, byte[] Body)[] packets, bool exact)
    {
        var packet = Assert.Single(packets);
        Assert.Equal(exact ? ServerPacketHeader.WiredVariableHoldersPage64Composer : ServerPacketHeader.WiredVariableHoldersPageComposer, packet.Header);
        var input = new FlashIncomingPacket { Buffer = packet.Body };

        if (exact) {
            Assert.Equal(1, input.ReadInt());
        }

        var (id, total, page, amount) = (input.ReadString(), input.ReadInt(), input.ReadInt(), input.ReadInt());
        var users = new List<int>();

        for (var count = input.ReadInt(); count > 0; count--) {
            Assert.Equal(1, input.ReadInt());
            users.Add(input.ReadInt());
            input.ReadString();
            input.ReadInt();

            if (exact) {
                input.ReadInt();
            }

            for (var time = 0; time < 2; time++) {
                input.ReadInt();
                input.ReadInt();
                input.ReadString();
            }
        }

        var result = (id, total, page, amount, string.Join(",", users), input.ReadInt(), input.ReadInt());
        Assert.False(input.HasDataRemaining());

        return result;
    }

    [Theory]
    [InlineData(false, 0, new[] { 7, VbOffline, LeaverId })]
    [InlineData(false, 1, new[] { LeaverId, VbOffline, 7 })]
    [InlineData(false, 2, new[] { 7, LeaverId, VbOffline })]
    [InlineData(false, 3, new[] { VbOffline, LeaverId, 7 })]
    [InlineData(false, 4, new[] { VbOffline, 7, LeaverId })]
    [InlineData(false, 5, new[] { LeaverId, 7, VbOffline })]
    [InlineData(true, 0, new[] { 7, VbOffline, LeaverId })]
    [InlineData(true, 1, new[] { LeaverId, VbOffline, 7 })]
    [InlineData(true, 2, new[] { 7, LeaverId, VbOffline })]
    [InlineData(true, 3, new[] { VbOffline, LeaverId, 7 })]
    [InlineData(true, 4, new[] { VbOffline, 7, LeaverId })]
    [InlineData(true, 5, new[] { LeaverId, 7, VbOffline })]
    public async Task HolderPageSortsByTheSortFieldAndEchoesUserTypeThenSort(bool exact, int sort, int[] users)
    {
        VbOwnersRoom();

        Assert.Equal(("user:502", 3, 1, 10, string.Join(",", users), -1, sort), VbReadHolderPage(await VbHolderPage(exact, "user:502", 1, 10, sort, -1), exact));
    }

    [Theory]
    [InlineData(false, -1, 3)]
    [InlineData(false, 1, 3)]
    [InlineData(false, 2, 0)]
    [InlineData(false, 4, 0)]
    [InlineData(true, -1, 3)]
    [InlineData(true, 1, 3)]
    [InlineData(true, 2, 0)]
    [InlineData(true, 4, 0)]
    public async Task HolderPageListsEveryHolderForAllTypesAndHabbosAndNobodyForPetsAndBots(bool exact, int userType, int total)
    {
        VbOwnersRoom();

        var page = VbReadHolderPage(await VbHolderPage(exact, "user:502", 1, 10, 0, userType), exact);
        Assert.Equal((total, total, userType, 0), (page.Total, page.Users.Split(",", StringSplitOptions.RemoveEmptyEntries).Length, page.UserType, page.Sort));
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(false, 0, 3)]
    [InlineData(false, 6, -1)]
    [InlineData(false, -1, -1)]
    [InlineData(true, 0, 0)]
    [InlineData(true, 0, 3)]
    [InlineData(true, 6, -1)]
    [InlineData(true, -1, -1)]
    public async Task HolderPageWithAnUnknownSortOrUserTypeIsDropped(bool exact, int sort, int userType)
    {
        VbOwnersRoom();

        Assert.Empty(await VbHolderPage(exact, "user:502", 1, 10, sort, userType));
    }
}
