using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Rooms.Chat.Commands.Administrator;
using Plus.HabboHotel.Rooms.Chat.Commands.Moderator;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // AIR WIN63-202610201032-107964540 reads one string for incoming 1090 (package_27/class_2572.as:23)
    // and a message plus a link for incoming 3187 (package_75/class_2713.as:42-43).
    private const uint AirBroadcastMessageAlert = 1090;
    private const uint AirModeratorMessage = 3187;

    [Fact]
    public async Task HotelAlertSendsOnlyTheMessageTheAirBroadcastAlertReads()
    {
        var (commands, sent) = HotelAlertCommands();

        Assert.True(await commands.Parse(_client, ":ha hello world"));

        Assert.Equal([(AirBroadcastMessageAlert, new object[] { "hello world\r\n- johndoe" })], sent.Select(Wire));
    }

    [Fact]
    public async Task HotelAlertWithLinkSendsTheAirAlertThatCarriesALink()
    {
        var (commands, sent) = HotelAlertCommands();

        Assert.True(await commands.Parse(_client, ":hal https://example.com hello world"));

        Assert.Equal([(AirModeratorMessage, new object[] { "hello world\r\n- johndoe", "https://example.com" })], sent.Select(Wire));
    }

    [Theory]
    [InlineData(":hal hello everyone")]
    [InlineData(":hal https://example.com")]
    [InlineData(":hal")]
    public async Task HotelAlertWithLinkSendsNothingWithoutALinkAndAMessage(string input)
    {
        var (commands, sent) = HotelAlertCommands();

        Assert.True(await commands.Parse(_client, input));

        Assert.Empty(sent);
        Assert.Equal("Please enter a link and a message: :hal %link% %message%. Use :ha for an alert without a link.", LastWhisper());
    }

    [Fact]
    public async Task CommandListShowsThatHotelAlertWithLinkTakesTheLinkFirst()
    {
        var (commands, _) = HotelAlertCommands();

        Assert.True(await commands.Parse(_client, ":commands"));

        var list = Encoding.UTF8.GetString(_client.Packets.Single(packet => packet.Header == ServerPacketHeader.MOTDNotificationComposer).Body);
        Assert.Contains(":hal %link% %message% - ", list);
    }

    private (CommandManager Commands, List<IServerPacket> Sent) HotelAlertCommands()
    {
        EnterAsOwner();
        _client.GetHabbo().Username = "johndoe";
        _client.GetHabbo().Access = UserAccess.Create([], [new(PermissionKeys.CommandHa, false), new(PermissionKeys.CommandHal, false)]);
        _client.Packets.Clear();
        var sent = new List<IServerPacket>();
        var clients = Proxy<IGameClientManager>((method, args) => method == nameof(IGameClientManager.SendPacket)
            ? RecordAlert(sent, (IServerPacket)args[0]!)
            : throw new InvalidOperationException(method));

        return (new CommandManager([new HotelAlertCommand(clients), new HalCommand(clients)], clients, _database, _interactionClock, NullLogger<CommandManager>.Instance), sent);
    }

    private static object? RecordAlert(List<IServerPacket> sent, IServerPacket packet)
    {
        sent.Add(packet);

        return null;
    }

    private static (uint Header, object[] Writes) Wire(IServerPacket packet)
    {
        var recording = new HabbiconTestSupport.RecordingPacket();
        packet.Compose(recording);

        return (packet.MessageId, recording.Writes.ToArray());
    }
}
