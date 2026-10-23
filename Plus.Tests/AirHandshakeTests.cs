using System.Buffers.Binary;
using System.Globalization;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Plus.Communication.Encryption;
using Plus.Communication.Encryption.Keys;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming.Handshake;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Revisions;
using Plus.HabboHotel.GameClients;
using Xunit;
using SystemBigInteger = System.Numerics.BigInteger;

namespace Plus.Tests;

/// <summary>Runs the Diffie-Hellman, RSA and RC4 handshake against the AIR client's own algorithm, reimplemented here.</summary>
[Collection(nameof(AirHandshakeTests))]
public class AirHandshakeTests
{
    private const uint InitDiffieId = 3371;
    private const uint SecretKeyId = 137;
    private const uint TestComposerId = 20;
    private const uint TestIncomingId = 1;

    private static readonly HandshakeRsaKey Key = HandshakeRsaKey.FromPem(HandshakeRsaKey.GeneratePem());

    [Fact]
    public void GeneratedKeyHasTheExponentAndSizeTheAirClientUses()
    {
        Assert.True(Key.UsesAirExponent);
        Assert.Equal(2048, Key.ModulusBits);
        Assert.Equal(256, Key.BlockSize);
        Assert.Equal(512, Key.ModulusHex.Length);
    }

    [Fact]
    public async Task FullHandshakeMatchesTheAirClientBothWays()
    {
        HabboEncryptionV2.Initialize(Key);
        var client = new AirClient(Key.ModulusHex);

        // Different DH key pairs give shared keys with odd-length hex and leading zero nibbles; each one must line up.
        for (var round = 0; round < 48; round++) {
            await RunHandshake(client);
        }
    }

    [Fact]
    public void Rc4KeyIsTheClientsHexStringBytes()
    {
        // getSharedKey(16) is "abcde"; hexStringToByteArray pads it to "0abcde".
        Assert.Equal(new byte[] { 0x0A, 0xBC, 0xDE }, HabboEncryptionV2.Rc4KeyFromSharedKeyHex("ABCDE"));
        Assert.Equal(new byte[] { 0x01, 0x00 }, HabboEncryptionV2.Rc4KeyFromSharedKeyHex("100"));
        Assert.Equal(new byte[] { 0xFF, 0x00, 0x01 }, HabboEncryptionV2.Rc4KeyFromSharedKeyHex("FF0001"));
    }

    [Fact]
    public void SecretKeyOnlyAnnouncesServerEncryptionForConnectionsThatEncrypt()
    {
        var flash = new FlashGameClient(new FakeServer(), new FlashPacketFactory(), TestLogging.GameClient);

        Assert.True(flash.EncryptsOutgoingTraffic);
    }

    [Fact]
    public void ProviderReadsThePemFromTheEnvironmentFirst()
    {
        var pem = HandshakeRsaKey.GeneratePem();
        var provider = new HandshakeRsaKeyProvider(new EncryptionConfiguration { RsaPrivateKeyFile = "/nonexistent.pem" },
            name => name == EncryptionConfiguration.PrivateKeyVariable ? pem : null, NullLogger.Instance);

        Assert.True(provider.IsConfigured);
        Assert.Equal(HandshakeRsaKey.FromPem(pem).ModulusHex, provider.Key.ModulusHex);
    }

    [Fact]
    public void ProviderReadsTheConfiguredFile()
    {
        var path = Path.GetTempFileName();

        try {
            var pem = HandshakeRsaKey.GeneratePem();
            File.WriteAllText(path, pem);
            var provider = new HandshakeRsaKeyProvider(new EncryptionConfiguration { RsaPrivateKeyFile = path }, _ => null, NullLogger.Instance);

            Assert.True(provider.IsConfigured);
            Assert.Equal(HandshakeRsaKey.FromPem(pem).ModulusHex, provider.Key.ModulusHex);
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ProviderFallsBackToTheLegacyKeyWhenNothingIsConfigured()
    {
        var provider = new HandshakeRsaKeyProvider(new EncryptionConfiguration(), _ => null, NullLogger.Instance);

        Assert.False(provider.IsConfigured);
        Assert.False(provider.Key.UsesAirExponent);
        Assert.Equal(new RsaKeys().N, provider.Key.ModulusHex);
    }

    private static async Task RunHandshake(AirClient air)
    {
        var server = new FakeServer();
        var sent = new List<byte[]>();
        var session = new FlashGameClient(server, new FlashPacketFactory(), TestLogging.GameClient)
        {
            Revision = new Revision
            {
                IncomingIdToInternalIdMapping = new Dictionary<uint, uint> { [TestIncomingId] = TestIncomingId },
                InternalIdToOutgoingIdMapping = new Dictionary<uint, uint>
                {
                    [ServerPacketHeader.InitDiffieHandshakeComposer] = InitDiffieId,
                    [ServerPacketHeader.SecretKeyComposer] = SecretKeyId,
                    [TestComposer.InternalId] = TestComposerId
                }
            },
            DisconnectRequested = () => { },
            SendCallback = args =>
            {
                sent.Add(args.MemoryBuffer.Slice(args.Offset, args.Count).ToArray());

                return false;
            }
        };

        // In 3371: the signed prime and generator.
        await new InitDiffieHandshakeEvent().Parse(session, new FlashIncomingPacket());
        var init = new FrameReader(Assert.Single(sent));
        Assert.Equal(InitDiffieId, init.Header);
        var clientPublicKey = air.OnInitDiffieHandshake(init.ReadString(), init.ReadString());
        sent.Clear();

        // Out 1830 (sent unencrypted), then in 137: the signed server key and the serverClientEncryption flag.
        using (var stream = PlusMemoryStream.GetStream(FrameReader.String(clientPublicKey))) {
            await new CompleteDiffieHandshakeEvent().Parse(session, new FlashIncomingPacket(stream));
        }

        var secret = new FrameReader(Assert.Single(sent));
        Assert.Equal(SecretKeyId, secret.Header);
        var encryptedPublicKey = secret.ReadString();
        Assert.True(secret.HasBytes, "class_4247 reads serverClientEncryption only when bytes remain.");
        var (outgoing, incoming) = air.OnCompleteDiffieHandshake(encryptedPublicKey, secret.ReadBool());
        Assert.NotNull(incoming);
        sent.Clear();

        // Client to server: the client enciphers everything it sends after the handshake.
        var frame = new byte[] { 0, 0, 0, 2, 0, (byte)TestIncomingId };
        outgoing.Encipher(frame);
        session.OnReceived(frame, 0, frame.Length);
        Assert.Equal(new[] { TestIncomingId }, server.MessageIds);

        // Server to client: the client deciphers incoming data because the flag was true.
        session.Send(new TestComposer());
        var reply = Assert.Single(sent);
        incoming!.Encipher(reply);
        Assert.Equal(new byte[] { 0, 0, 0, 3, 0, (byte)TestComposerId, 0x2A }, reply);
    }

    private sealed class AirClient(string modulusHex)
    {
        private readonly HurlantRsa _rsa = new(SystemBigInteger.Parse("0" + modulusHex, NumberStyles.HexNumber), 0x10001);
        private SystemBigInteger _prime;
        private SystemBigInteger _privateKey;

        public string OnInitDiffieHandshake(string encryptedPrime, string encryptedGenerator)
        {
            var prime = SystemBigInteger.Parse(Encoding.UTF8.GetString(_rsa.Verify(CryptoTools.HexStringToByteArray(encryptedPrime))));
            var generator = SystemBigInteger.Parse(Encoding.UTF8.GetString(_rsa.Verify(CryptoTools.HexStringToByteArray(encryptedGenerator))));
            Assert.False(prime <= 2 || generator >= prime, "Invalid DH prime and generator");

            string? best = null;
            SystemBigInteger bestPrivate = 0;

            for (var attempt = 10; attempt > 0; attempt--) {
                var privateKey = SystemBigInteger.Parse("0" + RandomHex(30), NumberStyles.HexNumber);
                var publicKey = SystemBigInteger.ModPow(generator, privateKey, prime).ToString(CultureInfo.InvariantCulture);

                if (publicKey.Length >= 64) {
                    best = publicKey;
                    bestPrivate = privateKey;

                    break;
                }

                if (best == null || publicKey.Length > best.Length) {
                    best = publicKey;
                    bestPrivate = privateKey;
                }
            }

            _prime = prime;
            _privateKey = bestPrivate;

            return CryptoTools.ByteArrayToHexString(_rsa.Encrypt(Encoding.Latin1.GetBytes(best!)));
        }

        public (ArcFour Outgoing, ArcFour? Incoming) OnCompleteDiffieHandshake(string encryptedPublicKey, bool serverClientEncryption)
        {
            var serverPublicKey = SystemBigInteger.Parse(Encoding.UTF8.GetString(_rsa.Verify(CryptoTools.HexStringToByteArray(encryptedPublicKey))));
            Assert.True(serverPublicKey >= 2, "isValidServerPublicKey");
            var sharedKey = ToRadix16(SystemBigInteger.ModPow(serverPublicKey, _privateKey, _prime)).ToUpperInvariant();
            var key = CryptoTools.HexStringToByteArray(sharedKey);

            return (new ArcFour(key), serverClientEncryption ? new ArcFour(key) : null);
        }

        // generateRandomHexString: param1 bytes, each two hex digits.
        private static string RandomHex(int bytes) => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(bytes));

        // hurlant BigInteger.toRadix(16): no leading zeros.
        private static string ToRadix16(SystemBigInteger value)
        {
            var hex = value.ToString("x", CultureInfo.InvariantCulture).TrimStart('0');

            return hex.Length == 0 ? "0" : hex;
        }
    }

    private sealed class HurlantRsa(SystemBigInteger n, int e)
    {
        private int BlockSize => (int)((n.GetBitLength() + 7) / 8);

        // verify: _decrypt(doPublic, ..., 1); each block is read as exactly BlockSize bytes.
        public byte[] Verify(byte[] source)
        {
            Assert.Equal(0, source.Length % BlockSize);
            var output = new List<byte>();

            for (var offset = 0; offset < source.Length; offset += BlockSize) {
                var c = new SystemBigInteger(source.AsSpan(offset, BlockSize), isUnsigned: true, isBigEndian: true);
                var unpadded = Pkcs1Unpad(SystemBigInteger.ModPow(c, e, n), 1);
                Assert.True(unpadded != null, "Decrypt error - padding function returned null!");
                output.AddRange(unpadded!);
            }

            return output.ToArray();
        }

        // encrypt: _encrypt(doPublic, ..., 2) with leading zero bytes up to the block size.
        public byte[] Encrypt(byte[] source)
        {
            var output = new List<byte>();
            var position = 0;

            while (position < source.Length) {
                var take = Math.Min(source.Length - position, BlockSize - 11);
                var padded = Pkcs1Pad(source.AsSpan(position, take), 2);
                position += take;
                var c = SystemBigInteger.ModPow(new SystemBigInteger(padded, isUnsigned: true, isBigEndian: true), e, n);
                var bytes = c.ToByteArray(isUnsigned: true, isBigEndian: true);
                output.AddRange(new byte[BlockSize - bytes.Length]);
                output.AddRange(bytes);
            }

            return output.ToArray();
        }

        private byte[] Pkcs1Pad(ReadOnlySpan<byte> source, byte type)
        {
            var output = new byte[BlockSize];
            var n = BlockSize;

            for (var i = source.Length - 1; i >= 0 && n > 11; i--) {
                output[--n] = source[i];
            }

            output[--n] = 0;

            while (n > 2) {
                byte filler;

                do {
                    filler = (byte)RandomNumberGenerator.GetInt32(256);
                } while (filler == 0);

                output[--n] = type == 2 ? filler : (byte)0xFF;
            }

            output[--n] = type;
            output[--n] = 0;

            return output;
        }

        // pkcs1unpad: BigInteger.toArray gives the minimal bytes, so the leading 00 is gone.
        private byte[]? Pkcs1Unpad(SystemBigInteger value, byte type)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            var i = 0;

            while (i < bytes.Length && bytes[i] == 0) {
                i++;
            }

            if (bytes.Length - i != BlockSize - 1 || bytes[i] != type) {
                return null;
            }

            i++;

            while (bytes[i] != 0) {
                if (++i >= bytes.Length) {
                    return null;
                }
            }

            return bytes[(i + 1)..];
        }
    }

    private static class CryptoTools
    {
        public static byte[] HexStringToByteArray(string hex)
        {
            if (hex.Length % 2 != 0) {
                hex = "0" + hex;
            }

            var bytes = new byte[hex.Length / 2];

            for (var i = 0; i < bytes.Length; i++) {
                bytes[i] = (byte)((int.Parse(hex[2 * i].ToString(), NumberStyles.HexNumber) << 4) | int.Parse(hex[2 * i + 1].ToString(), NumberStyles.HexNumber));
            }

            return bytes;
        }

        public static string ByteArrayToHexString(byte[] bytes) => Convert.ToHexStringLower(bytes);
    }

    private sealed class ArcFour
    {
        private readonly uint[] _sbox = new uint[256];
        private uint _i;
        private uint _j;

        public ArcFour(byte[] key)
        {
            for (_i = 0; _i < 256; _i++) {
                _sbox[_i] = _i;
            }

            _j = 0;

            for (_i = 0; _i < 256; _i++) {
                _j = (_j + _sbox[_i] + key[_i % key.Length]) % 256;
                (_sbox[_i], _sbox[_j]) = (_sbox[_j], _sbox[_i]);
            }

            _i = 0;
            _j = 0;
        }

        public void Encipher(byte[] data)
        {
            for (var k = 0; k < data.Length; k++) {
                data[k] = (byte)(data[k] ^ Next());
            }
        }

        private uint Next()
        {
            _i = (_i + 1) & 0xFF;
            _j = (_j + _sbox[_i]) & 0xFF;
            var swap = _sbox[_i];
            _sbox[_i] = _sbox[_j];
            _sbox[_j] = swap;

            return _sbox[(swap + _sbox[_i]) & 0xFF];
        }
    }

    private sealed class FrameReader
    {
        private readonly byte[] _frame;
        private int _position = 6;

        public FrameReader(byte[] frame)
        {
            _frame = frame;
            Assert.Equal(frame.Length - 4, BinaryPrimitives.ReadInt32BigEndian(frame));
            Header = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4));
        }

        public uint Header { get; }
        public bool HasBytes => _position < _frame.Length;

        public string ReadString()
        {
            var length = BinaryPrimitives.ReadUInt16BigEndian(_frame.AsSpan(_position));
            var value = Encoding.UTF8.GetString(_frame, _position + 2, length);
            _position += 2 + length;

            return value;
        }

        public bool ReadBool() => _frame[_position++] == 1;

        public static byte[] String(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var output = new byte[2 + bytes.Length];
            BinaryPrimitives.WriteUInt16BigEndian(output, (ushort)bytes.Length);
            bytes.CopyTo(output, 2);

            return output;
        }
    }

    private sealed class FakeServer : IGameServer
    {
        public List<uint> MessageIds { get; } = new();

        public bool Start() => true;
        public bool Stop() => true;

        public Task PacketReceived(GameClient client, uint messageId, IIncomingPacket packet)
        {
            MessageIds.Add(messageId);

            return Task.CompletedTask;
        }

        public bool ModifyOutgoingPacket(GameClient client, IOutgoingPacket packet) => true;
        public bool HasOutgoingPacketInjectors(uint messageId) => false;
    }

    private sealed class TestComposer : IServerPacket
    {
        public const uint InternalId = 900_001;
        public uint MessageId => InternalId;
        public void Compose(IOutgoingPacket packet) => packet.WriteByte(0x2A);
    }
}
