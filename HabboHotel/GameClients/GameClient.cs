using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using Plus.Communication.Abstractions;
using Plus.Communication.Encryption.Crypto.Prng;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.Communication.Revisions;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.GameClients;

public abstract class GameClient
{
    private static readonly AddressLogThrottle LimitWarnings = new(TimeProvider.System);

    private readonly IGameServer _server;
    private readonly IPacketFactory _packetFactory;
    private readonly object _sendLock = new();
    private readonly ILogger<GameClient> _logger;
    private readonly SemaphoreSlim _receiveLock = new(1, 1);
    private Habbo? _habbo;
    private readonly object _lifecycle = new();
    private readonly CancellationTokenSource _closed = new();
    private ITimer? _loginDeadline;
    private bool _loggingIn;
    private bool _loginDeadlinePassed;
    private long _receivedUnhandled;

    public RecyclableMemoryStream? _incompleteStream;
    public Arc4? Rc4Client { get; set; }
    private Arc4? _outgoingRc4;
    protected virtual bool SupportsLegacyCrypto => false;

    /// <summary>Whether this connection RC4-encrypts what it sends once the Diffie-Hellman handshake completes.</summary>
    public bool EncryptsOutgoingTraffic => SupportsLegacyCrypto;

    public bool IsAuthenticated { get; set; } = false;

    private string _machineId = string.Empty;

    [Obsolete("Will be removed")]
    public string MachineId
    {
        get => _machineId;
        set => _machineId = value;
    }

    // The handshake records the machine id without going through the deprecated property.
    public void RecordMachineId(string machineId) => _machineId = machineId;

    private int _pingCount;

    [Obsolete("Will be removed")]
    public int PingCount
    {
        get => _pingCount;
        set => _pingCount = value;
    }

    // A pong clears the ping counter without going through the deprecated property.
    public void ResetPingCount() => Interlocked.Exchange(ref _pingCount, 0);

    internal bool TryPing(int maxUnansweredPings)
    {
        if (Closed.IsCancellationRequested) {
            return true;
        }

        // Pongs wait behind the current receive handler, including room loads.
        if (_receiveLock.CurrentCount == 0) {
            ResetPingCount();

            return true;
        }

        if (Volatile.Read(ref _pingCount) >= maxUnansweredPings) {
            return false;
        }

        Send(new PingComposer(), countPing: true);

        return true;
    }

    // The factory sets the internal revision before ClientHello replaces it; a client built without one cannot speak the protocol.
    public Revision? Revision { get; set; }
    private int _revisionSelectionLogged;

    internal bool TryRecordRevisionSelection() => Interlocked.Exchange(ref _revisionSelectionLogged, 1) == 0;

    // True only when the supplied args have a pending operation that will raise Completed; unset until a transport attaches.
    internal Func<SocketAsyncEventArgs, bool>? SendCallback { get; set; }
    internal Action? DisconnectRequested { get; set; }

    public Guid Id { get; set; }

    /// <summary>Set by the transport once it knows where the connection comes from.</summary>
    public ClientAddress? RemoteAddress { get; internal set; }

    /// <summary>A connection that has not logged in this long after it opened is closed.</summary>
    internal static readonly TimeSpan LoginDeadline = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The most received data a connection may have waiting for the packet handler, and the most its transport may
    /// buffer: room for four of the largest game packets.
    /// </summary>
    internal const int ReceiveBufferLimit = 2 * 1024 * 1024;

    /// <summary>
    /// A received chunk that waits for the handler also holds its copy and async state, just under 512 bytes, so it
    /// counts this much against <see cref="ReceiveBufferLimit"/> on top of its data.
    /// </summary>
    private const int WaitingChunkOverhead = 512;

    /// <summary>The most a connection may have waiting to be sent. A client that leaves more unread is not reading.</summary>
    internal const int SendBufferLimit = 8 * 1024 * 1024;


    private Revision RequiredRevision => Revision ?? throw new InvalidOperationException("The client has no packet revision.");

    public void Disconnect()
    {
        Close();
        DisconnectRequested?.Invoke();
    }

    /// <summary>Closes a connection that went over a buffer limit, with a warning that names the address and the limit.</summary>
    internal void CloseOverLimit(string limit, IPAddress? peer = null)
    {
        var address = RemoteAddress?.Address ?? peer ?? IPAddress.None;

        if (!Closed.IsCancellationRequested && LimitWarnings.ShouldLog(address)) {
            _logger.LogWarning("Closed game connection {ConnectionId} from {Address}: it went over the {Limit} limit", Id, address, limit);
        }

        Disconnect();
    }

    /// <summary>Cancelled once the connection is closing, including after a packet timeout; session work should stop.</summary>
    internal CancellationToken Closed => _closed.Token;

    protected GameClient(IGameServer server, IPacketFactory packetFactory, ILogger<GameClient> logger)
    {
        _packetFactory = packetFactory;
        _server = server;
        _logger = logger;
    }


    internal event Action? CameraContextEnded;
    internal void EndCameraContext() => CameraContextEnded?.Invoke();

    /// <summary>
    /// NetCoreServer can raise a disconnect while it still holds this session's send lock, and logout takes room
    /// and Wired locks whose holders may be sending to this session. Clean up off the transport thread.
    /// </summary>
    internal void OnTransportDisconnected()
    {
        Close();
        ThreadPool.UnsafeQueueUserWorkItem(static client => client.OnDisconnected(), this, false);
    }

    internal void OnDisconnected()
    {
        Habbo? habbo;

        lock (_lifecycle) {
            Close();
            habbo = _habbo;
        }

        IsAuthenticated = false;
        EndCameraContext();

        try {
            habbo?.OnDisconnect();
        }
        catch (Exception exception) {
            _logger.LogError(exception, "Failed to clean up disconnected user {UserId}", habbo?.Id);
        }
    }

    /// <summary>
    /// Attaches a logged-in Habbo and registers the session as one step, unless the connection already closed.
    /// A close after this point finds the Habbo and logs it out.
    /// </summary>
    internal bool TryAttach(Habbo habbo, Action register)
    {
        lock (_lifecycle) {
            if (_closed.IsCancellationRequested) {
                return false;
            }

            SetHabbo(habbo);
            register();
            _loginDeadline?.Dispose();

            return true;
        }
    }

    internal void StartLoginDeadline(TimeProvider clock) =>
        _loginDeadline = clock.CreateTimer(static client => ((GameClient)client!).CloseUnlessLoggedIn(), this, LoginDeadline, Timeout.InfiniteTimeSpan);

    /// <summary>
    /// Holds the login deadline off until <see cref="EndLogin"/>, because a login uses up its ticket before it ends.
    /// False when the connection already closed.
    /// </summary>
    internal bool TryBeginLogin()
    {
        lock (_lifecycle) {
            if (_closed.IsCancellationRequested) {
                return false;
            }

            _loggingIn = true;

            return true;
        }
    }

    /// <summary>A login that ends without a Habbo after the deadline passed closes the connection.</summary>
    internal void EndLogin()
    {
        lock (_lifecycle) {
            _loggingIn = false;

            if (!_loginDeadlinePassed || _habbo != null) {
                return;
            }

            Close();
        }

        DisconnectRequested?.Invoke();
    }

    // Runs on a timer thread, where an escaping exception would end the process.
    private void CloseUnlessLoggedIn()
    {
        try {
            lock (_lifecycle) {
                if (_habbo != null) {
                    return;
                }

                if (_loggingIn) {
                    _loginDeadlinePassed = true;

                    return;
                }

                Close();
            }

            DisconnectRequested?.Invoke();
        }
        catch (Exception exception) {
            _logger.LogError(exception, "Closing connection {ConnectionId} at the login deadline failed", Id);
        }
    }

    private void Close()
    {
        lock (_lifecycle) {
            if (!_closed.IsCancellationRequested) {
                _closed.Cancel();
            }

            _loginDeadline?.Dispose();
        }
    }

    internal abstract (bool Complete, bool Malformed, uint MessageId, int HeaderLength, int Length) GetMessageIdAndPacketLength(ReadOnlyMemory<byte> buffer);
    /// <summary>
    /// Runs on a transport thread as async void, where an escaping exception ends the process. A receive that
    /// fails closes this connection only.
    /// </summary>
    internal virtual async void OnReceived(byte[] buffer, long offset, long size)
    {
        // Each chunk waits until the one before it is handled, so a slow handler must not let the queue grow without end.
        var cost = size + WaitingChunkOverhead;

        try {
            if (Interlocked.Add(ref _receivedUnhandled, cost) > ReceiveBufferLimit) {
                CloseOverLimit("receive queue");

                return;
            }

            await ReceiveAsync(buffer, offset, size);
        }
        catch (Exception exception) {
            _logger.LogError(exception, "Receive failed for connection {ConnectionId}; closing it", Id);

            try {
                Disconnect();
            }
            catch (Exception disconnectException) {
                _logger.LogError(disconnectException, "Failed to close connection {ConnectionId}", Id);
            }
        }
        finally {
            Interlocked.Add(ref _receivedUnhandled, -cost);
        }
    }

    private async Task ReceiveAsync(byte[] buffer, long offset, long size)
    {
        if (size > int.MaxValue) {
            throw new InvalidOperationException("");
        }

        // The transport reuses its receive buffer as soon as this method yields.
        var received = buffer.AsSpan((int)offset, (int)size).ToArray();
        await _receiveLock.WaitAsync();

        try {
            if (Closed.IsCancellationRequested) {
                _incompleteStream?.Dispose();
                _incompleteStream = null;

                return;
            }

            var decrypted = SupportsLegacyCrypto && Rc4Client != null;

            if (decrypted) {
                Rc4Client!.Transform(received);
            }

            await using var stream = PlusMemoryStream.GetStream(received);
            var memory = stream.GetBuffer().AsMemory().Slice(0, (int)stream.Length);

            if (_incompleteStream != null) {
                _incompleteStream.Position = _incompleteStream.Length;
                _incompleteStream.Write(memory.Span);
                memory = _incompleteStream.GetBuffer().AsMemory().Slice(0, (int)_incompleteStream.Length);
            }

            while (memory.Length > 0 && !Closed.IsCancellationRequested) {
                var (complete, malformed, messageId, headerLength, length) = GetMessageIdAndPacketLength(memory);

                if (malformed) {
                    Disconnect();
                    _incompleteStream?.Dispose();
                    _incompleteStream = null;

                    return;
                }

                if (!complete) {
                    break;
                }

                try {
                    if (RequiredRevision.IncomingIdToInternalIdMapping.TryGetValue(messageId, out var internalMessageId)) {
                        await using var packetStream = PlusMemoryStream.GetStream(memory.Slice(headerLength, length).Span);
                        await _server.PacketReceived(this, internalMessageId, _packetFactory.CreateIncomingPacket(packetStream));
                    }
                    else {
                        // TODO @80O: Add logging unknown packet received.
                    }
                }
                catch (Exception e) {
                    _logger.LogError(e, "Error handling packet {MessageId}", messageId);
                }

                memory = memory.Slice(headerLength + length);

                if (!decrypted && SupportsLegacyCrypto && Rc4Client != null && !memory.IsEmpty) {
                    Rc4Client.Transform(memory.Span);
                    decrypted = true;
                }
            }

            if (memory.Length == 0 || Closed.IsCancellationRequested) {
                _incompleteStream?.Dispose();
                _incompleteStream = null;
            }
            else {
                var tail = PlusMemoryStream.GetStream(memory.Span);
                _incompleteStream?.Dispose();
                _incompleteStream = tail;
            }
        }
        finally {
            _receiveLock.Release();
        }
    }

    public Habbo GetHabbo() => _habbo!;

    public void SetHabbo(Habbo habbo)
    {
        if (_habbo != null) {
            throw new InvalidOperationException();
        }

        _habbo = habbo;
        IsAuthenticated = true;
    }

    // The official hotel sends no such message, so a Flash socket session gets none.
    internal bool SkipsClientExtensions => _server is Plus.Communication.Flash.FlashServer;

    public void Send(IServerPacket composer) => Send(composer, countPing: false);

    private void Send(IServerPacket composer, bool countPing)
    {
        if (composer is Plus.Communication.Packets.Outgoing.IClientExtensionPacket && SkipsClientExtensions) {
            return;
        }

        var outgoingMessageId = RequiredRevision.InternalIdToOutgoingIdMapping[composer.MessageId];
        var encoded = EncodePacket(composer, outgoingMessageId);

        if (encoded == null) {
            return;
        }

        // Start waiting only once the packet is ready, before the transport can deliver a reply.
        if (countPing) {
            Interlocked.Increment(ref _pingCount);
        }

        SendEncoded(encoded);
        LogPacket(composer, outgoingMessageId);
    }

    // Encoding belongs to this broadcast only: composers can reference mutable room state.
    internal static void SendBroadcast(IServerPacket composer, IEnumerable<GameClient> clients, Func<GameClient, bool>? canSend = null)
    {
        var encodedPackets = new Dictionary<(Revision, IPacketFactory, Type, uint), byte[]>();

        foreach (var client in clients) {
            // A client without a revision cannot receive packets; it is skipped rather than aborting everyone else's copy.
            if (client.Revision is not { } revision || composer is Plus.Communication.Packets.Outgoing.IClientExtensionPacket && client.SkipsClientExtensions) {
                continue;
            }

            var outgoingMessageId = revision.InternalIdToOutgoingIdMapping[composer.MessageId];
            var key = (revision, client._packetFactory, client.GetType(), outgoingMessageId);
            byte[] buffer;

            if (client._server.HasOutgoingPacketInjectors(composer.MessageId) || !encodedPackets.TryGetValue(key, out buffer!)) {
                buffer = client.EncodePacket(composer, outgoingMessageId)!;

                if (buffer == null) {
                    continue;
                }

                if (!client._server.HasOutgoingPacketInjectors(composer.MessageId)) {
                    encodedPackets.Add(key, buffer);
                }
            }

            client.SendEncoded(buffer, canSend == null ? null : () => canSend(client));
            client.LogPacket(composer, outgoingMessageId);
        }
    }

    // One socket write per client: packets that belong together (a rider's and its horse's slides) cannot be
    // split across sends when the transport is slow to complete the first write.
    internal static void SendBroadcast(IReadOnlyList<IServerPacket> composers, IEnumerable<GameClient> clients)
    {
        foreach (var client in clients) {
            if (client.Revision is not { } revision) {
                continue;
            }

            var buffers = new List<(IServerPacket Composer, uint MessageId, byte[] Buffer)>(composers.Count);

            foreach (var composer in composers) {
                if (composer is Plus.Communication.Packets.Outgoing.IClientExtensionPacket && client.SkipsClientExtensions) {
                    continue;
                }

                var outgoingMessageId = revision.InternalIdToOutgoingIdMapping[composer.MessageId];

                if (client.EncodePacket(composer, outgoingMessageId) is { } buffer) {
                    buffers.Add((composer, outgoingMessageId, buffer));
                }
            }

            if (buffers.Count == 0) {
                continue;
            }

            client.SendEncoded(buffers.Count == 1 ? buffers[0].Buffer : buffers.SelectMany(entry => entry.Buffer).ToArray());

            foreach (var entry in buffers) {
                client.LogPacket(entry.Composer, entry.MessageId);
            }
        }
    }

    private byte[]? EncodePacket(IServerPacket composer, uint outgoingMessageId)
    {
        using var stream = PlusMemoryStream.GetStream();
        var packet = _packetFactory.CreateOutgoingPacket(stream);
        packet.MessageId = checked((int)composer.MessageId);
        composer.Compose(packet);

        if (!_server.ModifyOutgoingPacket(this, packet)) {
            return null;
        }

        var memory = stream.GetBuffer().AsMemory(0, (int)stream.Length);
        CreateHeader(memory, outgoingMessageId);

        // Socket.SendAsync can outlive this stream; never hand its pooled buffer to a send.
        return memory.ToArray();
    }

    public void ActivateLegacyCrypto(byte[] key)
    {
        if (!SupportsLegacyCrypto) {
            return;
        }

        lock (_sendLock) {
            Rc4Client = new Arc4(key);
            _outgoingRc4 = new Arc4(key);
        }
    }

    private void SendEncoded(byte[] buffer, Func<bool>? canSend = null)
    {
        if (!SupportsLegacyCrypto || _outgoingRc4 == null) {
            SendEncodedCore(buffer, canSend);

            return;
        }

        lock (_sendLock) {
            SendEncodedCore(buffer, canSend);
        }
    }

    private void SendEncodedCore(byte[] buffer, Func<bool>? canSend)
    {
        var args = new SocketAsyncEventArgs();
        args.SetBuffer(buffer.AsMemory());
        args.Completed += static (_, completed) => completed.Dispose();

        try {
            // Visit-scoped packets can expire during encoding or a prior recipient's synchronous send.
            // Recheck at the transport boundary, without holding a room/network lock across the callback.
            if (canSend != null && !canSend()) {
                args.Dispose();

                return;
            }

            if (SupportsLegacyCrypto && _outgoingRc4 != null) {
                buffer = buffer.ToArray();
                _outgoingRc4.Transform(buffer);
                args.SetBuffer(buffer.AsMemory());
            }

            // Without an attached transport nothing is pending, so the args are released like a completed send.
            if (SendCallback is not { } send || !send(args)) {
                args.Dispose();
            }
        }
        catch {
            args.Dispose();
            throw;
        }
    }

    private void LogPacket(IServerPacket composer, uint outgoingMessageId)
    {
        if (_logger.IsEnabled(LogLevel.Debug)) {
            _logger.LogDebug("Send Packet: {PacketType} (EmuId: {EmulatorId}, ClientId: {ClientId})", composer.GetType().Name, composer.MessageId, outgoingMessageId);
        }
    }

    public abstract void CreateHeader(Memory<byte> memory, uint messageId);
}
