using System.Globalization;
using System.Security.Cryptography;
using SystemBigInteger = System.Numerics.BigInteger;

namespace Plus.Communication.Encryption.Keys;

/// <summary>
/// The RSA key of the Diffie-Hellman handshake. The client holds only the public half and uses it two ways:
/// <list type="bullet">
/// <item><c>verify</c> (public exponent, PKCS#1 type 1) checks the prime, generator and server public key this key signs.</item>
/// <item><c>encrypt</c> (public exponent, PKCS#1 type 2) hides the client's public key, which this key decrypts.</item>
/// </list>
/// The client reads each block as exactly <see cref="BlockSize"/> bytes, so signatures are left-padded to it.
/// </summary>
public sealed class HandshakeRsaKey
{
    /// <summary>The exponent the client hard-codes: <c>RSAKey.parsePublicKey(..., "10001")</c>.</summary>
    public const int AirPublicExponent = 65537;

    private readonly SystemBigInteger _n;
    private readonly SystemBigInteger _e;
    private readonly SystemBigInteger _d;

    public HandshakeRsaKey(SystemBigInteger n, SystemBigInteger e, SystemBigInteger d)
    {
        if (n.Sign <= 0 || e.Sign <= 0 || d.Sign <= 0) {
            throw new ArgumentException("The RSA key needs a positive modulus and exponents.");
        }

        _n = n;
        _e = e;
        _d = d;
        BlockSize = (int)((n.GetBitLength() + 7) / 8);

        if (BlockSize < 64) {
            throw new ArgumentException("The RSA modulus is too short for a handshake.");
        }
    }

    public int BlockSize { get; }
    public long ModulusBits => (long)_n.GetBitLength();
    public SystemBigInteger PublicExponent => _e;
    public bool UsesAirExponent => _e == AirPublicExponent;

    /// <summary>The modulus as uppercase hex without a sign byte, as the client's string table holds it.</summary>
    public string ModulusHex => Convert.ToHexString(_n.ToByteArray(isUnsigned: true, isBigEndian: true));

    public static HandshakeRsaKey FromHex(string n, string e, string d) => new(ParseHex(n), ParseHex(e), ParseHex(d));

    /// <summary>Reads a PEM private key (PKCS#1 "RSA PRIVATE KEY" or PKCS#8 "PRIVATE KEY").</summary>
    public static HandshakeRsaKey FromPem(string pem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        var parameters = rsa.ExportParameters(includePrivateParameters: true);

        return new(Unsigned(parameters.Modulus!), Unsigned(parameters.Exponent!), Unsigned(parameters.D!));
    }

    /// <summary>A new 2048-bit key with the exponent the client uses, as a PKCS#8 PEM.</summary>
    public static string GeneratePem(int bits = 2048)
    {
        // .NET always generates with e = 65537.
        using var rsa = RSA.Create(bits);

        return rsa.ExportPkcs8PrivateKeyPem();
    }

    /// <summary>PKCS#1 type 1 with the private exponent: what the client's <c>verify</c> undoes.</summary>
    public byte[] Sign(byte[] message)
    {
        var padded = Pad(message, type: 1);
        var signature = SystemBigInteger.ModPow(Unsigned(padded), _d, _n);

        return ToBlock(signature);
    }

    /// <summary>Undoes the client's <c>encrypt</c> (PKCS#1 type 2). Null when the block does not unpad.</summary>
    public byte[]? Decrypt(byte[] cipher)
    {
        if (cipher.Length == 0 || cipher.Length > BlockSize) {
            return null;
        }

        var c = Unsigned(cipher);

        if (c >= _n) {
            return null;
        }

        return Unpad(ToBlock(SystemBigInteger.ModPow(c, _d, _n)), expectedType: 2);
    }

    // pkcs1pad: 00 type filler 00 message, filled to the block size.
    private byte[] Pad(byte[] message, byte type)
    {
        if (message.Length > BlockSize - 11) {
            throw new ArgumentException("The message does not fit in one RSA block.");
        }

        var block = new byte[BlockSize];
        block[1] = type;
        var separator = BlockSize - message.Length - 1;

        for (var i = 2; i < separator; i++) {
            block[i] = type == 1 ? (byte)0xFF : NonZeroRandomByte();
        }

        message.CopyTo(block, separator + 1);

        return block;
    }

    // pkcs1unpad on a full block: 00, the type byte, non-zero filler, a zero, then the message.
    private static byte[]? Unpad(byte[] block, byte expectedType)
    {
        if (block.Length < 11 || block[0] != 0 || block[1] != expectedType) {
            return null;
        }

        var separator = Array.IndexOf(block, (byte)0, 2);

        return separator < 0 ? null : block[(separator + 1)..];
    }

    private byte[] ToBlock(SystemBigInteger value)
    {
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        var block = new byte[BlockSize];
        bytes.CopyTo(block, BlockSize - bytes.Length);

        return block;
    }

    private static byte NonZeroRandomByte()
    {
        byte value;

        do {
            value = (byte)RandomNumberGenerator.GetInt32(256);
        } while (value == 0);

        return value;
    }

    private static SystemBigInteger Unsigned(byte[] bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);

    private static SystemBigInteger ParseHex(string hex) => SystemBigInteger.Parse("0" + hex.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
