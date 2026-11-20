using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Plus.Communication.Flash;
using Plus.Communication.Revisions;
using Plus.Communication.Packets.Incoming.Handshake;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public sealed class GameClientConnectionCheckTests
{
    [Fact]
    public void A_logged_in_client_gets_a_ping_every_interval()
    {
        var world = new World();
        var client = world.LoggedIn(1);

        world.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(0, client.Pings);

        world.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, client.Pings);

        world.Advance(TimeSpan.FromSeconds(15));
        Assert.Equal(1, client.Pings);

        world.Advance(TimeSpan.FromSeconds(15));
        Assert.Equal(2, client.Pings);
    }

    [Fact]
    public void A_client_with_three_unanswered_pings_is_disconnected_and_an_answering_client_stays()
    {
        var world = new World();
        var silent = world.LoggedIn(1);
        var answering = world.LoggedIn(2);

        for (var check = 0; check < 3; check++) {
            world.Advance(TimeSpan.FromSeconds(30));
            answering.Pong();
        }

        Assert.Equal(3, silent.Pings);
        Assert.False(silent.Disconnected);

        world.Advance(TimeSpan.FromSeconds(30));

        Assert.True(silent.Disconnected);
        Assert.Equal(3, silent.Pings);
        Assert.False(answering.Disconnected);
        Assert.Equal(4, answering.Pings);
    }

    [Fact]
    public void A_late_pong_keeps_the_client_and_starts_the_count_again()
    {
        var world = new World();
        var client = world.LoggedIn(1);

        for (var check = 0; check < 3; check++) {
            world.Advance(TimeSpan.FromSeconds(30));
        }

        world.Advance(TimeSpan.FromSeconds(29));
        client.Pong();
        world.Advance(TimeSpan.FromSeconds(1));

        Assert.False(client.Disconnected);
        Assert.Equal(4, client.Pings);

        world.Advance(TimeSpan.FromSeconds(30));
        world.Advance(TimeSpan.FromSeconds(30));
        Assert.False(client.Disconnected);

        world.Advance(TimeSpan.FromSeconds(30));
        Assert.True(client.Disconnected);
        Assert.Equal(6, client.Pings);
    }

    [Fact]
    public void A_client_that_always_answers_is_never_disconnected()
    {
        var world = new World();
        var client = world.LoggedIn(1);

        for (var check = 0; check < 20; check++) {
            world.Advance(TimeSpan.FromSeconds(30));
            client.Pong();
        }

        Assert.Equal(20, client.Pings);
        Assert.False(client.Disconnected);
    }

    [Fact]
    public void The_interval_and_the_limit_come_from_the_configuration()
    {
        var world = new World(new ConnectionCheckConfiguration { PingIntervalSeconds = 10, MaxUnansweredPings = 1 });
        var client = world.LoggedIn(1);

        world.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, client.Pings);
        Assert.False(client.Disconnected);

        world.Advance(TimeSpan.FromSeconds(10));
        Assert.True(client.Disconnected);
    }

    [Fact]
    public void Settings_below_one_fall_back_to_the_defaults()
    {
        var world = new World(new ConnectionCheckConfiguration { PingIntervalSeconds = 0, MaxUnansweredPings = 0 });
        var client = world.LoggedIn(1);

        world.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(0, client.Pings);

        for (var check = 0; check < 3; check++) {
            world.Advance(TimeSpan.FromSeconds(30));
        }

        Assert.Equal(3, client.Pings);
        Assert.False(client.Disconnected);

        world.Advance(TimeSpan.FromSeconds(30));
        Assert.True(client.Disconnected);
    }

    [Fact]
    public void A_connection_that_has_not_logged_in_is_not_pinged_or_disconnected()
    {
        var world = new World();
        var loading = new Probe(new Habbo { Id = 7, Username = "loading" });

        world.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(0, loading.Pings);
        Assert.False(loading.Disconnected);
    }

    [Fact]
    public void A_client_that_cannot_be_sent_to_does_not_stop_the_check_for_others()
    {
        var world = new World();
        var broken = world.LoggedIn(1);
        broken.Client.Revision = null;
        var client = world.LoggedIn(2);

        world.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(1, client.Pings);
    }

    [Fact]
    public void A_ping_that_fails_to_encode_does_not_count_as_unanswered()
    {
        var world = new World();
        var client = world.LoggedIn(1);
        var revision = client.Client.Revision;
        client.Client.Revision = null;

        for (var check = 0; check < 4; check++) {
            world.Advance(TimeSpan.FromSeconds(30));
        }

        client.Client.Revision = revision;
        world.Advance(TimeSpan.FromSeconds(30));
        Assert.False(client.Disconnected);
        Assert.Equal(1, client.Pings);
    }

    [Fact]
    public void A_slow_ping_sweep_leaves_a_full_interval_to_reply()
    {
        var world = new World(new ConnectionCheckConfiguration { PingIntervalSeconds = 1, MaxUnansweredPings = 1 });
        var client = world.LoggedIn(1);
        client.Client.SendCallback = _ =>
        {
            world.Clock.Advance(TimeSpan.FromSeconds(2));

            return false;
        };

        world.Advance(TimeSpan.FromSeconds(1));
        world.Advance(TimeSpan.Zero);

        Assert.False(client.Disconnected);
        world.Advance(TimeSpan.FromSeconds(1));
        Assert.True(client.Disconnected);
    }

    [Fact]
    public void A_slow_incoming_handler_leaves_time_to_process_a_pong()
    {
        var world = new World(new ConnectionCheckConfiguration { PingIntervalSeconds = 1, MaxUnansweredPings = 1 });
        var handling = new TaskCompletionSource();
        var server = CatalogSnapshotTestSupport.Proxy<IGameServer>((method, _) => method switch
        {
            "PacketReceived" => handling.Task,
            "ModifyOutgoingPacket" => true,
            _ => throw new NotSupportedException(method)
        });
        var client = new FlashGameClient(server, new FlashPacketFactory(), TestLogging.GameClient)
        {
            Revision = new RevisionsCache().InternalRevision
        };
        client.SetHabbo(new Habbo { Id = 1, Username = "busy" });
        world.Manager.RegisterClient(client, 1, "busy");

        world.Advance(TimeSpan.FromSeconds(1));
        byte[] frame = [0, 0, 0, 2, 5, 157];
        client.OnReceived(frame, 0, frame.Length);

        try {
            world.Advance(TimeSpan.FromSeconds(1));
            world.Advance(TimeSpan.FromSeconds(1));
            Assert.False(client.Closed.IsCancellationRequested);
        }
        finally {
            handling.SetResult();
        }
    }

    [Fact]
    public void A_closing_client_does_not_get_another_disconnect_request()
    {
        var world = new World();
        var client = world.LoggedIn(1);
        var requests = 0;
        client.Client.DisconnectRequested = () => requests++;

        for (var check = 0; check < 6; check++) {
            world.Advance(TimeSpan.FromSeconds(30));
        }

        Assert.Equal(1, requests);
    }

    private sealed class World
    {
        public readonly FakeTimeProvider Clock = new(DateTimeOffset.Parse("2026-11-20T12:00:00Z"));
        public readonly GameClientManager Manager;

        public World(ConnectionCheckConfiguration? configuration = null)
        {
            Manager = new GameClientManager(null!, NullLogger<GameClientManager>.Instance, Options.Create(configuration ?? new ConnectionCheckConfiguration()), Clock);
            Manager.OnCycle();
        }

        public Probe LoggedIn(int id)
        {
            var probe = new Probe(new Habbo { Id = id, Username = $"user{id}" });
            probe.Client.Id = Guid.NewGuid();
            Manager.RegisterClient(probe.Client, id, $"user{id}");

            return probe;
        }

        public void Advance(TimeSpan time)
        {
            Clock.Advance(time);
            Manager.OnCycle();
        }
    }

    private sealed class Probe
    {
        private readonly List<(uint Header, byte[] Payload)> _sent;

        public Probe(Habbo habbo)
        {
            (Client, _sent) = HabbiconTestSupport.Client(habbo);
            Client.DisconnectRequested = () => Disconnected = true;
        }

        public Plus.Communication.Flash.FlashGameClient Client { get; }

        public bool Disconnected { get; private set; }

        public int Pings => _sent.Count(packet => packet.Header == ServerPacketHeader.PingComposer);

        public void Pong() => new PongEvent().Parse(Client, null!).GetAwaiter().GetResult();
    }
}
