using System.Text;
using Plus.Communication.Encryption.KeyExchange;
using Plus.Communication.Encryption.Keys;
using Plus.Utilities;

namespace Plus.Communication.Encryption;

public static class HabboEncryptionV2
{
    private static HandshakeRsaKey? _rsa;
    private static DiffieHellman? _diffieHellman;
    private static string? _signedPrime;
    private static string? _signedGenerator;
    private static string? _signedPublicKey;
    private static HandshakeRsaKey Rsa => _rsa ?? throw new InvalidOperationException("Encryption has not been initialized.");
    private static DiffieHellman DiffieHellman => _diffieHellman ?? throw new InvalidOperationException("Encryption has not been initialized.");

    public static void Initialize(HandshakeRsaKey key)
    {
        _rsa = key;
        _diffieHellman = new();
        _signedPrime = GetRsaStringEncrypted(_diffieHellman.Prime.ToString(10));
        _signedGenerator = GetRsaStringEncrypted(_diffieHellman.Generator.ToString(10));
        _signedPublicKey = GetRsaStringEncrypted(_diffieHellman.PublicKey.ToString(10));
    }

    // The client reads the signed number with ByteArray.toString() and parses it in base 10.
    private static string GetRsaStringEncrypted(string message)
    {
        try {
            return Converter.BytesToHexString(Rsa.Sign(Encoding.ASCII.GetBytes(message)));
        }
        catch {
            return "0";
        }
    }

    public static string GetRsaDiffieHellmanPrimeKey() => _signedPrime ?? throw new InvalidOperationException("Encryption has not been initialized.");

    public static string GetRsaDiffieHellmanGeneratorKey() => _signedGenerator ?? throw new InvalidOperationException("Encryption has not been initialized.");

    public static string GetRsaDiffieHellmanPublicKey() => _signedPublicKey ?? throw new InvalidOperationException("Encryption has not been initialized.");

    /// <summary>The shared key from the client's RSA-encrypted, base-10 public key. 0 when it does not decrypt.</summary>
    public static BigInteger CalculateDiffieHellmanSharedKey(string publicKey)
    {
        try {
            var publicKeyBytes = Rsa.Decrypt(Converter.HexStringToBytes(publicKey));

            if (publicKeyBytes == null) {
                return 0;
            }

            return DiffieHellman.CalculateSharedKey(new(Encoding.ASCII.GetString(publicKeyBytes), 10));
        }
        catch {
            return 0;
        }
    }

    /// <summary>
    /// The RC4 key the client derives from the shared key. The client turns the upper-case hex string into bytes and prefixes a "0" to an odd-length string.
    /// So the key is the shared key's big-endian bytes with no leading zero byte.
    /// </summary>
    public static byte[] Rc4KeyFromSharedKey(BigInteger sharedKey) => Rc4KeyFromSharedKeyHex(sharedKey.ToString(16));

    internal static byte[] Rc4KeyFromSharedKeyHex(string hex) => Converter.HexStringToBytes(hex.Length % 2 == 0 ? hex : "0" + hex);
}
